package com.customerservice.app.ui

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.IconButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import androidx.compose.foundation.layout.padding
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.customerservice.app.data.net.ApiFailure
import com.customerservice.app.ui.password.ChangePasswordScreen
import com.customerservice.app.ui.password.ChangePasswordViewModel
import com.customerservice.app.ui.signin.SignInScreen
import com.customerservice.app.ui.signin.SignInViewModel

const val ROUTE_SIGN_IN = "sign-in"
const val ROUTE_CHANGE_PASSWORD = "change-password"
const val ROUTE_CUSTOMERS = "customers"
const val CUSTOMERS_TITLE = "Customers"
const val ACCOUNT_MENU_TAG = "account-menu"

/**
 * Each state kind gets its own NavHost and controller, so a state change starts from an empty back stack
 * and only [AppState.SignedIn] ever declares customer routes. Navigation follows state, never results.
 */
@Composable
fun AppRoot(
    state: AppState,
    signInViewModel: SignInViewModel,
    changePasswordViewModel: ChangePasswordViewModel,
    onRetry: () -> Unit,
    onSignOut: () -> Unit,
    onNavController: (NavHostController) -> Unit = {},
) {
    when (state) {
        AppState.Launching, is AppState.Checking -> Splash(state, onRetry)
        else -> key(state::class) {
            val navController = rememberNavController()
            onNavController(navController)
            when (state) {
                AppState.SignedOut -> NavHost(navController, startDestination = ROUTE_SIGN_IN) {
                    composable(ROUTE_SIGN_IN) { SignInScreen(signInViewModel) }
                }
                AppState.PasswordChange -> NavHost(navController, startDestination = ROUTE_CHANGE_PASSWORD) {
                    composable(ROUTE_CHANGE_PASSWORD) { ChangePasswordScreen(changePasswordViewModel, onSignOut) }
                }
                is AppState.SignedIn -> NavHost(navController, startDestination = ROUTE_CUSTOMERS) {
                    composable(ROUTE_CUSTOMERS) {
                        CustomerListPlaceholder(
                            onChangePassword = { navController.navigate(ROUTE_CHANGE_PASSWORD) },
                            onSignOut = onSignOut,
                        )
                    }
                    composable(ROUTE_CHANGE_PASSWORD) { ChangePasswordScreen(changePasswordViewModel, onSignOut) }
                }
                else -> Unit
            }
        }
    }
}

@Composable
private fun Splash(state: AppState, onRetry: () -> Unit) {
    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        val failure = (state as? AppState.Checking)?.failure
        if (failure == null) {
            CircularProgressIndicator()
        } else {
            ErrorMessage(failure, onRetry = if (failure == ApiFailure.Unavailable) onRetry else null)
        }
    }
}

// Placeholder until the customer list lands (ticket #37).
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun CustomerListPlaceholder(onChangePassword: () -> Unit, onSignOut: () -> Unit) {
    var menuOpen by remember { mutableStateOf(false) }
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(CUSTOMERS_TITLE) },
                actions = {
                    IconButton(onClick = { menuOpen = true }, modifier = Modifier.testTag(ACCOUNT_MENU_TAG)) {
                        Text("Menu")
                    }
                    DropdownMenu(expanded = menuOpen, onDismissRequest = { menuOpen = false }) {
                        DropdownMenuItem(
                            text = { Text("Change password") },
                            onClick = { menuOpen = false; onChangePassword() },
                        )
                        DropdownMenuItem(
                            text = { Text("Sign out") },
                            onClick = { menuOpen = false; onSignOut() },
                        )
                    }
                },
            )
        },
    ) { padding -> Box(Modifier.fillMaxSize().padding(padding).padding(16.dp)) }
}
