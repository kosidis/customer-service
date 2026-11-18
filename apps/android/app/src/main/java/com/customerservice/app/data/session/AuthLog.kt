package com.customerservice.app.data.session

/** Sink for auth-event lines. Callers pass fixed strings only: never usernames, passwords, tokens or bodies. */
fun interface AuthLog {
    fun info(message: String)

    companion object {
        val None = AuthLog { }
    }
}
