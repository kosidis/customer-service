package com.customerservice.app.data.session

import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import org.junit.Assert.assertEquals
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
}
