package com.customerservice.app.data.session

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.core.Preferences
import app.cash.turbine.test
import com.customerservice.app.data.net.ApiFailure
import com.customerservice.app.data.net.ApiResult
import com.customerservice.app.data.net.AuthApi
import com.customerservice.app.data.net.ChangePasswordRequest
import com.customerservice.app.data.net.LoginRequest
import com.customerservice.app.data.net.LoginResponse
import com.customerservice.app.data.net.MeResponse
import com.customerservice.app.ui.AppState
import java.io.File
import java.io.IOException
import javax.crypto.KeyGenerator
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.async
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.runTest
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import retrofit2.Response

@OptIn(ExperimentalCoroutinesApi::class)
class SessionRepositoryTest {
    @get:Rule
    val tmp = TemporaryFolder()

    private val ioScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    private class CountingDataStore(private val delegate: DataStore<Preferences>) : DataStore<Preferences> by delegate {
        var updates = 0
        var failUpdates = false

        override suspend fun updateData(transform: suspend (t: Preferences) -> Preferences): Preferences {
            updates++
            if (failUpdates) throw IOException("disk full")
            return delegate.updateData(transform)
        }
    }

    private class FakeAuthApi : AuthApi {
        var loginResult: () -> Response<LoginResponse> = { error(401) }
        val meResults = ArrayDeque<() -> Response<MeResponse>>()
        var logoutResult: suspend () -> Response<Unit> = { Response.success(null) }
        var changeResult: () -> Response<Unit> = { Response.success(null) }
        val calls = mutableListOf<String>()

        override suspend fun login(request: LoginRequest): Response<LoginResponse> {
            calls += "login"
            return loginResult()
        }

        override suspend fun logout(): Response<Unit> {
            calls += "logout"
            return logoutResult()
        }

        override suspend fun me(): Response<MeResponse> {
            calls += "me"
            return meResults.removeFirst()()
        }

        override suspend fun changePassword(request: ChangePasswordRequest): Response<Unit> {
            calls += "changePassword"
            return changeResult()
        }
    }

    private val api = FakeAuthApi()
    private val events = MutableSharedFlow<SessionEvent>(extraBufferCapacity = 8)
    private lateinit var dataStore: CountingDataStore
    private lateinit var store: SessionStore

    @Before
    fun setUp() {
        val file = File(tmp.newFolder(), "session.preferences_pb")
        dataStore = CountingDataStore(PreferenceDataStoreFactory.create(scope = ioScope, produceFile = { file }))
        val key = KeyGenerator.getInstance("AES").apply { init(256) }.generateKey()
        store = SessionStore(dataStore, TokenCipher { key })
    }

    @After
    fun tearDown() {
        ioScope.cancel()
    }

    private fun <T> error(code: Int): Response<T> =Response.error(code, "".toResponseBody())

    private fun me(role: String = "Admin", mustChange: Boolean = false): () -> Response<MeResponse> =
        { Response.success(MeResponse("u", role, mustChange)) }

    private fun TestScope.repo() = SessionRepository(
        api,
        store,
        events,
        CoroutineScope(backgroundScope.coroutineContext + UnconfinedTestDispatcher(testScheduler)),
    )

    @Test
    fun start_noToken_signedOut() = runTest {
        val repo = repo()

        repo.start()

        assertEquals(AppState.SignedOut, repo.state.value)
        assertTrue(api.calls.isEmpty())
    }

    @Test
    fun start_tokenAndMeRole_signedIn() = runTest {
        store.save("t")
        api.meResults += me("Viewer")
        val repo = repo()

        repo.start()

        assertEquals(AppState.SignedIn("Viewer"), repo.state.value)
    }

    @Test
    fun start_meMustChange_passwordChange() = runTest {
        store.save("t")
        api.meResults += me(mustChange = true)
        val repo = repo()

        repo.start()

        assertEquals(AppState.PasswordChange, repo.state.value)
    }

    @Test
    fun start_me401_clearsStoreSignedOut() = runTest {
        store.save("t")
        api.meResults += { error(401) }
        val repo = repo()

        repo.start()

        assertEquals(AppState.SignedOut, repo.state.value)
        assertNull(store.awaitToken())
    }

    @Test
    fun start_networkError_keepsTokenAndStaysChecking() = runTest {
        store.save("t")
        api.meResults += { throw IOException("offline") }
        val repo = repo()

        repo.start()

        assertEquals(AppState.Checking(ApiFailure.Unavailable), repo.state.value)
        assertEquals("t", store.awaitToken())
    }

    @Test
    fun start_me5xx_staysCheckingWithToken() = runTest {
        store.save("t")
        api.meResults += { error(503) }
        val repo = repo()

        repo.start()

        assertEquals(AppState.Checking(ApiFailure.Unavailable), repo.state.value)
        assertEquals("t", store.awaitToken())
    }

    @Test
    fun start_cancelledDuringMe_restoresLaunchingAndCanStartAgain() = runTest {
        store.save("t")
        val meStarted = CompletableDeferred<Unit>()
        api.meResults += { meStarted.complete(Unit); throw kotlinx.coroutines.CancellationException("destroyed") }
        api.meResults += me("Admin")
        val repo = repo()

        val job = async { runCatching { repo.start() } }
        job.await()

        assertTrue(meStarted.isCompleted)
        assertEquals(AppState.Launching, repo.state.value)
        repo.start()
        assertEquals(AppState.SignedIn("Admin"), repo.state.value)
    }

    @Test
    fun retry_afterNetworkError_signedIn() = runTest {
        store.save("t")
        api.meResults += { throw IOException("offline") }
        api.meResults += me("Admin")
        val repo = repo()
        repo.start()

        repo.retry()

        assertEquals(AppState.SignedIn("Admin"), repo.state.value)
    }

    @Test
    fun signIn_success_savesTokenAndSignedIn() = runTest {
        api.loginResult = { Response.success(LoginResponse("tok", "u", "Viewer", false)) }
        val repo = repo()
        repo.start()

        val result = repo.signIn("u", "p")

        assertEquals(ApiResult.Ok(Unit), result)
        assertEquals(AppState.SignedIn("Viewer"), repo.state.value)
        assertEquals("tok", store.awaitToken())
    }

    @Test
    fun signIn_mustChangePassword_passwordChange() = runTest {
        api.loginResult = { Response.success(LoginResponse("tok", "u", "Admin", true)) }
        val repo = repo()
        repo.start()

        repo.signIn("u", "p")

        assertEquals(AppState.PasswordChange, repo.state.value)
        assertEquals("tok", store.awaitToken())
    }

    @Test
    fun signIn_401_staysSignedOutSavingNothing() = runTest {
        api.loginResult = { error(401) }
        val repo = repo()
        repo.start()

        val result = repo.signIn("u", "bad")

        assertEquals(ApiResult.Failure(ApiFailure.Unauthorized), result)
        assertEquals(AppState.SignedOut, repo.state.value)
        assertNull(store.awaitToken())
    }

    @Test
    fun signIn_networkError_staysSignedOut() = runTest {
        api.loginResult = { throw IOException("offline") }
        val repo = repo()
        repo.start()

        val result = repo.signIn("u", "p")

        assertEquals(ApiResult.Failure(ApiFailure.Unavailable), result)
        assertEquals(AppState.SignedOut, repo.state.value)
        assertNull(store.awaitToken())
    }

    @Test
    fun changePassword_204_callsMeThenSignedIn() = runTest {
        store.save("t")
        api.meResults += me(mustChange = true)
        api.meResults += me("Admin")
        val repo = repo()
        repo.start()

        val result = repo.changePassword("old", "new")

        assertEquals(ApiResult.Ok(Unit), result)
        assertEquals(listOf("me", "changePassword", "me"), api.calls)
        assertEquals(AppState.SignedIn("Admin"), repo.state.value)
    }

    @Test
    fun changePassword_400_returnsFailureStaysPasswordChange() = runTest {
        store.save("t")
        api.meResults += me(mustChange = true)
        api.changeResult = { error(400) }
        val repo = repo()
        repo.start()

        val result = repo.changePassword("old", "new")

        assertTrue(result is ApiResult.Failure)
        assertEquals(AppState.PasswordChange, repo.state.value)
    }

    @Test
    fun changePassword_meFails_checkingWithFailureThenRetrySignedIn() = runTest {
        store.save("t")
        api.meResults += me(mustChange = true)
        api.meResults += { throw IOException("offline") }
        api.meResults += me("Admin")
        val repo = repo()
        repo.start()

        val result = repo.changePassword("old", "new")

        assertEquals(ApiResult.Ok(Unit), result)
        assertEquals(AppState.Checking(ApiFailure.Unavailable), repo.state.value)
        repo.retry()
        assertEquals(AppState.SignedIn("Admin"), repo.state.value)
    }

    @Test
    fun signOut_logoutFails_stillClearsStoreSignedOut() = runTest {
        store.save("t")
        api.meResults += me()
        api.logoutResult = { throw IOException("offline") }
        val repo = repo()
        repo.start()

        repo.signOut()

        assertEquals(AppState.SignedOut, repo.state.value)
        assertNull(store.awaitToken())
    }

    @Test
    fun signOut_clearThrows_stillSignedOutAndNoExceptionEscapes() = runTest {
        store.save("t")
        api.meResults += me()
        val repo = repo()
        repo.start()
        dataStore.failUpdates = true

        repo.signOut()

        assertEquals(AppState.SignedOut, repo.state.value)
    }

    @Test
    fun start_meBodyUnparsable_checkingUnavailableNoCrash() = runTest {
        store.save("t")
        api.meResults += { throw kotlinx.serialization.SerializationException("bad body") }
        val repo = repo()

        repo.start()

        assertEquals(AppState.Checking(ApiFailure.Unavailable), repo.state.value)
        assertEquals("t", store.awaitToken())
    }

    @Test
    fun start_meThrowsUnexpected_checkingUnavailableNoCrash() = runTest {
        store.save("t")
        api.meResults += { throw IllegalStateException("boom") }
        val repo = repo()

        repo.start()

        assertEquals(AppState.Checking(ApiFailure.Unavailable), repo.state.value)
    }

    @Test
    fun signIn_loginBodyUnparsable_returnsFailureNoCrash() = runTest {
        api.loginResult = { throw kotlinx.serialization.SerializationException("bad body") }
        val repo = repo()

        val result = repo.signIn("u", "p")

        assertEquals(ApiResult.Failure(ApiFailure.Unavailable), result)
        assertEquals(AppState.Launching, repo.state.value)
    }

    @Test
    fun expiredEvent_whileSignedIn_signedOut() = runTest {
        val repo = repo()
        store.save("t")
        api.meResults += me()
        repo.start()

        store.clear()
        events.emit(SessionEvent.Expired)

        assertEquals(AppState.SignedOut, repo.state.value)
    }

    @Test
    fun expiredEvent_withNewerToken_isIgnored() = runTest {
        val repo = repo()
        store.save("new")
        api.meResults += me()
        repo.start()

        events.emit(SessionEvent.Expired)

        assertEquals(AppState.SignedIn("Admin"), repo.state.value)
    }

    @Test
    fun expiredEvent_duringSignOut_clearsOnce() = runTest {
        val repo = repo()
        store.save("t")
        api.meResults += me()
        repo.start()
        val logoutEntered = CompletableDeferred<Unit>()
        val release = CompletableDeferred<Unit>()
        api.logoutResult = {
            logoutEntered.complete(Unit)
            release.await()
            Response.success(null)
        }
        val baseline = dataStore.updates

        repo.state.test {
            assertEquals(AppState.SignedIn("Admin"), awaitItem())
            val signOut = async(UnconfinedTestDispatcher(testScheduler)) { repo.signOut() }
            logoutEntered.await()
            events.emit(SessionEvent.Expired)
            release.complete(Unit)
            signOut.await()

            assertEquals(AppState.SignedOut, awaitItem())
            expectNoEvents()
        }
        assertEquals(1, dataStore.updates - baseline)
        assertNull(store.awaitToken())
    }
}
