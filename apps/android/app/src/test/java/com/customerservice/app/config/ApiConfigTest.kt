package com.customerservice.app.config

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class ApiConfigTest {

    @Test
    fun check_httpUrl_isInvalid() {
        assertTrue(ApiConfig.check("http://api.example.com") is ApiConfigResult.Invalid)
    }

    @Test
    fun check_httpsUrl_isValid() {
        val result = ApiConfig.check("https://api.example.com")

        assertEquals(ApiConfigResult.Valid("https://api.example.com"), result)
    }

    @Test
    fun check_upperCaseScheme_isInvalid() {
        assertTrue(ApiConfig.check("HTTPS://api.example.com") is ApiConfigResult.Invalid)
    }

    @Test
    fun check_leadingWhitespace_isInvalid() {
        assertTrue(ApiConfig.check(" https://api.example.com") is ApiConfigResult.Invalid)
    }

    @Test
    fun check_blank_isInvalid() {
        assertTrue(ApiConfig.check("   ") is ApiConfigResult.Invalid)
        assertTrue(ApiConfig.check("") is ApiConfigResult.Invalid)
    }
}
