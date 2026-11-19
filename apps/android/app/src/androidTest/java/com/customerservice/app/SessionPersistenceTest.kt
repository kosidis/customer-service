package com.customerservice.app

import com.customerservice.app.DeviceTestSupport.settledState
import com.customerservice.app.data.net.ApiResult
import com.customerservice.app.ui.AppState
import kotlinx.coroutines.runBlocking
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

/**
 * Real Keystore, real DataStore file, real TLS. A "process restart" is simulated by closing the container
 * (cancelling its scope, which releases the DataStore file) and building a fresh one over the same file and
 * Keystore alias, then running the start-up check. It does not kill the process, so it does not cover
 * process-level state outside the container (statics, the Application object, the activity).
 */
class SessionPersistenceTest {
    private val containers = mutableListOf<AppContainer>()

    @Before fun cleanSlate() = DeviceTestSupport.deleteSessionFile()

    @After fun tearDown() {
        runBlocking { containers.forEach { it.close() } }
        DeviceTestSupport.deleteSessionFile()
    }

    private fun container() = DeviceTestSupport.newContainer().also { containers += it }

    private suspend fun restart(old: AppContainer): AppContainer {
        old.close()
        return container().also { it.start() }
    }

    private suspend fun signIn(c: AppContainer) {
        val (user, password) = DeviceTestSupport.credentials()
        assertTrue(c.sessionRepository.signIn(user, password) is ApiResult.Ok)
        assertTrue(c.sessionRepository.state.value is AppState.SignedIn)
    }

    @Test
    fun signIn_thenRestartProcess_staysSignedIn() = runBlocking {
        DeviceTestSupport.credentials()
        val first = container()
        signIn(first)

        val second = restart(first)

        assertTrue(second.settledState() is AppState.SignedIn)
    }

    @Test
    fun signOut_thenRestartProcess_staysOut() = runBlocking {
        DeviceTestSupport.credentials()
        val first = container()
        signIn(first)
        first.sessionRepository.signOut()
        assertEquals(AppState.SignedOut, first.sessionRepository.state.value)
        first.close()

        val second = container().also { it.start() }

        assertEquals(AppState.SignedOut, second.settledState())
        second.close()
        assertNull(DeviceTestSupport.readStoredToken())
    }
}
