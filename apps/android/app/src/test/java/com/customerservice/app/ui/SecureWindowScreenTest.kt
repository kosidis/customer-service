package com.customerservice.app.ui

import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class SecureWindowScreenTest {
    @get:Rule
    val compose = createAndroidComposeRule<ComponentActivity>()

    private fun secure(): Boolean =
        compose.activity.window.attributes.flags and WindowManager.LayoutParams.FLAG_SECURE != 0

    @Test
    fun SecureWindow_setsAndClearsFlag() {
        var enabled by mutableStateOf(false)
        var shown by mutableStateOf(true)
        compose.setContent { if (shown) SecureWindow(enabled) }
        assertEquals(false, secure())

        enabled = true
        compose.waitForIdle()
        assertEquals(true, secure())

        enabled = false
        compose.waitForIdle()
        assertEquals(false, secure())

        enabled = true
        compose.waitForIdle()
        shown = false
        compose.waitForIdle()
        assertEquals(false, secure())
    }

    @Test
    fun SecureWindow_overlappingInstances_flagStaysUntilLastLeaves() {
        var first by mutableStateOf(true)
        var second by mutableStateOf(false)
        compose.setContent {
            if (first) SecureWindow(true)
            if (second) SecureWindow(true)
        }
        compose.waitForIdle()
        assertEquals(true, secure())

        second = true
        compose.waitForIdle()
        first = false
        compose.waitForIdle()
        assertEquals(true, secure())

        second = false
        compose.waitForIdle()
        assertEquals(false, secure())
    }
}
