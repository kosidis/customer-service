package com.customerservice.app.ui.signin

import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextInput
import com.customerservice.app.data.net.ApiFailure
import com.customerservice.app.data.net.ApiResult
import com.customerservice.app.data.session.SessionRepository
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class SignInScreenTest {
    @get:Rule
    val compose = createComposeRule()

    private val repository = mockk<SessionRepository>()

    private fun editableText(tag: String): String =
        compose.onNodeWithTag(tag).fetchSemanticsNode().config[SemanticsProperties.EditableText].text

    private fun submit(username: String, password: String) {
        compose.onNodeWithTag(USERNAME_TAG).performTextInput(username)
        compose.onNodeWithTag(PASSWORD_TAG).performTextInput(password)
        compose.onNodeWithTag(SIGN_IN_TAG).performClick()
        compose.waitForIdle()
    }

    @Test
    fun SignIn_wrongUsernameAndWrongPassword_showSameMessageAndClearPassword() {
        coEvery { repository.signIn(any(), any()) } returnsMany listOf(
            ApiResult.Failure(ApiFailure.Unauthorized),
            ApiResult.Failure(ApiFailure.Validation(emptyMap())),
        )
        compose.setContent { SignInScreen(SignInViewModel(repository)) }

        submit("nobody", "secret")
        compose.onNodeWithText(SIGN_IN_FAILED_MESSAGE).assertIsDisplayed()
        assertEquals("", editableText(PASSWORD_TAG))

        compose.onNodeWithTag(PASSWORD_TAG).performTextInput("other")
        compose.onNodeWithTag(SIGN_IN_TAG).performClick()
        compose.waitForIdle()
        compose.onNodeWithText(SIGN_IN_FAILED_MESSAGE).assertIsDisplayed()
        assertEquals("", editableText(PASSWORD_TAG))
    }

    @Test
    fun SignIn_networkFailure_showsRetry() {
        coEvery { repository.signIn(any(), any()) } returns ApiResult.Failure(ApiFailure.Unavailable)
        compose.setContent { SignInScreen(SignInViewModel(repository)) }

        submit("admin", "secret")
        compose.onNodeWithText("Something went wrong. Try again.").assertIsDisplayed()
        compose.onNodeWithText("Retry").assertIsDisplayed().performClick()
        compose.waitForIdle()
        coVerify(exactly = 2) { repository.signIn("admin", "secret") }
    }
}
