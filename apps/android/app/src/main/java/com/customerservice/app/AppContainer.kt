package com.customerservice.app

import android.content.Context
import android.util.Log
import androidx.datastore.core.handlers.ReplaceFileCorruptionHandler
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.core.emptyPreferences
import androidx.datastore.preferences.preferencesDataStoreFile
import com.customerservice.app.data.net.AuthApi
import com.customerservice.app.data.net.AuthInterceptor
import com.customerservice.app.data.net.RedactingLogger
import com.customerservice.app.data.net.UnauthorizedInterceptor
import com.customerservice.app.data.session.SESSION_DATASTORE_NAME
import com.customerservice.app.data.session.SessionEvent
import com.customerservice.app.data.session.SessionRepository
import com.customerservice.app.data.session.SessionStore
import com.customerservice.app.data.session.TokenCipher
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.launch
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory

/** Hand-written wiring, built once per process; only call it with a base URL that passed `ApiConfig.check`. */
class AppContainer(context: Context, baseUrl: String, debug: Boolean) {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    // replay = 1 keeps a 401 that UnauthorizedInterceptor tryEmits before the repository subscribes.
    private val events = MutableSharedFlow<SessionEvent>(replay = 1)

    private val store = SessionStore(
        PreferenceDataStoreFactory.create(
            corruptionHandler = ReplaceFileCorruptionHandler { emptyPreferences() },
            scope = scope,
            produceFile = { context.applicationContext.preferencesDataStoreFile(SESSION_DATASTORE_NAME) },
        ),
        TokenCipher(TokenCipher::androidKeystoreKey),
    )

    private val client = OkHttpClient.Builder()
        // Bounded so a sign-out over a dead network cannot hang: 10 s connect, 15 s read/write, 20 s whole call.
        .connectTimeout(10, TimeUnit.SECONDS)
        .readTimeout(15, TimeUnit.SECONDS)
        .writeTimeout(15, TimeUnit.SECONDS)
        .callTimeout(20, TimeUnit.SECONDS)
        .addInterceptor(AuthInterceptor(store))
        .addInterceptor(UnauthorizedInterceptor(store, events))
        .apply { if (debug) addInterceptor(RedactingLogger { Log.d("Http", it) }) }
        .build()

    private val json = Json { ignoreUnknownKeys = true }

    private val api: AuthApi = Retrofit.Builder()
        .baseUrl(if (baseUrl.endsWith("/")) baseUrl else "$baseUrl/")
        .client(client)
        .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
        .build()
        .create(AuthApi::class.java)

    val sessionRepository = SessionRepository(api, store, events, scope)

    private val started = AtomicBoolean(false)

    /** Runs the start-up session check once, in the process-lifetime scope so an activity recreation cannot cancel it. */
    fun start() {
        if (started.compareAndSet(false, true)) scope.launch { sessionRepository.start() }
    }
}
