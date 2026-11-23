$ErrorActionPreference = 'Stop'
$version = $env:TORII_RELEASE_VERSION
$androidOnly = $env:TORII_ANDROID_ONLY -eq 'true'
if ($androidOnly -and $env:TORII_REGISTER_RELEASE -eq 'true') { throw 'Android preflight cannot publish a release' }
if ($version -notmatch '^\d{4}\.\d+\.\d+-(nova|torii)$') { throw 'Invalid release version' }
$numeric = ($version -split '-')[0] + '.0'

$isNet10 = $version.EndsWith('-torii') -or (Test-Path '.github/satori-net10.json')
if ($isNet10) {
    $runtimeMajor = 10
    $satoriPrefix = 'satori10'
    $targetFramework = 'net10.0'
    $androidTfm = 'net10.0-android'
    $dotnet = 'C:\Program Files\dotnet\dotnet.exe'
    if (!(Test-Path -LiteralPath $dotnet)) {
        $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
        if ($cmd) { $dotnet = $cmd.Source }
        else { throw 'Local .NET 10 SDK is missing' }
    }
    $sdk = Split-Path -Parent $dotnet
    $cache = if ($env:TORII_LOCAL_DOTNET11) { Split-Path -Parent $env:TORII_LOCAL_DOTNET11 } else { 'C:\Users\megablackito\Favorites\toriiserverlocal\.tmp' }
} else {
    $runtimeMajor = 11
    $satoriPrefix = 'satori11'
    $targetFramework = 'net11.0'
    $androidTfm = 'net11.0-android'
    $sdk = $env:TORII_LOCAL_DOTNET11
    $dotnet = Join-Path $sdk 'dotnet.exe'
    if (!(Test-Path -LiteralPath $dotnet)) { throw 'Local .NET 11 SDK is missing' }
    $cache = Split-Path -Parent $sdk
}

$buildCache = Join-Path (Split-Path -Parent $env:RUNNER_TEMP) 'torii-build-cache'
$env:DOTNET_ROOT = $sdk
$env:TEMP = Join-Path $env:RUNNER_TEMP 'release-build'
$env:TMP = $env:TEMP
$env:DOTNET_CLI_HOME = Join-Path $buildCache 'dotnet-home'
$env:NUGET_PACKAGES = Join-Path $buildCache 'nuget-packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $buildCache 'nuget-http'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
New-Item -ItemType Directory -Path $env:TEMP,$env:DOTNET_CLI_HOME -Force | Out-Null
$rids = @('win-x64','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64')
foreach ($rid in $rids) {
    python .github/scripts/satori_runtime.py verify $rid (Join-Path $cache "$satoriPrefix-$rid-ci")
    if ($LASTEXITCODE -ne 0) { throw "Invalid Satori payload: $rid" }
}
foreach ($name in 'ANDROID_KEYSTORE_BASE64','ANDROID_KEYSTORE_PASSWORD','ANDROID_KEY_ALIAS','ANDROID_KEY_PASSWORD') {
    if (![Environment]::GetEnvironmentVariable($name)) { throw "Missing original signing credential: $name" }
}
$androidSdk = Join-Path $cache 'android-sdk-local'
$env:ANDROID_SDK_ROOT = $androidSdk
$java = Get-ChildItem 'C:/Program Files/Eclipse Adoptium' -Directory -Filter 'jdk-21*' | Sort-Object Name -Descending | Select-Object -First 1
if (!$java -or !(Test-Path "$androidSdk/platforms/android-37.0/android.jar") -or !(Test-Path "$androidSdk/platforms/android-36/android.jar")) { throw 'Android SDK 36/37.0 or JDK 21 missing' }
$env:JAVA_HOME = $java.FullName
$tools = Get-ChildItem "$androidSdk/build-tools" -Directory | Sort-Object Name -Descending | Select-Object -First 1
$apksigner = Join-Path $tools.FullName 'apksigner.bat'
if (!(Test-Path $apksigner)) { throw 'Android signing tools missing' }
$workspace = (Resolve-Path $env:GITHUB_WORKSPACE).Path.TrimEnd('\')
foreach ($relative in 'client/Releases','client/publish','release-files') {
    $target = [IO.Path]::GetFullPath((Join-Path $workspace $relative))
    if (!$target.StartsWith($workspace + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe staging path' }
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
}
New-Item -ItemType Directory -Path Releases -Force | Out-Null
New-Item -ItemType Directory -Path publish -Force | Out-Null
& compact.exe /C /Q publish | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not enable compression for build staging' }
python -m unittest discover -s .github/scripts -p 'test_release_contract.py' -v
if ($LASTEXITCODE -ne 0) { throw 'Release contract tests failed' }
$hashes = @{}
& $dotnet test osu.Game.Tests/osu.Game.Tests.csproj -c Debug --filter 'FullyQualifiedName~ToriiStableUITest' -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Classic UI configuration regressions; publication blocked' }
$title = if ($version.EndsWith('-nova')) { 'Torii Nova' } else { 'Torii' }
$desktopRids = if ($androidOnly) { @() } else { $rids }
foreach ($rid in $desktopRids) {
    $output = "publish/$rid"
    & $dotnet publish osu.Desktop/osu.Desktop.csproj -c Release -r $rid --self-contained true `
        -p:PublishSingleFile=false -p:PublishTrimmed=false -p:Version=$version `
        -p:FileVersion=$numeric -p:AssemblyVersion=$numeric -p:InformationalVersion="v$version" -o $output
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $rid" }
    python .github/scripts/satori_runtime.py overlay $rid (Join-Path $cache "$satoriPrefix-$rid-ci") --output $output
    if ($LASTEXITCODE -ne 0) { throw "Runtime overlay failed: $rid" }
    Get-ChildItem -LiteralPath $output -Filter '*.xml' | Remove-Item -Force
    $hash = (Get-FileHash "$output/osu.Game.dll" -Algorithm MD5).Hash.ToLowerInvariant()
    $label = if ($rid.StartsWith('win')) { 'Windows' } elseif ($rid.StartsWith('osx')) { 'macOS' } else { 'Linux' }
    if ($hashes.ContainsKey($hash) -and $hashes[$hash] -ne $label) {
        $hashes[$hash] = ''
    } else {
    $hashes[$hash] = if ($hashes.ContainsKey($hash) -and $hashes[$hash] -ne $label) { 'Desktop' } else { $label }
    }
    if ($rid.StartsWith('win')) {
        $channel = if ($rid -eq 'win-x64') { 'win' } else { 'win-arm64' }
        Remove-Item Env:DOTNET_ROOT -ErrorAction SilentlyContinue
        vpk pack -u torii -v $version -p $output -e torii.exe -r $rid --packAuthors 'Torii Team' --packTitle $title -i osu.Desktop/torii.ico -c $channel -o Releases
        if ($LASTEXITCODE -ne 0) { throw "Velopack failed: $rid" }
        $env:DOTNET_ROOT = $sdk
        Move-Item -LiteralPath "Releases/torii-$channel-Setup.exe" -Destination "Releases/install-$rid.exe"
        Move-Item -LiteralPath "Releases/torii-$channel-Portable.zip" -Destination "Releases/portable-$rid.zip"
        $indexPath = "Releases/assets.$channel.json"
        $index = Get-Content $indexPath -Raw | ConvertFrom-Json
        foreach ($entry in $index) {
            if ($entry.Type -eq 'Installer') { $entry.RelativeFileName = "install-$rid.exe" }
            if ($entry.Type -eq 'Portable') { $entry.RelativeFileName = "portable-$rid.zip" }
        }
        ConvertTo-Json -InputObject @($index) -Depth 10 | Out-File $indexPath -Encoding utf8
    } else {
        $extension = if ($rid.StartsWith('linux')) { 'tar.gz' } else { 'zip' }
        python .github/scripts/release_contract.py archive $output "Releases/portable-$rid.$extension"
        if ($LASTEXITCODE -ne 0) { throw "Archive failed: $rid" }
        if ($rid.StartsWith('osx')) {
            $stage = "publish/bundle-$rid"
            $app = "$stage/Torii.app"
            New-Item -ItemType Directory -Path "$app/Contents/MacOS","$app/Contents/Resources" -Force | Out-Null
            Copy-Item "$output/*" "$app/Contents/MacOS" -Recurse
            Copy-Item osu.Desktop/torii.icns "$app/Contents/Resources/torii.icns"
            Copy-Item 'osu.Desktop/macos-package/Install Torii.command' $stage
            Copy-Item 'osu.Desktop/macos-package/README.txt' $stage
            $env:TORII_BUNDLE_PATH = $app
            $env:TORII_BUNDLE_VERSION = ($version -split '-')[0]
            python .github/scripts/release_macos_plist.py
            if ($LASTEXITCODE -ne 0) { throw 'macOS metadata generation failed' }
            $name = if ($rid -eq 'osx-arm64') { 'Torii-macOS-AppleSilicon.zip' } else { 'Torii-macOS-Intel.zip' }
            python .github/scripts/release_contract.py archive $stage "Releases/$name"
            if ($LASTEXITCODE -ne 0) { throw 'macOS bundle packaging failed' }
        }
    }
}
$keystore = Join-Path $env:TEMP 'torii-original.keystore'
try {
    [IO.File]::WriteAllBytes($keystore, [Convert]::FromBase64String($env:ANDROID_KEYSTORE_BASE64))
    $code = 2040000000 + [long][Math]::Floor([DateTimeOffset]::UtcNow.ToUnixTimeSeconds() / 60)
    if ($code -ge 2100000000) { throw 'Android version code limit reached' }
    & $dotnet build osu.Android/osu.Android.csproj -t:Package -c Release -f $androidTfm -r android-arm64 `
        -p:Version=$version -p:ApplicationVersion=$code -p:ApplicationDisplayVersion=$version `
        -p:AndroidSdkDirectory=$androidSdk -p:JavaSdkDirectory=$env:JAVA_HOME `
        -p:ToriiUnsignedAndroid=true -p:AndroidKeyStore=true `
        -p:ApplicationTitle=$title `
        -p:AndroidPackageFormat=apk
    if ($LASTEXITCODE -ne 0) { throw 'Android package build failed' }
    New-Item -ItemType Directory -Path publish/android -Force | Out-Null
    $apk = @(Get-ChildItem "osu.Android/bin/Release/$androidTfm" -Filter 'sh.shikkesora.torii.apk' -Recurse)
    if ($apk.Count -ne 1) { throw 'Expected exactly one unsigned APK' }
    python .github/scripts/prepare_android_apk.py $apk[0].FullName ../torii-framework/osu.Framework.Android/arm64-v8a publish/android/torii-unsigned.apk
    if ($LASTEXITCODE -ne 0) { throw 'Android bionic packaging failed' }
    & (Join-Path $tools.FullName 'zipalign.exe') -P 16 -f 4 publish/android/torii-unsigned.apk publish/android/torii-aligned.apk
    if ($LASTEXITCODE -ne 0) { throw 'APK alignment failed' }
    & $apksigner sign --ks $keystore --ks-pass env:ANDROID_KEYSTORE_PASSWORD --ks-key-alias $env:ANDROID_KEY_ALIAS --key-pass env:ANDROID_KEY_PASSWORD publish/android/torii-aligned.apk
    if ($LASTEXITCODE -ne 0) { throw 'APK signing failed' }
    $finalApk = 'publish/android/torii-aligned.apk'
    & $apksigner verify --verbose --print-certs $finalApk
    if ($LASTEXITCODE -ne 0) { throw 'APK signature verification failed' }
    python .github/scripts/verify_android_release.py $finalApk $keystore $code
    if ($LASTEXITCODE -ne 0) { throw 'APK upgrade compatibility check failed' }
    Copy-Item $finalApk Releases/torii.apk
    $androidHash = python .github/scripts/android_release.py version-hash $version
    if ($LASTEXITCODE -ne 0) { throw 'Android hash calculation failed' }
    $hashes[$androidHash.Trim()] = 'Android'
} finally {
    if (Test-Path -LiteralPath $keystore) { Remove-Item -LiteralPath $keystore -Force }
}
if ($androidOnly) {
    'Android signature and upgrade checks passed; no release published.' | Out-File $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
    exit 0
}
python .github/scripts/build_appimages.py . (Join-Path $buildCache 'appimage-tools')
if ($LASTEXITCODE -ne 0) { throw 'AppImage packaging failed; publication blocked' }
python .github/scripts/release_contract.py validate Releases $version
if ($LASTEXITCODE -ne 0) { throw 'Incomplete release; publication blocked' }
$hashes | ConvertTo-Json | Out-File Releases/client-hashes.json -Encoding utf8
$outputRoot = Join-Path $workspace 'release-files'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
Move-Item Releases/* $outputRoot
$runtimeNote = if ($isNet10) {
    'All six desktop architectures and Android are included. Desktop builds bundle .NET 10 and Satori 10. Android preserves the application ID and original signing identity.'
} else {
    'All six desktop architectures and Android are included. Desktop builds bundle .NET 11 and Satori 11. Android preserves the application ID and original signing identity.'
}
$osNote = if ($isNet10) {
    'macOS requires 12 or newer. x64 requires x86-64-v2. These runtime requirements are unchanged from Torii .NET 10.'
} else {
    'macOS requires 14 or newer. x64 requires x86-64-v2. These runtime requirements are unchanged from the .NET 11 migration.'
}
$notes = @("# $title v$version",'', 'Hotfix: disabled the new Aspire/2B rendering that caused ghost circles. Classic UI and player profiles are still included.', '', 'Player profiles, Torii Classic UI and optional NoFail.', '', $runtimeNote, '', $osNote, '', "Private source commit: $(git rev-parse HEAD)")
$notes += @('', 'Fixes: menu cursor trail visibility, toolbar auto-hide coordinates and focus handling, Classic UI isolation from other themes, and preservation of existing settings without a login token.', '', 'Linux AppImages are available for x64 and ARM64 alongside the portable archives.')
$notes | Out-File (Join-Path $outputRoot RELEASE-NOTES.md) -Encoding utf8
$checksums = Get-ChildItem -LiteralPath $outputRoot -File | Sort-Object Name | ForEach-Object {
    "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
}
$checksums | Out-File (Join-Path $outputRoot SHA256SUMS.txt) -Encoding ascii
if ($env:TORII_REGISTER_RELEASE -eq 'true') {
    if (!$env:TORII_VERSION_WEBHOOK_SECRET -or !$env:TORII_SERVER_URL) { throw 'Missing server hash registration configuration' }
    $payload = @{version=$version; client_name=$title; hashes=$hashes} | ConvertTo-Json -Depth 5
    $registration = Invoke-RestMethod -Uri "$env:TORII_SERVER_URL/api/private/client-versions/register" -Method POST `
        -Headers @{Authorization="Bearer $env:TORII_VERSION_WEBHOOK_SECRET"} -ContentType application/json -Body $payload
    if ($registration.registered -ne $hashes.Count -or $registration.version -ne $version) {
        throw 'Server did not register every release hash; publication blocked'
    }
}
"Validated complete $version from $(git rev-parse HEAD)" | Out-File $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
