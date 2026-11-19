package com.customerservice.app.data.net

import com.customerservice.app.data.session.SessionStore
import kotlinx.coroutines.runBlocking
import okhttp3.Interceptor
import okhttp3.Response

class AuthInterceptor(private val store: SessionStore) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()
        if (request.url.encodedPath == LOGIN_PATH) return chain.proceed(request)
        // OkHttp interceptors are blocking, so bridge to the suspend read.
        val token = runBlocking { store.awaitToken() } ?: return chain.proceed(request)
        return chain.proceed(request.newBuilder().header("Authorization", "Bearer $token").build())
    }

    private companion object {
        const val LOGIN_PATH = "/api/auth/login"
    }
}
