package com.customerservice.app.data.session

import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Test

class TokenCipherTest {
    private fun newKey(): SecretKey = KeyGenerator.getInstance("AES").apply { init(256) }.generateKey()

    @Test
    fun encryptDecrypt_roundTrip_returnsPlain() {
        val key = newKey()
        val cipher = TokenCipher { key }

        val encrypted = cipher.encrypt("a-secret-value")

        assertEquals("a-secret-value", cipher.decrypt(encrypted))
    }

    @Test
    fun decrypt_wrongKey_returnsNull() {
        val encrypted = TokenCipher { newKey() }.encrypt("a-secret-value")
        val otherKey = newKey()

        assertNull(TokenCipher { otherKey }.decrypt(encrypted))
    }

    @Test
    fun encrypt_twice_usesDistinctIvs() {
        val key = newKey()
        val cipher = TokenCipher { key }

        val first = cipher.encrypt("same")
        val second = cipher.encrypt("same")

        assertNotEquals(first.iv.toList(), second.iv.toList())
    }

    @Test
    fun decrypt_tamperedCipherText_returnsNull() {
        val key = newKey()
        val cipher = TokenCipher { key }
        val encrypted = cipher.encrypt("a-secret-value")
        val tampered = encrypted.cipherText.copyOf().also { it[0] = (it[0].toInt() xor 1).toByte() }

        assertNull(cipher.decrypt(Encrypted(encrypted.iv, tampered)))
    }

    @Test
    fun encrypt_keyProviderThrowsOnce_retriesAndSucceeds() {
        val key = newKey()
        var calls = 0
        val cipher = TokenCipher {
            if (calls++ == 0) throw java.security.GeneralSecurityException("corrupt entry")
            key
        }

        assertEquals("tok", cipher.decrypt(cipher.encrypt("tok")))
    }
}
