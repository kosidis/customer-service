package com.customerservice.app.data.net

import java.io.IOException
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Test
import retrofit2.Response

class ApiResultTest {
    private fun error(code: Int, body: String = ""): Response<String> =
        Response.error(code, body.toResponseBody("application/json".toMediaType()))

    private fun failureOf(response: Response<String>): ApiFailure =
        (response.toApiResult() as ApiResult.Failure).failure

    @Test
    fun toApiResult_success_returnsOkWithBody() {
        assertEquals(ApiResult.Ok("hello"), Response.success("hello").toApiResult())
    }

    @Test
    fun toApiResult_401_returnsUnauthorized() {
        assertEquals(ApiFailure.Unauthorized, failureOf(error(401)))
    }

    @Test
    fun toApiResult_403_returnsForbidden() {
        assertEquals(ApiFailure.Forbidden, failureOf(error(403)))
    }

    @Test
    fun toApiResult_404_returnsNotFound() {
        assertEquals(ApiFailure.NotFound, failureOf(error(404)))
    }

    @Test
    fun toApiResult_409_returnsConflict() {
        assertEquals(ApiFailure.Conflict, failureOf(error(409)))
    }

    @Test
    fun toApiResult_500_returnsUnavailable() {
        assertEquals(ApiFailure.Unavailable, failureOf(error(500)))
    }

    @Test
    fun toApiResult_503_returnsUnavailable() {
        assertEquals(ApiFailure.Unavailable, failureOf(error(503)))
    }

    @Test
    fun toApiResult_400WithJsonPathKey_usesLastSegmentLowerCased() {
        val body = """{"errors":{"${'$'}.NewPassword":["x"]}}"""
        assertEquals(
            ApiFailure.Validation(mapOf("newpassword" to listOf("x"))),
            failureOf(error(400, body)),
        )
    }

    @Test
    fun toApiResult_400WithPlainKey_lowerCasesKey() {
        val body = """{"errors":{"CurrentPassword":["a","b"]}}"""
        assertEquals(
            ApiFailure.Validation(mapOf("currentpassword" to listOf("a", "b"))),
            failureOf(error(400, body)),
        )
    }

    @Test
    fun toApiResult_400WithMalformedBody_returnsValidationWithNoFieldErrors() {
        assertEquals(ApiFailure.Validation(emptyMap()), failureOf(error(400, "not json")))
    }

    @Test
    fun toApiResult_400WithoutErrors_returnsValidationWithNoFieldErrors() {
        assertEquals(ApiFailure.Validation(emptyMap()), failureOf(error(400, """{"title":"bad"}""")))
    }

    @Test
    fun apiCall_ioException_returnsUnavailable() {
        val result = apiCall<String> { throw IOException("offline") }
        assertEquals(ApiResult.Failure(ApiFailure.Unavailable), result)
    }

    @Test
    fun apiCall_success_returnsOk() {
        assertEquals(ApiResult.Ok("hi"), apiCall { Response.success("hi") })
    }
}
