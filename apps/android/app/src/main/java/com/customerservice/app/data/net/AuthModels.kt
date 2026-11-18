package com.customerservice.app.data.net

import kotlinx.serialization.Serializable

@Serializable
data class LoginRequest(val username: String, val password: String)

/** `role` is the API's string enum: "Viewer" or "Admin". */
@Serializable
data class LoginResponse(val token: String, val username: String, val role: String, val mustChangePassword: Boolean)

@Serializable
data class MeResponse(val username: String, val role: String, val mustChangePassword: Boolean)

@Serializable
data class ChangePasswordRequest(val currentPassword: String, val newPassword: String)
