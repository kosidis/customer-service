package com.customerservice.app.data.net

import com.customerservice.app.data.session.SessionEvent
import com.customerservice.app.data.session.SessionStore
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.runBlocking
import okhttp3.Interceptor
import okhttp3.Response

/** Install after [AuthInterceptor]: a 401 only expires the session when the request carried a token. */
class UnauthorizedInterceptor(
    private val store: SessionStore,
    private val events: MutableSharedFlow<SessionEvent>,
) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val response = chain.proceed(chain.request())
        if (response.code == 401 && response.request.header("Authorization") != null) {
            runBlocking { store.clear() }
            // tryEmit never blocks the network thread; the flow needs buffer capacity so the event is not dropped.
            events.tryEmit(SessionEvent.Expired)
        }
        return response
    }
}
