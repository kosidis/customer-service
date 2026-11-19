package com.customerservice.app.data.session

import java.util.Base64
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.emptyPreferences
import androidx.datastore.preferences.core.stringPreferencesKey
import java.io.IOException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.flow.flowOn
import kotlinx.coroutines.withContext
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

    // An unreadable or corrupt store reads as signed out, never as a crash.
    private val preferences: Flow<Preferences> = dataStore.data.catch { e ->
        if (e is IOException) emit(emptyPreferences()) else throw e
    }

    val token: Flow<String?> = preferences.map { decode(it) }.flowOn(Dispatchers.IO)

    suspend fun awaitToken(): String? = mutex.withLock { readLocked() }

    private suspend fun readLocked(): String? =
        if (!loaded) {
            val prefs = preferences.first()
            val value = withContext(Dispatchers.IO) { decode(prefs) }
            // Present-but-undecodable data may be a transient Keystore failure: do not cache it.
            if (value != null || !hasStored(prefs)) {
                cached = value
                loaded = true
            }
            value
        } else {
            cached
        }

    suspend fun save(token: String) = mutex.withLock {
        val encrypted = withContext(Dispatchers.IO) { cipher.encrypt(token) }
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

    /** Clears only when the stored token still equals [token]; returns whether it cleared. */
    suspend fun clearIf(token: String): Boolean = mutex.withLock {
        if (readLocked() != token) return@withLock false
        dataStore.edit {
            it.remove(IV)
            it.remove(CIPHER_TEXT)
        }
        cached = null
        loaded = true
        true
    }

    private fun hasStored(prefs: Preferences): Boolean = prefs[IV] != null || prefs[CIPHER_TEXT] != null

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
