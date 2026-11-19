package com.customerservice.app.data.session

sealed interface SessionEvent {
    data object Expired : SessionEvent
}
