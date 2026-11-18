package com.customerservice.app.data.session

import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import java.io.File
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.test.runTest
import org.junit.After
import org.junit.Assert.assertFalse
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder

class SessionStoreTest {
    @get:Rule
    val tmp = TemporaryFolder()

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private lateinit var file: File

    @Before
    fun setUp() {
        file = File(tmp.newFolder(), "session.preferences_pb")
    }

    @After
    fun tearDown() {
        scope.cancel()
    }

    private fun newKey(): SecretKey = KeyGenerator.getInstance("AES").apply { init(256) }.generateKey()

    // One DataStore per file; stores with different keys share it, like a process restart with a lost key.
    private val dataStore by lazy { PreferenceDataStoreFactory.create(scope = scope, produceFile = { file }) }

    private fun store(key: SecretKey): SessionStore = SessionStore(dataStore, TokenCipher { key })

    @Test
    fun save_thenFileContents_doNotContainPlaintext() = runTest {
        val plain = "plain-token-value-12345"
        store(newKey()).save(plain)

        val bytes = file.readBytes()

        assertFalse(String(bytes, Charsets.ISO_8859_1).contains(plain))
    }

    @Test
    fun save_thenAwaitToken_returnsToken() = runTest {
        val key = newKey()
        store(key).save("tok")

        assertEquals("tok", store(key).awaitToken())
    }

    @Test
    fun awaitToken_decryptFailure_returnsNull() = runTest {
        store(newKey()).save("tok")

        assertNull(store(newKey()).awaitToken())
    }

    @Test
    fun clear_removesToken() = runTest {
        val key = newKey()
        val s = store(key)
        s.save("tok")

        s.clear()

        assertNull(s.awaitToken())
        assertNull(store(key).awaitToken())
    }
}
