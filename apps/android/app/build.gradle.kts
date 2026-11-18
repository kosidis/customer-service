import com.android.build.api.variant.ApplicationAndroidComponentsExtension

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.kotlin.serialization)
}

android {
    namespace = "com.customerservice.app"
    compileSdkVersion("android-37.0")
    buildToolsVersion = "36.0.0"

    defaultConfig {
        applicationId = "com.customerservice.app"
        minSdk = 26
        targetSdk = 36
        versionCode = 1
        versionName = "0.1.0"
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    buildTypes {
        debug {
            // 10.0.2.2 is the emulator's name for the host running the local TLS front.
            buildConfigField("String", "API_BASE_URL", "\"https://10.0.2.2:8443\"")
        }
        release {
            buildConfigField("String", "API_BASE_URL", "\"https://customer-service.example.com\"")
            isMinifyEnabled = false
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlin {
        compilerOptions {
            jvmTarget.set(org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17)
        }
    }
}

dependencies {
    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.compose.ui)
    implementation(libs.androidx.compose.ui.tooling.preview)
    implementation(libs.androidx.compose.material3)
    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.navigation.compose)
    implementation(libs.androidx.lifecycle.viewmodel.compose)
    implementation(libs.androidx.lifecycle.runtime.compose)
    implementation(libs.androidx.datastore.preferences)
    implementation(libs.retrofit)
    implementation(libs.retrofit.converter.kotlinx.serialization)
    implementation(libs.okhttp)
    implementation(libs.kotlinx.serialization.json)
    implementation(libs.kotlinx.coroutines.android)
    debugImplementation(libs.androidx.compose.ui.tooling)

    testImplementation(libs.junit)
    testImplementation(libs.mockk)
    testImplementation(libs.turbine)
    testImplementation(libs.okhttp.mockwebserver)
    testImplementation(libs.kotlinx.coroutines.test)
}

// The debug network security config trusts @raw/dev_ca, whose file (src/debug/res/raw/dev_ca.pem)
// is generated locally by apps/android/scripts/dev-tls.sh and never committed. Resource linking
// needs the resource to exist, so when the file is missing a generated empty placeholder stands in
// for it. That keeps :app:testDebugUnitTest working on a fresh checkout, while packaging the debug
// variant fails with a clear message (see checkDevCa below).
val devCaFile = layout.projectDirectory.file("src/debug/res/raw/dev_ca.pem")

abstract class DevCaPlaceholderTask : DefaultTask() {
    @get:OutputDirectory
    abstract val outputDir: DirectoryProperty

    @TaskAction
    fun generate() {
        val raw = outputDir.get().asFile.resolve("raw")
        raw.mkdirs()
        raw.resolve("dev_ca.pem").writeText("")
    }
}

val devCaPlaceholder = tasks.register<DevCaPlaceholderTask>("generateDevCaPlaceholder") {
    outputDir.set(layout.buildDirectory.dir("generated/devCaPlaceholder/res"))
    onlyIf { !devCaFile.asFile.exists() }
}

val androidComponents = extensions.getByType<ApplicationAndroidComponentsExtension>()
androidComponents.onVariants(androidComponents.selector().withBuildType("debug")) { variant ->
    if (!devCaFile.asFile.exists()) {
        variant.sources.res?.addGeneratedSourceDirectory(devCaPlaceholder, DevCaPlaceholderTask::outputDir)
    }
}

val checkDevCa = tasks.register("checkDevCa") {
    val file = devCaFile.asFile
    doLast {
        if (!file.exists() || file.length() == 0L) {
            throw GradleException(
                "Missing debug CA certificate: ${file.path}. " +
                    "Run apps/android/scripts/dev-tls.sh to generate it (it is gitignored and never committed).",
            )
        }
    }
}

tasks.matching { it.name == "packageDebug" }.configureEach {
    dependsOn(checkDevCa)
}
