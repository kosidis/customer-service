package com.customerservice.app.ui

import androidx.activity.ComponentActivity
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.navigation.NavHostController
import com.customerservice.app.data.net.ApiFailure
import com.customerservice.app.data.session.SessionRepository
import com.customerservice.app.ui.config.CONFIG_ERROR_TITLE
import com.customerservice.app.ui.password.ChangePasswordViewModel
import com.customerservice.app.ui.password.SIGN_OUT_TAG
import com.customerservice.app.ui.signin.SIGN_IN_TAG
import com.customerservice.app.ui.signin.SignInViewModel
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.flow.MutableStateFlow
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class AppRootTest {
    @get:Rule
    val compose = createAndroidComposeRule<ComponentActivity>()

    private val repository = mockk<SessionRepository>(relaxed = true) {
        every { state } returns MutableStateFlow<AppState>(AppState.PasswordChange)
    }
    private val signInViewModel = SignInViewModel(repository)
    private val changePasswordViewModel = ChangePasswordViewModel(repository)

    private var state by mutableStateOf<AppState>(AppState.SignedOut)
    private var controller: NavHostController? = null
    private var signOuts = 0
    private var retries = 0

    private fun show(initial: AppState) {
        state = initial
        compose.setContent {
            AppRoot(
                state = state,
                signInViewModel = signInViewModel,
                changePasswordViewModel = changePasswordViewModel,
                onRetry = { retries++ },
                onSignOut = { signOuts++ },
                onNavController = { controller = it },
            )
        }
    }

    private fun routes(): Set<String> = controller!!.graph.map { it.route.orEmpty() }.toSet()

    private fun currentRoute(): String? = controller!!.currentBackStackEntry?.destination?.route

    @Test
    fun AppRoot_signedOut_hasNoCustomerOrUserRoute() {
        show(AppState.SignedOut)

        compose.onNodeWithTag(SIGN_IN_TAG).assertIsDisplayed()
        assertEquals(setOf(ROUTE_SIGN_IN), routes())
        assertTrue(routes().none { it.startsWith("customers") || it.startsWith("users") })
    }

    @Test
    fun AppRoot_passwordChange_showsOnlyChangePasswordAndSignOut() {
        show(AppState.PasswordChange)

        compose.onNodeWithTag(SIGN_OUT_TAG).assertIsDisplayed()
        assertEquals(setOf(ROUTE_CHANGE_PASSWORD), routes())
        compose.onNodeWithTag(SIGN_OUT_TAG).performClick()
        assertEquals(1, signOuts)
    }

    @Test
    fun AppRoot_httpBaseUrl_showsConfigErrorAndMakesNoRequest() {
        var containersBuilt = 0
        compose.setContent {
            AppHost(baseUrl = "http://insecure.example.com", container = { containersBuilt++; error("must not build") })
        }

        compose.onNodeWithText(CONFIG_ERROR_TITLE).assertIsDisplayed()
        assertEquals(0, containersBuilt)
    }

    @Test
    fun AppRoot_signOut_emptiesBackStack() {
        show(AppState.SignedIn("Admin"))
        compose.onNodeWithTag(ACCOUNT_MENU_TAG).performClick()
        compose.onNodeWithText("Change password").performClick()
        compose.waitForIdle()
        assertEquals(ROUTE_CHANGE_PASSWORD, currentRoute())

        state = AppState.SignedOut
        compose.waitForIdle()

        assertEquals(setOf(ROUTE_SIGN_IN), routes())
        assertEquals(ROUTE_SIGN_IN, currentRoute())
        assertNull(controller!!.previousBackStackEntry)
        compose.activity.onBackPressedDispatcher.onBackPressed()
        compose.waitForIdle()
        assertEquals(ROUTE_SIGN_IN, currentRoute())
    }

    @Test
    fun AppRoot_signedIn_accountMenuOpensVoluntaryChangePassword() {
        show(AppState.SignedIn("Admin"))
        compose.onNodeWithText(CUSTOMERS_TITLE).assertIsDisplayed()

        compose.onNodeWithTag(ACCOUNT_MENU_TAG).performClick()
        compose.onNodeWithText("Change password").performClick()
        compose.waitForIdle()

        assertEquals(ROUTE_CHANGE_PASSWORD, currentRoute())
        compose.onNodeWithTag(SIGN_OUT_TAG).assertIsDisplayed()

        state = AppState.Checking(null)
        compose.waitForIdle()
        state = AppState.SignedIn("Admin")
        compose.waitForIdle()
        assertEquals(ROUTE_CUSTOMERS, currentRoute())
    }

    @Test
    fun AppRoot_checkingForbidden_offersRetryAndSignOut() {
        show(AppState.Checking(ApiFailure.Forbidden))

        compose.onNodeWithText("Retry").assertIsDisplayed().performClick()
        compose.onNodeWithTag(SIGN_OUT_TAG).assertIsDisplayed().performClick()

        assertEquals(1, retries)
        assertEquals(1, signOuts)
    }

    @Test
    fun AppRoot_checkingUnavailable_offersRetryAndSignOut() {
        show(AppState.Checking(ApiFailure.Unavailable))

        compose.onNodeWithText("Retry").assertIsDisplayed()
        compose.onNodeWithTag(SIGN_OUT_TAG).assertIsDisplayed()
    }

    @Test
    fun AppRoot_checkingInFlight_showsNoRetryOrSignOut() {
        show(AppState.Checking(null))

        compose.onNodeWithText("Retry").assertDoesNotExist()
        compose.onNodeWithTag(SIGN_OUT_TAG).assertDoesNotExist()
    }
}
