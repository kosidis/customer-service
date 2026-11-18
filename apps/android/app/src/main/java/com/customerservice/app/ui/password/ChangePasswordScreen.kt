package com.customerservice.app.ui.password

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import com.customerservice.app.data.net.ApiFailure
import com.customerservice.app.ui.ErrorMessage
import com.customerservice.app.ui.SecureWindow

const val CURRENT_PASSWORD_TAG = "current-password"
const val NEW_PASSWORD_TAG = "new-password"
const val SUBMIT_TAG = "change-password"
const val SIGN_OUT_TAG = "sign-out"

@Composable
fun ChangePasswordScreen(viewModel: ChangePasswordViewModel, onSignOut: () -> Unit) {
    val state by viewModel.state.collectAsState()
    SecureWindow(enabled = true)
    Column(
        modifier = Modifier.fillMaxSize().padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp, Alignment.CenterVertically),
    ) {
        Text("Change password", style = MaterialTheme.typography.headlineMedium)
        PasswordField(
            value = state.currentPassword,
            onValueChange = viewModel::onCurrentPasswordChange,
            label = "Current password",
            errors = state.currentPasswordErrors,
            tag = CURRENT_PASSWORD_TAG,
        )
        PasswordField(
            value = state.newPassword,
            onValueChange = viewModel::onNewPasswordChange,
            label = "New password",
            errors = state.newPasswordErrors,
            tag = NEW_PASSWORD_TAG,
        )
        if (state.checking) CircularProgressIndicator()
        state.sessionFailure?.let { failure ->
            ErrorMessage(failure, onRetry = if (failure == ApiFailure.Unavailable) viewModel::retry else null)
        }
        state.failure?.let { failure ->
            ErrorMessage(failure, onRetry = if (failure == ApiFailure.Unavailable) viewModel::submit else null)
        }
        Button(
            onClick = viewModel::submit,
            enabled = !state.submitting && !state.checking && state.sessionFailure == null,
            modifier = Modifier.fillMaxWidth().testTag(SUBMIT_TAG),
        ) { Text("Change password") }
        TextButton(onClick = onSignOut, modifier = Modifier.fillMaxWidth().testTag(SIGN_OUT_TAG)) {
            Text("Sign out")
        }
    }
}

@Composable
private fun PasswordField(
    value: String,
    onValueChange: (String) -> Unit,
    label: String,
    errors: List<String>,
    tag: String,
) {
    OutlinedTextField(
        value = value,
        onValueChange = onValueChange,
        label = { Text(label) },
        singleLine = true,
        isError = errors.isNotEmpty(),
        supportingText = if (errors.isEmpty()) null else ({ Text(errors.joinToString("\n")) }),
        visualTransformation = PasswordVisualTransformation(),
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password, autoCorrectEnabled = false),
        modifier = Modifier.fillMaxWidth().testTag(tag),
    )
}
