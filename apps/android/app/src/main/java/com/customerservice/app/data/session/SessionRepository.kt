package com.customerservice.app.data.session

import com.customerservice.app.data.net.ApiFailure
import com.customerservice.app.data.net.ApiResult
import com.customerservice.app.data.net.AuthApi
import com.customerservice.app.data.net.ChangePasswordRequest
import com.customerservice.app.data.net.LoginRequest
import com.customerservice.app.data.net.MeResponse
import com.customerservice.app.data.net.apiCall
import com.customerservice.app.data.net.apiCallUnit
import com.customerservice.app.ui.AppState
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext

/**
 * Owns [AppState]. Every state change runs inside [transition], under one mutex; the private `*Locked`
 * helpers assume the lock is held and never take it again. A [SessionEvent.Expired] that arrives while
 * `signIn`, `signOut` or `start` is in flight waits for that call to finish, then applies to the result:
 * it is ignored if the store holds a token again (a stale 401 must not end a newer session) and is a no-op
 * when already signed out. Passwords and tokens never enter state, exceptions or logs.
 */
class SessionRepository(
    private val api: AuthApi,
    private val store: SessionStore,
    events: SharedFlow<SessionEvent>,
    scope: CoroutineScope,
    private val log: AuthLog = AuthLog.None,
) {
    private val mutex = Mutex()
    private val _state = MutableStateFlow<AppState>(AppState.Launching)
    val state: StateFlow<AppState> = _state.asStateFlow()

    init {
        scope.launch {
            events.collect { event ->
                when (event) {
                    SessionEvent.Expired -> transition { if (store.awaitToken() == null) expireLocked() }
                }
            }
        }
    }

    suspend fun start() = transition {
        if (_state.value == AppState.Launching) checkLocked()
    }

    suspend fun retry() = transition {
        if (_state.value is AppState.Checking) checkLocked()
    }

    suspend fun signIn(username: String, password: String): ApiResult<Unit> = transition {
        when (val result = apiCall { api.login(LoginRequest(username, password)) }) {
            is ApiResult.Failure -> {
                log.info("Sign-in failed: ${result.failure.kind()}")
                result
            }
            is ApiResult.Ok -> {
                val login = result.value
                store.save(login.token)
                _state.value = if (login.mustChangePassword) AppState.PasswordChange else AppState.SignedIn(login.role)
                log.info("Sign-in succeeded")
                ApiResult.Ok(Unit)
            }
        }
    }

    suspend fun signOut() = transition {
        try {
            apiCallUnit { api.logout() }
        } finally {
            withContext(NonCancellable) {
                store.clear()
                _state.value = AppState.SignedOut
                log.info("Signed out")
            }
        }
    }

    suspend fun changePassword(current: String, new: String): ApiResult<Unit> = transition {
        val result = apiCallUnit { api.changePassword(ChangePasswordRequest(current, new)) }
        // The password did change on a 204 even if the follow-up check fails; retry() repeats that check.
        if (result is ApiResult.Ok) checkLocked()
        result
    }

    private suspend fun checkLocked() {
        val token = store.awaitToken()
        if (token == null) {
            _state.value = AppState.SignedOut
            return
        }
        val previous = _state.value
        _state.value = AppState.Checking(null)
        val result = try {
            apiCall { api.me() }
        } catch (e: CancellationException) {
            // A cancelled check must not park the state at Checking(null), which has no Retry.
            _state.value = previous
            throw e
        }
        when (result) {
            is ApiResult.Ok -> {
                _state.value = result.value.toState()
                log.info("Session restored")
            }
            is ApiResult.Failure -> if (result.failure == ApiFailure.Unauthorized) {
                store.clearIf(token)
                expireLocked()
            } else {
                _state.value = AppState.Checking(result.failure)
            }
        }
    }

    private fun expireLocked() {
        if (_state.value != AppState.SignedOut) log.info("Session expired")
        _state.value = AppState.SignedOut
    }

    private fun ApiFailure.kind(): String = when (this) {
        ApiFailure.Unauthorized -> "Unauthorized"
        ApiFailure.Forbidden -> "Forbidden"
        is ApiFailure.Validation -> "Validation"
        ApiFailure.NotFound -> "NotFound"
        ApiFailure.Conflict -> "Conflict"
        ApiFailure.Unavailable -> "Unavailable"
    }

    private fun MeResponse.toState(): AppState =
        if (mustChangePassword) AppState.PasswordChange else AppState.SignedIn(role)

    private suspend fun <T> transition(block: suspend () -> T): T = mutex.withLock { block() }
}
