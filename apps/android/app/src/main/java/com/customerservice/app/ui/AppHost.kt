package com.customerservice.app.ui

import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.lifecycle.viewmodel.compose.viewModel
import com.customerservice.app.AppContainer
import com.customerservice.app.config.ApiConfig
import com.customerservice.app.config.ApiConfigResult
import com.customerservice.app.ui.config.ConfigErrorScreen
import com.customerservice.app.ui.password.ChangePasswordViewModel
import com.customerservice.app.ui.signin.SignInViewModel

/** [container] is only called for a valid base URL, so an invalid one builds no client and sends no request. */
@Composable
fun AppHost(baseUrl: String, container: () -> AppContainer) {
    when (ApiConfig.check(baseUrl)) {
        is ApiConfigResult.Invalid -> ConfigErrorScreen()
        is ApiConfigResult.Valid -> {
            val appContainer = container()
            val repository = appContainer.sessionRepository
            val state by repository.state.collectAsState()
            val signIn = viewModel { SignInViewModel(repository) }
            val changePassword = viewModel { ChangePasswordViewModel(repository) }
            AppRoot(
                state = state,
                signInViewModel = signIn,
                changePasswordViewModel = changePassword,
                onRetry = appContainer::retry,
                onSignOut = appContainer::signOut,
            )
        }
    }
}
