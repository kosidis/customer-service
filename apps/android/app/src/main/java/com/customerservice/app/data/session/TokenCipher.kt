package com.customerservice.app.data.session

import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

data class Encrypted(val iv: ByteArray, val cipherText: ByteArray) {
    override fun equals(other: Any?): Boolean =
        other is Encrypted && iv.contentEquals(other.iv) && cipherText.contentEquals(other.cipherText)

    override fun hashCode(): Int = 31 * iv.contentHashCode() + cipherText.contentHashCode()
}

/** AES-256/GCM. The key comes from [keyProvider] so JVM tests can inject one; production uses [androidKeystoreKey]. */
class TokenCipher(private val keyProvider: () -> SecretKey) {
    fun encrypt(plain: String): Encrypted {
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.ENCRYPT_MODE, keyProvider())
        return Encrypted(cipher.iv, cipher.doFinal(plain.toByteArray(Charsets.UTF_8)))
    }

    /** Returns null when the data cannot be decrypted (lost or rotated key, corrupt data). */
    fun decrypt(e: Encrypted): String? = try {
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.DECRYPT_MODE, keyProvider(), GCMParameterSpec(TAG_BITS, e.iv))
        String(cipher.doFinal(e.cipherText), Charsets.UTF_8)
    } catch (_: Exception) {
        null
    }

    companion object {
        private const val TRANSFORMATION = "AES/GCM/NoPadding"
        private const val TAG_BITS = 128
        private const val KEYSTORE = "AndroidKeyStore"
        private const val KEY_ALIAS = "customer_service_session_key"

        /** Production key provider: a non-exportable AES-256 key in the Android Keystore, created on first use. */
        fun androidKeystoreKey(): SecretKey {
            val keyStore = KeyStore.getInstance(KEYSTORE).apply { load(null) }
            (keyStore.getKey(KEY_ALIAS, null) as? SecretKey)?.let { return it }
            val generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, KEYSTORE)
            generator.init(
                KeyGenParameterSpec.Builder(
                    KEY_ALIAS,
                    KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT,
                )
                    .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                    .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                    .setKeySize(256)
                    .build(),
            )
            return generator.generateKey()
        }
    }
}
