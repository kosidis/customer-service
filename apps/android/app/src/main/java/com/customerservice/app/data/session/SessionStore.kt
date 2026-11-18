package com.customerservice.app.data.session

import java.util.Base64
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

/** DataStore file name; it is named in the backup exclusions (backup_rules.xml, data_extraction_rules.xml). */
const val SESSION_DATASTORE_NAME = "session"

/**
 * Keeps the session token encrypted at rest. An undecryptable stored token (for example a lost Keystore
 * key) reads as signed out, never as a crash. The token is never logged.
 */
class SessionStore(
    private val dataStore: DataStore<Preferences>,
    private val cipher: TokenCipher,
) {
    private val mutex = Mutex()
    private var loaded = false
    private var cached: String? = null

    val token: Flow<String?> = dataStore.data.map { decode(it) }

    suspend fun awaitToken(): String? = mutex.withLock {
        if (!loaded) {
            cached = decode(dataStore.data.first())
            loaded = true
        }
        cached
    }

    suspend fun save(token: String) = mutex.withLock {
        val encrypted = cipher.encrypt(token)
        dataStore.edit {
            it[IV] = Base64.getEncoder().encodeToString(encrypted.iv)
            it[CIPHER_TEXT] = Base64.getEncoder().encodeToString(encrypted.cipherText)
        }
        cached = token
        loaded = true
    }

    suspend fun clear() = mutex.withLock {
        dataStore.edit {
            it.remove(IV)
            it.remove(CIPHER_TEXT)
        }
        cached = null
        loaded = true
    }

    private fun decode(prefs: Preferences): String? {
        val iv = prefs[IV] ?: return null
        val text = prefs[CIPHER_TEXT] ?: return null
        return try {
            cipher.decrypt(Encrypted(Base64.getDecoder().decode(iv), Base64.getDecoder().decode(text)))
        } catch (_: IllegalArgumentException) {
            null
        }
    }

    private companion object {
        val IV = stringPreferencesKey("token_iv")
        val CIPHER_TEXT = stringPreferencesKey("token_ciphertext")
    }
}
