package com.customerservice.app.ui

import androidx.compose.runtime.mutableStateOf
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import com.customerservice.app.data.net.ApiFailure
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class ErrorMessageTest {
    @get:Rule
    val compose = createComposeRule()

    @Test
    fun errorMessage_unavailable_showsFixedText() {
        compose.setContent { ErrorMessage(ApiFailure.Unavailable, onRetry = null) }
        compose.onNodeWithText("Something went wrong. Try again.").assertIsDisplayed()
    }

    @Test
    fun errorMessage_nullOnRetry_hidesRetryButton() {
        compose.setContent { ErrorMessage(ApiFailure.Unavailable, onRetry = null) }
        compose.onNodeWithText("Retry").assertDoesNotExist()
    }

    @Test
    fun errorMessage_nonNullOnRetry_showsRetryButtonAndInvokesCallback() {
        var clicks = 0
        compose.setContent { ErrorMessage(ApiFailure.Unavailable, onRetry = { clicks++ }) }
        compose.onNodeWithText("Retry").assertIsDisplayed().performClick()
        assertEquals(1, clicks)
    }

    @Test
    fun errorMessage_validationWithoutFieldErrors_showsGenericText() {
        compose.setContent { ErrorMessage(ApiFailure.Validation(emptyMap()), onRetry = null) }
        compose.onNodeWithText("Something went wrong. Try again.").assertIsDisplayed()
    }

    @Test
    fun errorMessage_everyFailureVariant_showsItsPinnedCopy() {
        val expected = listOf(
            ApiFailure.Unavailable to "Something went wrong. Try again.",
            ApiFailure.Unauthorized to "Your session has ended. Sign in again.",
            ApiFailure.Forbidden to "You do not have permission to do that.",
            ApiFailure.NotFound to "We could not find that.",
            ApiFailure.Conflict to "That conflicts with existing data.",
            ApiFailure.Validation(mapOf("email" to listOf("bad"))) to "Check the highlighted fields and try again.",
        )
        val shown = mutableStateOf<ApiFailure>(expected.first().first)
        compose.setContent { ErrorMessage(shown.value, onRetry = null) }
        expected.forEach { (failure, text) ->
            shown.value = failure
            compose.waitForIdle()
            compose.onNodeWithText(text).assertIsDisplayed()
        }
    }
}
