package com.customerservice.app.ui.password

import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.assert
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
import io.mockk.mockk
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class ChangePasswordScreenTest {
    @get:Rule
    val compose = createComposeRule()

    private val repository = mockk<SessionRepository>()

    private fun editableText(tag: String): String =
        compose.onNodeWithTag(tag).fetchSemanticsNode().config[SemanticsProperties.EditableText].text

    @Test
    fun ChangePassword_400_showsFieldErrorsAndKeepsTypedValues() {
        coEvery { repository.changePassword("old-pass", "weak") } returns ApiResult.Failure(
            ApiFailure.Validation(
                mapOf(
                    "currentpassword" to listOf("Current password is incorrect."),
                    "newpassword" to listOf("New password is too short."),
                ),
            ),
        )
        val viewModel = ChangePasswordViewModel(repository)
        compose.setContent { ChangePasswordScreen(viewModel, onSignOut = {}) }

        compose.onNodeWithTag(CURRENT_PASSWORD_TAG).performTextInput("old-pass")
        compose.onNodeWithTag(NEW_PASSWORD_TAG).performTextInput("weak")
        compose.onNodeWithTag(SUBMIT_TAG).performClick()
        compose.waitForIdle()

        compose.onNodeWithText("Current password is incorrect.").assertIsDisplayed()
        compose.onNodeWithText("New password is too short.").assertIsDisplayed()
        assertEquals("old-pass", viewModel.state.value.currentPassword)
        assertEquals("weak", viewModel.state.value.newPassword)
        // The semantic text of a password field is the masked text, one bullet per typed character.
        assertEquals(8, editableText(CURRENT_PASSWORD_TAG).length)
        assertEquals(4, editableText(NEW_PASSWORD_TAG).length)
    }

    @Test
    fun ChangePassword_passwordFieldsUsePasswordTransformation() {
        compose.setContent { ChangePasswordScreen(ChangePasswordViewModel(repository), onSignOut = {}) }

        val isPassword = SemanticsMatcher.keyIsDefined(SemanticsProperties.Password)
        compose.onNodeWithTag(CURRENT_PASSWORD_TAG).assert(isPassword)
        compose.onNodeWithTag(NEW_PASSWORD_TAG).assert(isPassword)
    }
}
