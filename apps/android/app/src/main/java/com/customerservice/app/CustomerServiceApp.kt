package com.customerservice.app

import android.app.Application

class CustomerServiceApp : Application() {
    // Process-wide: a second DataStore on the session file would throw after an activity recreation.
    val container: AppContainer by lazy { AppContainer(this, BuildConfig.API_BASE_URL, BuildConfig.DEBUG) }
}
