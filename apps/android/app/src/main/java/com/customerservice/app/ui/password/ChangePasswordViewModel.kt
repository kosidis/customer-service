package com.customerservice.app.ui.password

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

private const val CURRENT_PASSWORD_KEY = "currentpassword"
private const val NEW_PASSWORD_KEY = "newpassword"

data class ChangePasswordUiState(
    val currentPassword: String = "",
    val newPassword: String = "",
    val submitting: Boolean = false,
    val currentPasswordErrors: List<String> = emptyList(),
    val newPasswordErrors: List<String> = emptyList(),
    val failure: ApiFailure? = null,
)

class ChangePasswordViewModel(private val repository: SessionRepository) : ViewModel() {
    private val _state = MutableStateFlow(ChangePasswordUiState())
    val state: StateFlow<ChangePasswordUiState> = _state.asStateFlow()

    fun onCurrentPasswordChange(value: String) = _state.update { it.copy(currentPassword = value) }

    fun onNewPasswordChange(value: String) = _state.update { it.copy(newPassword = value) }

    fun submit() {
        val current = _state.value
        if (current.submitting) return
        _state.update {
            it.copy(submitting = true, currentPasswordErrors = emptyList(), newPasswordErrors = emptyList(), failure = null)
        }
        viewModelScope.launch {
            val result = repository.changePassword(current.currentPassword, current.newPassword)
            _state.update { it.after(result) }
        }
    }

    // Typed values stay on every failure; the API's field errors are shown as sent, with no client-side rules.
    private fun ChangePasswordUiState.after(result: ApiResult<Unit>): ChangePasswordUiState = when (result) {
        is ApiResult.Ok -> copy(currentPassword = "", newPassword = "", submitting = false)
        is ApiResult.Failure -> {
            val failure = result.failure
            if (failure is ApiFailure.Validation) {
                val current = failure.fieldErrors[CURRENT_PASSWORD_KEY].orEmpty()
                val new = failure.fieldErrors[NEW_PASSWORD_KEY].orEmpty()
                copy(
                    submitting = false,
                    currentPasswordErrors = current,
                    newPasswordErrors = new,
                    failure = if (current.isEmpty() && new.isEmpty()) ApiFailure.Validation(emptyMap()) else null,
                )
            } else {
                copy(submitting = false, failure = failure)
            }
        }
    }
}
