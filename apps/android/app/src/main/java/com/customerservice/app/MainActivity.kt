package com.customerservice.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.lifecycle.lifecycleScope
import com.customerservice.app.config.ApiConfig
import com.customerservice.app.config.ApiConfigResult
import com.customerservice.app.ui.AppHost
import kotlinx.coroutines.launch

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val app = application as CustomerServiceApp
        if (ApiConfig.check(BuildConfig.API_BASE_URL) is ApiConfigResult.Valid) {
            // start() only acts while the state is Launching, so running it on every recreation is harmless.
            lifecycleScope.launch { app.container.sessionRepository.start() }
        }
        setContent {
            MaterialTheme {
                Surface { AppHost(BuildConfig.API_BASE_URL) { app.container } }
            }
        }
    }
}
