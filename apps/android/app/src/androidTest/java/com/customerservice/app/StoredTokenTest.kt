package com.customerservice.app

import android.security.NetworkSecurityPolicy
import android.util.Base64
import com.customerservice.app.data.net.ApiFailure
import com.customerservice.app.data.net.ApiResult
import java.io.IOException
import java.net.UnknownServiceException
import kotlinx.coroutines.runBlocking
import okhttp3.OkHttpClient
import okhttp3.Request
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

class StoredTokenTest {
    @Before fun cleanSlate() = DeviceTestSupport.deleteSessionFile()

    @After fun tearDown() = DeviceTestSupport.deleteSessionFile()

    @Test
    fun storedToken_inDataStoreFile_isNotPlaintext() = runBlocking {
        val (user, password) = DeviceTestSupport.credentials()
        val container = DeviceTestSupport.newContainer()
        try {
            assertTrue(container.sessionRepository.signIn(user, password) is ApiResult.Ok)
        } finally {
            container.close()
        }

        // Decrypt with production's key to learn the real token; it must be there, yet absent from the file bytes.
        val token = DeviceTestSupport.readStoredToken()
        assertNotNull("the sign-in should have stored a token", token)
        val bytes = DeviceTestSupport.sessionFile().readBytes()
        assertTrue("the DataStore file should not be empty", bytes.isNotEmpty())
        val haystack = String(bytes, Charsets.ISO_8859_1)
        assertFalse("raw token found in the DataStore file", haystack.contains(token!!))
        val b64 = Base64.encodeToString(token.toByteArray(Charsets.UTF_8), Base64.NO_WRAP)
        assertFalse("base64 token found in the DataStore file", haystack.contains(b64))
    }

    /**
     * Debug build: the manifest sets usesCleartextTraffic=false and the debug network security config sets
     * cleartextTrafficPermitted=false in its base-config, so cleartext is refused for every host, including the
     * dev host. This asserts that policy, a refused request through the app's own OkHttp stack, and a refused
     * request through a plain OkHttp client. The release config (src/main) has the same cleartext setting but
     * no dev CA; this debug-variant test does not exercise it.
     */
    @Test
    fun cleartextHttpRequest_fails() = runBlocking {
        val host = "10.0.2.2"
        assertFalse(
            "cleartext must not be permitted for $host",
            NetworkSecurityPolicy.getInstance().isCleartextTrafficPermitted(host),
        )

        // The app's real stack (OkHttp + Retrofit + interceptors) pointed at an http:// URL. ApiConfig refuses
        // such a URL before production builds a container, so this deliberately bypasses that check.
        val container = DeviceTestSupport.newContainer(baseUrl = "http://$host:8080")
        try {
            val result = container.sessionRepository.signIn("nobody", "nothing")
            assertEquals(ApiResult.Failure(ApiFailure.Unavailable), result)
        } finally {
            container.close()
        }

        val refused = try {
            OkHttpClient().newCall(Request.Builder().url("http://$host:8080/health/live").build()).execute().close()
            null
        } catch (e: IOException) {
            e
        }
        assertNotNull("a cleartext request must be refused", refused)
        assertTrue("expected UnknownServiceException, got $refused", refused is UnknownServiceException)
    }
}
