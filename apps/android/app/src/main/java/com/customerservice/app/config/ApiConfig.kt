package com.customerservice.app.config

sealed interface ApiConfigResult {
    data class Valid(val baseUrl: String) : ApiConfigResult
    data class Invalid(val reason: String) : ApiConfigResult
}

/**
 * HTTPS-only check on the configured API base URL. The value must start with exactly `https://`:
 * no trimming and no case folding, so `HTTPS://...` and ` https://...` are rejected.
 */
object ApiConfig {
    private const val REQUIRED_PREFIX = "https://"

    fun check(baseUrl: String): ApiConfigResult =
        if (baseUrl.startsWith(REQUIRED_PREFIX)) {
            ApiConfigResult.Valid(baseUrl)
        } else {
            ApiConfigResult.Invalid("API_BASE_URL must start with $REQUIRED_PREFIX")
        }
}
