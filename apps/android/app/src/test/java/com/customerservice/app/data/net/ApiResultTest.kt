package com.customerservice.app.data.net

import java.io.IOException
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody
import okhttp3.ResponseBody.Companion.toResponseBody
import okio.BufferedSource
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

    @Test
    fun toApiResult_400WithNestedJsonPath_usesLastSegment() {
        val body = """{"errors":{"${'$'}.customer.email":["bad"]}}"""
        assertEquals(
            ApiFailure.Validation(mapOf("email" to listOf("bad"))),
            failureOf(error(400, body)),
        )
    }

    @Test
    fun toApiResult_400WithIndexedKey_usesLastSegment() {
        val body = """{"errors":{"Items[0].Name":["bad"]}}"""
        assertEquals(
            ApiFailure.Validation(mapOf("name" to listOf("bad"))),
            failureOf(error(400, body)),
        )
    }

    @Test
    fun toApiResult_400WithBareDollarKey_keepsKeyInsteadOfEmpty() {
        val body = """{"errors":{"${'$'}":["bad"]}}"""
        assertEquals(
            ApiFailure.Validation(mapOf("${'$'}" to listOf("bad"))),
            failureOf(error(400, body)),
        )
    }

    @Test
    fun toApiResult_other4xx_returnUnavailable() {
        listOf(405, 422, 429).forEach { code ->
            assertEquals("status $code", ApiFailure.Unavailable, failureOf(error(code)))
        }
    }

    @Test
    fun toApiResult_successWithNullBody_returnsUnavailable() {
        val response: Response<String> = Response.success(null as String?)
        assertEquals(ApiResult.Failure(ApiFailure.Unavailable), response.toApiResult())
    }

    @Test
    fun toApiResult_unitSuccessWithNullBody_returnsOkUnit() {
        val response: Response<Unit> = Response.success(null as Unit?)
        assertEquals(ApiResult.Ok(Unit), response.toUnitResult())
    }

    @Test
    fun toApiResult_unitFailure_mapsStatus() {
        val response: Response<Unit> = Response.error(401, "".toResponseBody(null))
        assertEquals(ApiResult.Failure(ApiFailure.Unauthorized), response.toUnitResult())
    }

    @Test
    fun apiCall_unitSuccess_returnsOkUnit() {
        val result = apiCallUnit { Response.success(null as Unit?) }
        assertEquals(ApiResult.Ok(Unit), result)
    }

    @Test
    fun apiCall_unitIoException_returnsUnavailable() {
        val result = apiCallUnit { throw IOException("offline") }
        assertEquals(ApiResult.Failure(ApiFailure.Unavailable), result)
    }

    @Test
    fun toApiResult_400WithInjectedParser_usesParserOutput() {
        var received: String? = null
        val response = error(400, "raw body")
        val result = response.toApiResult { body ->
            received = body
            mapOf("custom" to listOf("m"))
        }
        assertEquals("raw body", received)
        assertEquals(ApiResult.Failure(ApiFailure.Validation(mapOf("custom" to listOf("m")))), result)
    }

    @Test
    fun toApiResult_400WhenReadingErrorBodyThrowsIoException_returnsEmptyValidation() {
        val broken = object : ResponseBody() {
            override fun contentType() = null
            override fun contentLength() = -1L
            override fun source(): BufferedSource = throw IOException("read failed")
        }
        val response: Response<String> = Response.error(400, broken)
        assertEquals(
            ApiResult.Failure(ApiFailure.Validation(emptyMap())),
            response.toApiResult(),
        )
    }
}
