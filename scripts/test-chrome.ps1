$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
# A disposable official Chrome for Testing, never the user's browser/profile.
$version = '156.0.8078.80'
$work = Join-Path $env:RUNNER_TEMP 'clipdrop-chrome-test'
New-Item -ItemType Directory -Force $work | Out-Null
$zip = Join-Path $work 'chrome.zip'
Invoke-WebRequest "https://storage.googleapis.com/chrome-for-testing-public/$version/win64/chrome-win64.zip" -OutFile $zip
Expand-Archive $zip -DestinationPath $work -Force
$hive = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, [Microsoft.Win32.RegistryView]::Registry32)
$subkey = 'Software\Google\Chrome\NativeMessagingHosts\com.nasaat.clipdrop.downloads'
$key = $hive.CreateSubKey($subkey)
$previous = $key.GetValue('')
try {
    $key.SetValue('', (Join-Path $PWD 'artifacts/publish/com.nasaat.clipdrop.downloads.json'))
    node scripts/test-chrome.mjs (Join-Path $work 'chrome-win64/chrome.exe') (Join-Path $PWD 'artifacts/publish/browser/chrome') (Join-Path $work 'profile')
    if ($LASTEXITCODE -ne 0) { throw 'Chrome-to-native-host integration failed' }
} finally {
    if ($null -ne $previous) { $key.SetValue('', $previous) }
    else { $key.DeleteValue('', $false) }
    $key.Dispose()
    if ($null -eq $previous) { $hive.DeleteSubKey($subkey, $false) }
    $hive.Dispose()
}
