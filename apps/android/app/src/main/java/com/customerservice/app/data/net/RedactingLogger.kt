package com.customerservice.app.data.net

import okhttp3.Interceptor
import okhttp3.Response

/** Logs method, path and status only: never bodies, headers or the query string. */
class RedactingLogger(private val log: (String) -> Unit) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()
        val path = request.url.encodedPath
        log("--> ${request.method} $path")
        val response = chain.proceed(request)
        log("<-- ${response.code} $path")
        return response
    }
}
