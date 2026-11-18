package com.customerservice.app.ui

import androidx.compose.foundation.layout.Column
import androidx.compose.material3.Button
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import com.customerservice.app.data.net.ApiFailure

private const val GENERIC_MESSAGE = "Something went wrong. Try again."

/**
 * Shared error UI. The text is fixed and never carries server detail. A **Retry** button shows only
 * when the caller passes [onRetry].
 */
@Composable
fun ErrorMessage(failure: ApiFailure, onRetry: (() -> Unit)?) {
    Column {
        Text(text = messageFor(failure))
        if (onRetry != null) {
            Button(onClick = onRetry) { Text("Retry") }
        }
    }
}

private fun messageFor(failure: ApiFailure): String = when (failure) {
    ApiFailure.Unavailable -> GENERIC_MESSAGE
    ApiFailure.Unauthorized -> "Your session has ended. Sign in again."
    ApiFailure.Forbidden -> "You do not have permission to do that."
    ApiFailure.NotFound -> "We could not find that."
    ApiFailure.Conflict -> "That conflicts with existing data."
    is ApiFailure.Validation -> "Check the highlighted fields and try again."
}
