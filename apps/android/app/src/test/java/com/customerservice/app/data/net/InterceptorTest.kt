package com.customerservice.app.data.net

import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import app.cash.turbine.test
import com.customerservice.app.data.session.SessionEvent
import com.customerservice.app.data.session.SessionStore
import com.customerservice.app.data.session.TokenCipher
import javax.crypto.KeyGenerator
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder

class InterceptorTest {
    @get:Rule
    val tmp = TemporaryFolder()

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val server = MockWebServer()
    private lateinit var store: SessionStore

    // Buffered so the interceptor's tryEmit never drops an event or blocks when nobody collects yet.
    private val events = MutableSharedFlow<SessionEvent>(extraBufferCapacity = 8)
    private lateinit var client: OkHttpClient

    @Before
    fun setUp() {
        val key = KeyGenerator.getInstance("AES").apply { init(256) }.generateKey()
        val dataStore = PreferenceDataStoreFactory.create(
            scope = scope,
            produceFile = { tmp.newFolder().resolve("s.preferences_pb") },
        )
        store = SessionStore(dataStore, TokenCipher { key })
        server.start()
        client = OkHttpClient.Builder()
            .addInterceptor(AuthInterceptor(store))
            .addInterceptor(UnauthorizedInterceptor(store, events))
            .build()
    }

    @After
    fun tearDown() {
        server.shutdown()
        scope.cancel()
    }

    private suspend fun get(path: String) = withContext(Dispatchers.IO) {
        client.newCall(Request.Builder().url(server.url(path)).build()).execute().close()
    }

    private suspend fun post(path: String) = withContext(Dispatchers.IO) {
        client.newCall(Request.Builder().url(server.url(path)).post("{}".toRequestBody()).build()).execute().close()
    }

    @Test
    fun AuthInterceptor_addsBearer_onMe() = runTest {
        store.save("tok-1")
        server.enqueue(MockResponse().setResponseCode(200).setBody("{}"))

        get("/api/auth/me")

        assertEquals("Bearer tok-1", server.takeRequest().getHeader("Authorization"))
    }

    @Test
    fun AuthInterceptor_skipsLogin() = runTest {
        store.save("tok-1")
        server.enqueue(MockResponse().setResponseCode(200).setBody("{}"))

        post("/api/auth/login")

        assertNull(server.takeRequest().getHeader("Authorization"))
    }

    @Test
    fun AuthInterceptor_noToken_addsNoHeader() = runTest {
        server.enqueue(MockResponse().setResponseCode(200).setBody("{}"))

        get("/api/auth/me")

        assertNull(server.takeRequest().getHeader("Authorization"))
    }

    @Test
    fun UnauthorizedInterceptor_401WithToken_clearsStoreAndEmitsExpired() = runTest {
        store.save("tok-1")
        server.enqueue(MockResponse().setResponseCode(401))

        events.test {
            get("/api/auth/me")

            assertEquals(SessionEvent.Expired, awaitItem())
        }
        assertNull(store.awaitToken())
    }

    @Test
    fun UnauthorizedInterceptor_403_leavesStore() = runTest {
        store.save("tok-1")
        server.enqueue(MockResponse().setResponseCode(403))

        events.test {
            get("/api/auth/me")

            expectNoEvents()
        }
        assertEquals("tok-1", store.awaitToken())
    }

    @Test
    fun UnauthorizedInterceptor_loginReturns401_doesNotEmit() = runTest {
        store.save("tok-1")
        server.enqueue(MockResponse().setResponseCode(401))

        events.test {
            post("/api/auth/login")

            expectNoEvents()
        }
        assertEquals("tok-1", store.awaitToken())
    }
}
