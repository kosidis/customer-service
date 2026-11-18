package com.customerservice.app.data.net

import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.runBlocking
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory

class UnparsableResponseTest {
    private val json = Json { ignoreUnknownKeys = true }
    private val server = MockWebServer()
    private lateinit var api: AuthApi

    @Before
    fun setUp() {
        server.start()
        api = Retrofit.Builder()
            .baseUrl(server.url("/"))
            .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
            .build()
            .create(AuthApi::class.java)
    }

    @After
    fun tearDown() = server.shutdown()

    private fun respond(body: String) = server.enqueue(MockResponse().setResponseCode(200).setBody(body))

    @Test
    fun me_200NonJson_returnsUnavailable() {
        respond("<html>proxy</html>")

        val result = runBlocking { apiCall { api.me() } }

        assertEquals(ApiResult.Failure(ApiFailure.Unavailable), result)
    }

    @Test
    fun me_200MissingField_returnsUnavailable() {
        respond("""{"username":"u"}""")

        val result = runBlocking { apiCall { api.me() } }

        assertEquals(ApiResult.Failure(ApiFailure.Unavailable), result)
    }

    @Test
    fun login_200MissingField_returnsUnavailable() {
        respond("""{"username":"u","role":"Admin"}""")

        val result = runBlocking { apiCall { api.login(LoginRequest("u", "p")) } }

        assertEquals(ApiResult.Failure(ApiFailure.Unavailable), result)
    }

    @Test
    fun apiCallUnit_unexpectedException_returnsUnavailable() {
        val result = apiCallUnit { throw IllegalStateException("boom") }

        assertEquals(ApiResult.Failure(ApiFailure.Unavailable), result)
    }

    @Test(expected = CancellationException::class)
    fun apiCall_cancellation_isRethrown() {
        apiCall<String> { throw CancellationException("cancelled") }
    }
}
