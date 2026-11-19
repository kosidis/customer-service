package com.customerservice.app.data.session

import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import com.customerservice.app.data.net.AuthApi
import com.customerservice.app.data.net.ChangePasswordRequest
import com.customerservice.app.data.net.LoginRequest
import com.customerservice.app.data.net.LoginResponse
import com.customerservice.app.data.net.MeResponse
import java.io.File
import javax.crypto.KeyGenerator
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.runTest
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder
import retrofit2.Response

@OptIn(ExperimentalCoroutinesApi::class)
class AuthLoggingTest {
    @get:Rule
    val tmp = TemporaryFolder()

    private val marker = "ZQX-MARKER-7431"
    private val ioScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val lines = mutableListOf<String>()
    private lateinit var store: SessionStore

    private class StubApi(private val login: () -> Response<LoginResponse>) : AuthApi {
        override suspend fun login(request: LoginRequest) = login()
        override suspend fun logout(): Response<Unit> = Response.success(null)
        override suspend fun me(): Response<MeResponse> = Response.success(MeResponse("u", "Admin", false))
        override suspend fun changePassword(request: ChangePasswordRequest): Response<Unit> = Response.success(null)
    }

    @Before
    fun setUp() {
        val file = File(tmp.newFolder(), "session.preferences_pb")
        val key = KeyGenerator.getInstance("AES").apply { init(256) }.generateKey()
        store = SessionStore(PreferenceDataStoreFactory.create(scope = ioScope, produceFile = { file }), TokenCipher { key })
    }

    @After
    fun tearDown() {
        ioScope.cancel()
    }

    private fun TestScope.scope() = CoroutineScope(backgroundScope.coroutineContext + UnconfinedTestDispatcher(testScheduler))

    private fun TestScope.loggingRepo(api: AuthApi) =
        SessionRepository(api, store, MutableSharedFlow(extraBufferCapacity = 8), scope(), AuthLog { lines += it })

    private fun okLogin() = Response.success(LoginResponse("tok-$marker", "user-$marker", "Admin", false))

    @Test
    fun signIn_debugBuild_logsInfoWithoutUsernameOrToken() = runTest {
        val repo = loggingRepo(StubApi { okLogin() })

        repo.signIn("user-$marker", "pw-$marker")
        repo.signOut()

        assertTrue(lines.isNotEmpty())
        assertTrue(lines.none { it.contains(marker) || it.contains("Admin") })
    }

    @Test
    fun signIn_failed_logsFailureKindNotBody() = runTest {
        val repo = loggingRepo(StubApi { Response.error(401, "secret-$marker".toResponseBody()) })

        repo.signIn("user-$marker", "pw-$marker")

        assertEquals(listOf("Sign-in failed: Unauthorized"), lines)
    }

    @Test
    fun signIn_defaultNoOpLogger_logsNothing() = runTest {
        val repo = SessionRepository(StubApi { okLogin() }, store, MutableSharedFlow(extraBufferCapacity = 8), scope())

        repo.signIn("u", "p")
        repo.signOut()

        assertTrue(lines.isEmpty())
    }
}
