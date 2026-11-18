package com.customerservice.app.ui

import com.customerservice.app.data.net.ApiFailure

sealed interface AppState {
    data object Launching : AppState
    data object SignedOut : AppState

    /** A stored token is being confirmed with `me`; [failure] is set when that check failed and can be retried. */
    data class Checking(val failure: ApiFailure?) : AppState
    data object PasswordChange : AppState
    data class SignedIn(val role: String) : AppState
}
