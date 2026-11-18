package com.customerservice.app.ui

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
}
