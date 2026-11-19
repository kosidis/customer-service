package com.customerservice.app.data.net

import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class RedactingLoggerTest {
    @Test
    fun RedactingLogger_neverLogsBodyOrAuthorizationValue() {
        val marker = "SECRET-MARKER-9f3a"
        val lines = mutableListOf<String>()
        val server = MockWebServer()
        server.start()
        try {
            server.enqueue(MockResponse().setResponseCode(200).setBody("""{"token":"$marker"}"""))
            val client = OkHttpClient.Builder()
                .addInterceptor { it.proceed(it.request().newBuilder().header("Authorization", "Bearer $marker").build()) }
                .addInterceptor(RedactingLogger { lines.add(it) })
                .build()
            val request = Request.Builder()
                .url(server.url("/api/auth/login?password=$marker"))
                .post("""{"password":"$marker"}""".toRequestBody())
                .build()

            client.newCall(request).execute().use { it.body.string() }
        } finally {
            server.shutdown()
        }

        assertTrue(lines.isNotEmpty())
        assertFalse(lines.any { it.contains(marker) })
        assertEquals(listOf("--> POST /api/auth/login", "<-- 200 /api/auth/login"), lines)
    }
}
