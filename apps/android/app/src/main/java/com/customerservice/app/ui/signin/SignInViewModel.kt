package com.customerservice.app.ui.signin

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.customerservice.app.data.net.ApiFailure
import com.customerservice.app.data.net.ApiResult
import com.customerservice.app.data.session.SessionRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

const val SIGN_IN_FAILED_MESSAGE = "Sign-in failed. Check your username and password and try again."

data class SignInUiState(
    val username: String = "",
    val password: String = "",
    val submitting: Boolean = false,
    val credentialsRejected: Boolean = false,
    val failure: ApiFailure? = null,
)

class SignInViewModel(private val repository: SessionRepository) : ViewModel() {
    private val _state = MutableStateFlow(SignInUiState())
    val state: StateFlow<SignInUiState> = _state.asStateFlow()

    fun onUsernameChange(value: String) = _state.update { it.copy(username = value) }

    fun onPasswordChange(value: String) = _state.update { it.copy(password = value) }

    fun submit() {
        val current = _state.value
        if (current.submitting) return
        _state.update { it.copy(submitting = true, credentialsRejected = false, failure = null) }
        viewModelScope.launch {
            val result = repository.signIn(current.username, current.password)
            _state.update { it.after(result) }
        }
    }

    // A rejected 400 or 401 looks the same to the user and empties the password; a network failure keeps it so Retry works.
    private fun SignInUiState.after(result: ApiResult<Unit>): SignInUiState = when (result) {
        is ApiResult.Ok -> copy(password = "", submitting = false)
        is ApiResult.Failure -> when (result.failure) {
            ApiFailure.Unauthorized, is ApiFailure.Validation ->
                copy(password = "", submitting = false, credentialsRejected = true)
            else -> copy(submitting = false, failure = result.failure)
        }
    }
}
