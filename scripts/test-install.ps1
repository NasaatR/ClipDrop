$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$target = Join-Path $env:RUNNER_TEMP 'ClipDrop-install-test'
$previous = Join-Path $env:RUNNER_TEMP 'ClipDrop-previous-setup.exe'
Invoke-WebRequest 'https://github.com/NasaatR/ClipDrop/releases/download/v0.3.1/ClipDrop_0.3.1_windows-x64_setup.exe' -OutFile $previous
$process = Start-Process $previous -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=`"$target`"") -Wait -PassThru
if ($process.ExitCode -ne 0) { throw 'Previous release installation failed' }
$data = Join-Path $env:LOCALAPPDATA 'NasaatR/ClipDrop'
New-Item -ItemType Directory -Force $data | Out-Null
$fixture = @{ SchemaVersion = 1; Items = @(@{ Id = [Guid]::NewGuid().ToString('N'); Kind = 'Text'; Title = 'Upgrade reference'; Content = 'Привет — upgrade preserved'; IsPinned = $true }); Settings = @{ DockOpacity = 0; DockSpacing = 0; Theme = 'Light' } } | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText((Join-Path $data 'shelf.json'), $fixture)
$installer = (Get-ChildItem artifacts/release/*_setup.exe).FullName
# No /DIR: the permanent AppId must reuse the previous installation directory.
$process = Start-Process $installer -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Upgrade failed: $($process.ExitCode)" }
[xml]$props = Get-Content Directory.Build.props
$expected = $props.Project.PropertyGroup.VersionPrefix
if (!(Test-Path "$target/ClipDrop.exe") -or !(Get-Item "$target/ClipDrop.exe").VersionInfo.FileVersion.StartsWith($expected)) { throw 'Previous directory was not updated to the expected version' }
$registrations = @(Get-ChildItem 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall' | Get-ItemProperty | Where-Object { $_.PSChildName -eq '{657DCFD1-3CCB-46E7-9BB1-534F69B57C36}_is1' })
if ($registrations.Count -ne 1) { throw "Expected one installed app, found $($registrations.Count)" }
if ([IO.File]::ReadAllText((Join-Path $data 'shelf.json')) -ne $fixture) { throw 'Installer changed saved user data' }
$app = Start-Process "$target/ClipDrop.exe" -ArgumentList '--background', '--check-startup' -PassThru
try {
    Start-Sleep -Seconds 2
    if ($app.HasExited) { throw 'Updated executable could not load previous data' }
    $quit = Start-Process "$target/ClipDrop.exe" -ArgumentList '--quit' -Wait -PassThru
    if ($quit.ExitCode -ne 0 -or !$app.WaitForExit(10000) -or $app.ExitCode -ne 0) { throw 'Updated executable did not exit cleanly' }
} finally { if (!$app.HasExited) { Stop-Process -Id $app.Id -Force } }
$kept = Get-Content (Join-Path $data 'shelf.json') -Raw | ConvertFrom-Json
if ($kept.Items[0].Content -ne 'Привет — upgrade preserved' -or $kept.Settings.DockOpacity -ne 0) { throw 'Upgrade lost contents or zero preferences' }
if ($kept.SchemaVersion -ne 4 -or !(Test-Path (Join-Path $data 'shelf.json.schema-1-before-v4.bak'))) { throw 'Schema migration backup was not preserved' }
if ([IO.File]::ReadAllText((Join-Path $data 'shelf.json.schema-1-before-v4.bak')) -ne $fixture) { throw 'Migration backup changed the original legacy data' }
$process = Start-Process "$target/unins000.exe" -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait -PassThru
if ($process.ExitCode -ne 0 -or (Test-Path "$target/ClipDrop.exe")) { throw 'Uninstallation failed' }
if (!(Test-Path (Join-Path $data 'shelf.json'))) { throw 'Uninstaller removed user data' }
Write-Output 'PASS Previous-version upgrade keeps one installation, original directory and saved user data; uninstall retains the library.'
