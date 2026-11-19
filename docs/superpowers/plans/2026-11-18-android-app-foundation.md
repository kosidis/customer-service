# Android app foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create `apps/android` with sign-in, sign-out, encrypted session storage, 401 handling, forced and voluntary password change, and HTTPS-only transport, plus the staging TLS exposure the phone needs (Sub-task #35, Story #31).

**Architecture:** A single-activity Compose app. `SessionRepository` owns the token (via `SessionStore` and `TokenCipher`) and the current user; OkHttp interceptors add the Bearer header and turn any 401 on an authenticated call into `SessionEvent.Expired`; a root `AppState` drives the `NavHost`. Three independent streams: the Android app, the Helm staging ingress, and the Compose TLS front with CI and docs.

**Tech Stack:** Kotlin, Jetpack Compose, Gradle with a version catalogue, Retrofit, OkHttp, kotlinx.serialization, DataStore (Preferences), Android Keystore; JUnit, MockK, Turbine, `MockWebServer`; Helm; nginx in Compose.

**Spec:** `governance/prds/prd-android-app/designs/tech-design-1.md` (ADR-1, ADR-2, ADR-3 in `governance/prds/prd-android-app/decisions/`). Story ACs: GitHub issue #31. Endpoint contracts: `governance/prds/prd-login-permissions/designs/tech-design-1.md`.

## Global Constraints

- Package `com.customerservice.app`, one `app` module, `minSdk` 26, current stable `targetSdk`.
- `API_BASE_URL` comes from `buildConfigField` per build type and must start with `https://`; otherwise a full-screen configuration error shows and no request is made (no crash).
- Manifest: `android:usesCleartextTraffic="false"`, `android:allowBackup="false"`, plus `data_extraction_rules.xml` and `fullBackupContent` exclusions for the DataStore file. Release trusts system authorities only; only `debug` adds `res/raw/dev_ca.pem`.
- Token: AES-256 GCM key in Android Keystore, ciphertext and IV in a Preferences DataStore; a decryption failure means no session. The password is never stored.
- Logging interceptor (debug only) logs method, path and status; never bodies; `Authorization` redacted. Release logs nothing from the auth layer.
- `login` returning 401 or 400 shows one fixed message and clears the password field; `login` never triggers `SessionEvent.Expired`.
- Sign-out calls `POST /api/auth/logout` and clears the store whatever the answer, then empties the back stack.
- Launch reads the token without a network call, then calls `GET /api/auth/me`; network or 5xx failure keeps the token and offers **Retry**; 401 clears the store.
- Shared failure copy: "Something went wrong. Try again." for `Unavailable`.
- Field-error keys normalised as in `apps/web/src/lib/api/http.ts`: case-insensitive, last segment of a JSON path such as `$.phoneNumber`.
- `values-prod.yaml` is not changed. Staging ingress path is `/api` only.
- No secrets, keys or certificates are committed; `dev-certs/` and `apps/android/app/src/debug/res/raw/dev_ca.pem` are gitignored.
- Test names `Method_Condition_Expected` style for JVM tests, to match the repo.

## Review Focus

1. Cold start with a token and no network: token kept, Retry shown, user not signed out.
2. Keystore key lost or invalidated: `SessionStore` returns no session instead of crashing.
3. Process death on the change-password screen: relaunch lands on `PasswordChange` again via `me`, never on the list.
4. A 401 arriving while sign-out is in flight: store cleared once, one navigation to sign-in, no loop.
5. `API_BASE_URL` of `HTTPS://...` or with leading whitespace: define the behaviour (reject unless it starts with exactly `https://`) and pin it in a test.

## File structure

| Path | Responsibility |
|---|---|
| `apps/android/{settings,build}.gradle.kts`, `gradle/libs.versions.toml`, `app/build.gradle.kts` | Build, catalogue, `API_BASE_URL` per build type |
| `app/src/main/AndroidManifest.xml`, `res/xml/{network_security_config,data_extraction_rules,backup_rules}.xml`, `app/src/debug/res/xml/network_security_config.xml` | Transport and backup policy |
| `.../config/ApiConfig.kt` | HTTPS-only base URL check |
| `.../data/net/{AuthApi,AuthInterceptor,UnauthorizedInterceptor,RedactingLogger,ApiResult}.kt` | Retrofit API, interceptors, result mapping |
| `.../data/session/{TokenCipher,SessionStore,SessionRepository,SessionEvent}.kt` | Encrypted token, state and events |
| `.../ui/{AppState,AppRoot,SecureWindow,ErrorMessage}.kt` | State machine, `NavHost`, window flag, shared error UI |
| `.../ui/signin/*`, `.../ui/password/*`, `.../ui/config/ConfigErrorScreen.kt` | Screens and view models |
| `apps/api/helm/customer-service/*` | Staging ingress, version bump, wording |
| `docker-compose.yml`, `apps/android/scripts/dev-tls.sh`, `.gitignore` | Local TLS front |
| `.claude/pre-pr-checks`, `CLAUDE.md`, `.claude/skills/{dev-setup,helm,ci}/SKILL.md` | Gate and docs |

## Streams (parallel-development)

Stream A (Tasks 1 to 8, Android, sequential within the stream), Stream B (Task 9, Helm), Stream C (Tasks 10 and 11, Compose TLS, pre-pr-checks, docs). B and C touch no Android files. Stream A's Task 12 (instrumented tests) needs C's script and the debug certificate path.

---

### Task 1: Gradle skeleton, manifest, transport policy, `ApiConfig`

**Files:**
- Create: `apps/android/` Gradle files listed above, manifest, the three XML policy files, debug network config, `app/src/main/java/com/customerservice/app/config/ApiConfig.kt`, `MainActivity.kt`
- Test: `app/src/test/java/com/customerservice/app/config/ApiConfigTest.kt`

**Interfaces:**
- Produces: `sealed interface ApiConfigResult { data class Valid(val baseUrl: String); data class Invalid(val reason: String) }`; `fun ApiConfig.check(baseUrl: String): ApiConfigResult`.

- [ ] **Step 1: Write failing tests** `ApiConfigTest`: `check_httpUrl_isInvalid`, `check_httpsUrl_isValid`, `check_upperCaseScheme_isInvalid`, `check_leadingWhitespace_isInvalid`, `check_blank_isInvalid`.
- [ ] **Step 2: Run** `cd apps/android && ./gradlew :app:testDebugUnitTest --tests '*ApiConfigTest'`. Expected: FAIL (class missing).
- [ ] **Step 3: Implement** `ApiConfig.check` (exact-prefix `https://`, no trimming). Add the build, manifest and XML files; the debug config trusts `@raw/dev_ca` and the build fails with a clear message when the file is missing for `debug`.
- [ ] **Step 4: Run** the same command. Expected: PASS. Also run `./gradlew :app:assembleRelease` and confirm the merged manifest has `usesCleartextTraffic="false"`.
- [ ] **Step 5: Commit** through the `committer` agent.

### Task 2: `ApiResult`, `ApiFailure`, field-error normalisation, `ErrorMessage`

**Files:** `data/net/ApiResult.kt`, `ui/ErrorMessage.kt`; tests `ApiResultTest.kt`, `ErrorMessageTest.kt` (Compose UI).

**Interfaces:**
- Produces: `sealed interface ApiResult<out T> { data class Ok<T>(val value: T); data class Failure(val failure: ApiFailure) }`; `sealed interface ApiFailure { Unauthorized; Forbidden; data class Validation(val fieldErrors: Map<String, List<String>>); NotFound; Conflict; Unavailable }`; `fun <T> retrofit2.Response<T>.toApiResult(errorBody parser): ApiResult<T>`; `@Composable fun ErrorMessage(failure: ApiFailure, onRetry: (() -> Unit)?)`.

- [ ] **Step 1: Write failing tests**: 401→`Unauthorized`, 403→`Forbidden`, 404→`NotFound`, 409→`Conflict`, 5xx and `IOException`→`Unavailable`; 400 with `errors` `{"$.NewPassword":["x"]}` yields key `newpassword`; `ErrorMessage` shows "Something went wrong. Try again." for `Unavailable`, shows **Retry** only when `onRetry` is non-null.
- [ ] **Step 2: Run** `./gradlew :app:testDebugUnitTest --tests '*ApiResultTest'`. Expected: FAIL.
- [ ] **Step 3: Implement** the mapping and the composable. Lower-case keys on the way out so callers match case-insensitively.
- [ ] **Step 4: Run** the unit and Compose tests. Expected: PASS.
- [ ] **Step 5: Commit.**

### Task 3: `TokenCipher` and `SessionStore`

**Files:** `data/session/TokenCipher.kt`, `SessionStore.kt`; tests `TokenCipherTest.kt` (JVM, with an injectable `SecretKey` provider), `SessionStoreTest.kt`.

**Interfaces:**
- Produces: `class TokenCipher(keyProvider: () -> SecretKey) { fun encrypt(plain: String): Encrypted; fun decrypt(e: Encrypted): String? }` with `data class Encrypted(val iv: ByteArray, val cipherText: ByteArray)`; `class SessionStore(dataStore: DataStore<Preferences>, cipher: TokenCipher) { val token: Flow<String?>; suspend fun awaitToken(): String?; suspend fun save(token: String); suspend fun clear() }` keeping an in-memory copy.

- [ ] **Step 1: Write failing tests**: `encryptDecrypt_roundTrip_returnsPlain`, `decrypt_wrongKey_returnsNull`, `save_thenFileContents_doNotContainPlaintext`, `awaitToken_decryptFailure_returnsNull`, `clear_removesToken`.
- [ ] **Step 2: Run** `--tests '*TokenCipherTest' --tests '*SessionStoreTest'`. Expected: FAIL.
- [ ] **Step 3: Implement.** AES/GCM/NoPadding, 256-bit key from `AndroidKeyStore` (non-exportable) in the production provider; DataStore file name `session.preferences_pb`, named in the backup exclusions.
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit.**

### Task 4: Networking: `AuthApi`, interceptors, redacting logger

**Files:** `data/net/{AuthApi,AuthInterceptor,UnauthorizedInterceptor,RedactingLogger}.kt`, `data/session/SessionEvent.kt`; tests with `MockWebServer`.

**Interfaces:**
- Consumes: `SessionStore` (Task 3).
- Produces: `interface AuthApi` with `login(LoginRequest): Response<LoginResponse>`, `logout(): Response<Unit>`, `me(): Response<MeResponse>`, `changePassword(ChangePasswordRequest): Response<Unit>`; `sealed interface SessionEvent { object Expired }`; `class UnauthorizedInterceptor(store: SessionStore, events: MutableSharedFlow<SessionEvent>)`; `RedactingLogger` as an `HttpLoggingInterceptor.Logger`-compatible interceptor at level `BASIC`-equivalent.

- [ ] **Step 1: Write failing tests**: `AuthInterceptor_addsBearer_onMe`, `AuthInterceptor_skipsLogin`, `UnauthorizedInterceptor_401WithToken_clearsStoreAndEmitsExpired`, `UnauthorizedInterceptor_403_leavesStore`, `UnauthorizedInterceptor_loginReturns401_doesNotEmit`, `RedactingLogger_neverLogsBodyOrAuthorizationValue` (a unique marker in the body and token must not appear in captured lines).
- [ ] **Step 2: Run** `--tests '*InterceptorTest' --tests '*RedactingLoggerTest'`. Expected: FAIL.
- [ ] **Step 3: Implement.** Decide "carried a token" by whether the request had an `Authorization` header after `AuthInterceptor`.
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit.**

### Task 5: `SessionRepository` and `AppState`

**Files:** `data/session/SessionRepository.kt`, `ui/AppState.kt`; test `SessionRepositoryTest.kt` (Turbine).

**Interfaces:**
- Consumes: `AuthApi`, `SessionStore`, `SessionEvent` flow.
- Produces: `sealed interface AppState { Launching; SignedOut; data class Checking(val failure: ApiFailure?); PasswordChange; data class SignedIn(val role: String) }`; `class SessionRepository { val state: StateFlow<AppState>; suspend fun start(); suspend fun signIn(u: String, p: String): ApiResult<Unit>; suspend fun signOut(); suspend fun changePassword(current: String, new: String): ApiResult<Unit>; suspend fun retry() }`.

- [ ] **Step 1: Write failing tests**, one per transition in the design's state diagram: `start_noToken_signedOut`, `start_tokenAndMeRole_signedIn`, `start_meMustChange_passwordChange`, `start_me401_clearsStoreSignedOut`, `start_networkError_keepsTokenAndStaysChecking`, `retry_afterNetworkError_signedIn`, `signIn_mustChangePassword_passwordChange`, `changePassword_204_callsMeThenSignedIn`, `signOut_logoutFails_stillClearsStoreSignedOut`, `expiredEvent_whileSignedIn_signedOut`, `expiredEvent_duringSignOut_clearsOnce`.
- [ ] **Step 2: Run** `--tests '*SessionRepositoryTest'`. Expected: FAIL.
- [ ] **Step 3: Implement** the state machine with a single mutex-guarded transition function.
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit.**

### Task 6: Sign-in and change-password screens

**Files:** `ui/signin/{SignInScreen,SignInViewModel}.kt`, `ui/password/{ChangePasswordScreen,ChangePasswordViewModel}.kt`, `ui/SecureWindow.kt`; Compose UI tests.

**Interfaces:**
- Consumes: `SessionRepository` (Task 5), `ErrorMessage` (Task 2).
- Produces: `@Composable fun SecureWindow(enabled: Boolean)` (sets `FLAG_SECURE` on enter, clears on leave); `SignInScreen(viewModel)`; `ChangePasswordScreen(viewModel, onSignOut)`.

- [ ] **Step 1: Write failing tests**: `SignIn_wrongUsernameAndWrongPassword_showSameMessageAndClearPassword`, `SignIn_networkFailure_showsRetry`, `ChangePassword_400_showsFieldErrorsAndKeepsTypedValues`, `ChangePassword_passwordFieldsUsePasswordTransformation`, `SecureWindow_setsAndClearsFlag`.
- [ ] **Step 2: Run** `./gradlew :app:testDebugUnitTest --tests '*ScreenTest'`. Expected: FAIL.
- [ ] **Step 3: Implement.** Fixed sign-in message text is a single constant used for 400 and 401.
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit.**

### Task 7: `AppRoot` navigation, config error, account menu

**Files:** `ui/AppRoot.kt`, `ui/config/ConfigErrorScreen.kt`, `MainActivity.kt`; tests `AppRootTest.kt`.

**Interfaces:**
- Consumes: `AppState`, screens, `ApiConfig.check`.
- Produces: `@Composable fun AppRoot(state: AppState, ...)`. Only the `SignedIn` branch declares customer or user routes (placeholder list screen for #37); no deep links.

- [ ] **Step 1: Write failing tests**: `AppRoot_signedOut_hasNoCustomerOrUserRoute`, `AppRoot_passwordChange_showsOnlyChangePasswordAndSignOut`, `AppRoot_httpBaseUrl_showsConfigErrorAndMakesNoRequest`, `AppRoot_signOut_emptiesBackStack`, `AppRoot_signedIn_accountMenuOpensVoluntaryChangePassword`.
- [ ] **Step 2: Run** `--tests '*AppRootTest'`. Expected: FAIL.
- [ ] **Step 3: Implement.** Wiring through a hand-written `AppContainer` (no DI framework).
- [ ] **Step 4: Run** all JVM tests and `./gradlew :app:lintDebug`. Expected: PASS.
- [ ] **Step 5: Commit.**

### Task 8: Debug-only Info logging of auth events

**Files:** `SessionRepository.kt`, test `AuthLoggingTest.kt`.

- [ ] **Step 1: Failing test** `signIn_debugBuild_logsInfoWithoutUsernameOrToken`; release build logs nothing from the auth layer (log through a `BuildConfig.DEBUG`-gated logger injected into the repository).
- [ ] **Step 2 to 5:** run, implement, run, commit.

### Task 9: Helm staging ingress (Stream B)

**Files:** `apps/api/helm/customer-service/values-staging.yaml`, `Chart.yaml`, `values.yaml` (comments), `templates/NOTES.txt`, `.claude/skills/helm/SKILL.md`.

- [ ] **Step 1: Write the failing check.** Run `helm template customer-service apps/api/helm/customer-service -f apps/api/helm/customer-service/values-staging.yaml | grep -c "kind: Ingress"`. Expected: `0`.
- [ ] **Step 2: Implement.** Staging: `ingress.enabled: true`, one host `customer-api.staging.example` (placeholder, operator overrides), one path `/api` with `pathType: Prefix`, `tls` entry with `secretName: customer-service-api-tls`; bump `Chart.yaml` `version` to `0.2.0`; replace the "isn't exposed" comments in `values-staging.yaml`, `values.yaml` and the `NOTES.txt` and `helm` skill wording. `values-prod.yaml` stays unchanged and says why. `values-dev.yaml` (done, at your request) turns the ingress on for the local `kind-litellm` cluster: class `traefik`, `websecure` entrypoint, path `/api`, hosts `customer-api.localhost` and `api.10-0-2-2.nip.io`, TLS secret `customer-service-api-tls`. The `helm` skill's "ingress is off in dev" wording changes with it, and Task 10's certificate SANs include both hosts.
- [ ] **Step 3: Run** `helm lint` and `helm template` for dev, staging and prod. Expected: staging renders one Ingress with path `/api` and a `tls` block; dev and prod render none.
- [ ] **Step 4: Commit.**

### Task 10: Compose TLS front and `dev-tls.sh` (Stream C)

**Files:** `docker-compose.yml`, `apps/android/scripts/dev-tls.sh`, `apps/android/scripts/nginx.conf`, `.gitignore`.

- [ ] **Step 1: Implement** `dev-tls.sh` (POSIX sh): create `dev-certs/` with a CA and a server certificate whose SANs are `localhost`, `10.0.2.2` and the machine's LAN name; copy only the CA certificate to `apps/android/app/src/debug/res/raw/dev_ca.pem`. Add both paths to `.gitignore`.
- [ ] **Step 2: Implement** the `tls-proxy` service (nginx, `profiles: ["mobile"]`, port 8443→443, upstream `api:8080`) mounting `dev-certs/` read-only.
- [ ] **Step 3: Verify** `sh apps/android/scripts/dev-tls.sh`, then `docker compose --profile mobile config` parses, and `git status` shows no key or certificate staged.
- [ ] **Step 4: Commit.**

### Task 11: `pre-pr-checks` Android step and docs (Stream C)

**Files:** `.claude/pre-pr-checks`, `CLAUDE.md`, `.claude/skills/dev-setup/SKILL.md`, `.claude/skills/ci/SKILL.md`.

- [ ] **Step 1: Implement** in `.claude/pre-pr-checks`: when `apps/android` exists and both `java` and an Android SDK (`ANDROID_HOME` or `ANDROID_SDK_ROOT`) are present, `run "android: unit tests + lint" sh -c "cd apps/android && ./gradlew test lint"`; otherwise print a skip notice to stderr.
- [ ] **Step 2: Update** `CLAUDE.md` (the "only the web app is public" line, the module table and local setup), `dev-setup` (JDK, SDK, `dev-tls.sh`, `--profile mobile`) and `ci` (the skipped Android step gap).
- [ ] **Step 3: Verify** `sh .claude/pre-pr-checks` reaches the Android step and either runs it or prints the skip notice.
- [ ] **Step 4: Commit.**

### Task 12: Instrumented tests (Stream A, after Tasks 7 and 10)

**Files:** `app/src/androidTest/java/com/customerservice/app/*`.

- [ ] **Step 1: Write the tests**: `signIn_thenRestartProcess_staysSignedIn`, `signOut_thenRestartProcess_staysOut`, `storedToken_inDataStoreFile_isNotPlaintext`, `cleartextHttpRequest_fails`, `sessionDeletedServerSide_nextCall_returnsToSignIn`.
- [ ] **Step 2: Run** against the TLS-fronted Compose stack on an emulator: `./gradlew :app:connectedDebugAndroidTest`. Expected: PASS. If no emulator is available, record that in the PR and leave the tests compiling (`./gradlew :app:assembleDebugAndroidTest`).
- [ ] **Step 3: Commit.**

### Task 13: Wrap-up

- [ ] Run `sh .claude/pre-pr-checks` and fix failures; run `requesting-code-review`; open the PR through `pr-author` on branch `GH-35-android-app-foundation`; do not merge without the user's say-so.

## Self-review

- Coverage: AAU-0010/0040 (Tasks 5, 6), 0020 (5, 7), 0030 (7), 0050 (5), 0060 (4, 5), 0070 (4, 8), 0080 to 0100 (5, 6, 7), 0110 (1, 7, 9, 10). Story ACs 1 to 11 each map to a named test above.
- Gaps accepted: the macrobenchmark and the manual backup, screenshot and recents cases from the design stay manual and are listed in the PR.
- Open assumption: the staging host name in Task 9 is a placeholder; the operator sets the real host and TLS secret.
