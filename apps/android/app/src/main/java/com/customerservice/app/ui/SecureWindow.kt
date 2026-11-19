package com.customerservice.app.ui

import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import android.view.Window
import android.view.WindowManager
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.ui.platform.LocalContext
import java.util.WeakHashMap

@Composable
fun SecureWindow(enabled: Boolean) {
    val activity = LocalContext.current.findActivity()
    DisposableEffect(activity, enabled) {
        val window = activity?.window
        if (enabled && window != null) {
            secureCounts[window] = (secureCounts[window] ?: 0) + 1
            window.addFlags(WindowManager.LayoutParams.FLAG_SECURE)
        }
        onDispose {
            if (enabled && window != null) {
                val remaining = (secureCounts[window] ?: 1) - 1
                if (remaining <= 0) {
                    secureCounts.remove(window)
                    window.clearFlags(WindowManager.LayoutParams.FLAG_SECURE)
                } else {
                    secureCounts[window] = remaining
                }
            }
        }
    }
}

// Live enabled SecureWindows per window (main thread only): during a navigation transition the outgoing screen
// disposes after the incoming one has set the flag, and must not clear it.
private val secureCounts = WeakHashMap<Window, Int>()

private tailrec fun Context.findActivity(): Activity? = when (this) {
    is Activity -> this
    is ContextWrapper -> baseContext.findActivity()
    else -> null
}
