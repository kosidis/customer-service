package com.customerservice.app

import com.customerservice.app.DeviceTestSupport.settledState
import com.customerservice.app.data.net.ApiResult
import com.customerservice.app.ui.AppState
import kotlinx.coroutines.runBlocking
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

class ServerSideSessionTest {
    private val containers = mutableListOf<AppContainer>()

    @Before fun cleanSlate() = DeviceTestSupport.deleteSessionFile()

    @After fun tearDown() {
        runBlocking { containers.forEach { it.close() } }
        DeviceTestSupport.deleteSessionFile()
    }

    /**
     * The token lives only inside the app's encrypted store, so a second client learns it by reading the same
     * file with the same Keystore key while no container holds it (a restart), then ends that session through
     * the API's own `POST /api/auth/logout`. The restarted app's authenticated start-up call (`GET /api/auth/me`)
     * then gets a 401 and the app must go to SignedOut with the stored token cleared.
     */
    @Test
    fun sessionDeletedServerSide_nextCall_returnsToSignIn() = runBlocking {
        val (user, password) = DeviceTestSupport.credentials()
        val first = DeviceTestSupport.newContainer().also { containers += it }
        assertTrue(first.sessionRepository.signIn(user, password) is ApiResult.Ok)
        assertTrue(first.sessionRepository.state.value is AppState.SignedIn)
        first.close()

        val token = DeviceTestSupport.readStoredToken()
        assertNotNull(token)
        val code = OkHttpClient().newCall(
            Request.Builder()
                .url("${BuildConfig.API_BASE_URL}/api/auth/logout")
                .header("Authorization", "Bearer $token")
                .post(ByteArray(0).toRequestBody())
                .build(),
        ).execute().use { it.code }
        assertEquals(204, code)

        val second = DeviceTestSupport.newContainer().also { containers += it }
        second.start()

        assertEquals(AppState.SignedOut, second.settledState())
        second.close()
        assertNull(DeviceTestSupport.readStoredToken())
    }
}
