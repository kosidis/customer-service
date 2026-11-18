package com.customerservice.app.data.net

import java.io.IOException
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.contentOrNull
import retrofit2.Response

sealed interface ApiFailure {
    data object Unauthorized : ApiFailure
    data object Forbidden : ApiFailure

    /** Field-error keys are lower-cased; for JSON-path keys such as `$.NewPassword` only the last segment is kept. */
    data class Validation(val fieldErrors: Map<String, List<String>>) : ApiFailure
    data object NotFound : ApiFailure
    data object Conflict : ApiFailure
    data object Unavailable : ApiFailure
}

sealed interface ApiResult<out T> {
    data class Ok<T>(val value: T) : ApiResult<T>
    data class Failure(val failure: ApiFailure) : ApiResult<Nothing>
}

private val json = Json { ignoreUnknownKeys = true }

/**
 * Parses a ProblemDetails-style 400 body (`{"errors":{"key":["message"]}}`) into normalised field errors.
 * Returns an empty map when the body is missing, malformed or has no `errors` object.
 */
fun parseFieldErrors(body: String): Map<String, List<String>> {
    val errors = try {
        (json.parseToJsonElement(body) as? JsonObject)?.get("errors") as? JsonObject
    } catch (_: IllegalArgumentException) {
        null
    } ?: return emptyMap()

    val result = LinkedHashMap<String, List<String>>()
    for ((key, value) in errors) {
        val messages = (value as? JsonArray)
            ?.mapNotNull { (it as? JsonPrimitive)?.contentOrNull }
            .orEmpty()
        val normalised = key.substringAfterLast('.').lowercase()
        result[normalised] = result[normalised].orEmpty() + messages
    }
    return result
}

fun <T> Response<T>.toApiResult(
    parseErrors: (String) -> Map<String, List<String>> = ::parseFieldErrors,
): ApiResult<T> {
    if (isSuccessful) {
        @Suppress("UNCHECKED_CAST")
        return ApiResult.Ok(body() as T)
    }
    val failure = when (code()) {
        400 -> ApiFailure.Validation(
            try {
                parseErrors(errorBody()?.string().orEmpty())
            } catch (_: IOException) {
                emptyMap()
            },
        )
        401 -> ApiFailure.Unauthorized
        403 -> ApiFailure.Forbidden
        404 -> ApiFailure.NotFound
        409 -> ApiFailure.Conflict
        else -> ApiFailure.Unavailable
    }
    return ApiResult.Failure(failure)
}

/** Runs a Retrofit call, mapping a network failure ([IOException]) to [ApiFailure.Unavailable]. */
inline fun <T> apiCall(block: () -> Response<T>): ApiResult<T> =
    try {
        block().toApiResult()
    } catch (_: IOException) {
        ApiResult.Failure(ApiFailure.Unavailable)
    }
