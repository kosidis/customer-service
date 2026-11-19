package com.customerservice.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import com.customerservice.app.config.ApiConfig
import com.customerservice.app.config.ApiConfigResult
import com.customerservice.app.ui.AppHost

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val app = application as CustomerServiceApp
        if (ApiConfig.check(BuildConfig.API_BASE_URL) is ApiConfigResult.Valid) {
            app.container.start()
        }
        setContent {
            MaterialTheme {
                Surface { AppHost(BuildConfig.API_BASE_URL) { app.container } }
            }
        }
    }
}
