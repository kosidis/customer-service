package com.customerservice.app

import android.content.Context
import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.datastore.preferences.preferencesDataStoreFile
import androidx.test.platform.app.InstrumentationRegistry
import com.customerservice.app.data.session.SESSION_DATASTORE_NAME
import com.customerservice.app.data.session.SessionStore
import com.customerservice.app.data.session.TokenCipher
import com.customerservice.app.ui.AppState
import java.io.File
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancelAndJoin
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.job
import kotlinx.coroutines.withTimeout
import org.junit.Assume.assumeTrue

/**
 * Shared helpers for the on-device tests. They run against the Compose stack behind the local TLS front
 * (`https://10.0.2.2:8443`, the debug API_BASE_URL) and need a real user, passed as instrumentation arguments:
 *
 *   -Pandroid.testInstrumentationRunnerArguments.testUsername=<username>
 *   -Pandroid.testInstrumentationRunnerArguments.testPassword=<password>
 *
 * The user must not have `mustChangePassword` set. Credentials are never logged or stored in the repo.
 */
object DeviceTestSupport {
    const val ARG_USERNAME = "testUsername"
    const val ARG_PASSWORD = "testPassword"

    val context: Context get() = InstrumentationRegistry.getInstrumentation().targetContext

    /** Skips (does not fail) a test that needs credentials when they were not supplied. */
    fun credentials(): Pair<String, String> {
        val args = InstrumentationRegistry.getArguments()
        val username = args.getString(ARG_USERNAME)
        val password = args.getString(ARG_PASSWORD)
        assumeTrue(
            "pass $ARG_USERNAME and $ARG_PASSWORD as instrumentation arguments to run this test",
            !username.isNullOrEmpty() && !password.isNullOrEmpty(),
        )
        return username!! to password!!
    }

    fun newContainer(baseUrl: String = BuildConfig.API_BASE_URL): AppContainer =
        AppContainer(context, baseUrl, debug = true)

    /** The DataStore file `AppContainer` writes the session to. */
    fun sessionFile(): File = context.preferencesDataStoreFile(SESSION_DATASTORE_NAME)

    /** Starts every test from a signed-out disk state. The Keystore key is left alone: it is production's alias. */
    fun deleteSessionFile() {
        sessionFile().delete()
    }

    /** Waits for the start-up check to settle on a final state. */
    suspend fun AppContainer.settledState(): AppState = withTimeout(30_000) {
        sessionRepository.state.first { it !is AppState.Launching && it !is AppState.Checking }
    }

    /**
     * Reads the stored token the way production does (same file, same Keystore key) through a separate store.
     * Only call while no container holds the file open.
     */
    suspend fun readStoredToken(): String? {
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        try {
            val store = SessionStore(
                PreferenceDataStoreFactory.create(scope = scope, produceFile = { sessionFile() }),
                TokenCipher(TokenCipher::androidKeystoreKey),
            )
            return store.awaitToken()
        } finally {
            scope.coroutineContext.job.cancelAndJoin()
        }
    }
}
