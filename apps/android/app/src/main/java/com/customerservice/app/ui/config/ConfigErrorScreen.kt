package com.customerservice.app.ui.config

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp

const val CONFIG_ERROR_TITLE = "App configuration error"

@Composable
fun ConfigErrorScreen() {
    Column(
        modifier = Modifier.fillMaxSize().padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp, Alignment.CenterVertically),
    ) {
        Text(CONFIG_ERROR_TITLE, style = MaterialTheme.typography.headlineMedium)
        Text("This build is not set up to reach the server over HTTPS, so the app cannot connect. Install a correctly configured build.")
    }
}
