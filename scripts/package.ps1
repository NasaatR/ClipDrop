$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    [xml]$properties = Get-Content Directory.Build.props
    $version = $properties.Project.PropertyGroup.VersionPrefix
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid product version' }
    $publish = Join-Path $projectRoot 'artifacts/publish'
    $release = Join-Path $projectRoot 'artifacts/release'
    New-Item -ItemType Directory -Force $release | Out-Null
    dotnet publish src/ClipDrop.Windows -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    dotnet publish src/ClipDrop.BrowserHost -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'Browser host publish failed' }
    New-Item -ItemType Directory -Force "$publish/browser" | Out-Null
    Copy-Item "browser/*" "$publish/browser" -Recurse -Force
    $hostManifest = @{ name = 'com.nasaat.clipdrop.downloads'; description = 'ClipDrop recent downloads'; path = 'ClipDrop.BrowserHost.exe'; type = 'stdio'; allowed_origins = @('chrome-extension://iedbmkchlbjjabmopniipnffmdjdpmmn/') } | ConvertTo-Json
    [IO.File]::WriteAllText("$publish/com.nasaat.clipdrop.downloads.json", $hostManifest)
    Compress-Archive -Path browser/chrome, browser/CONNECT-CHROME.html -DestinationPath "$release/ClipDrop_${version}_chrome-companion.zip" -Force
    Copy-Item LICENSE, README.md, README.ru.md, THIRD-PARTY-NOTICES.md $publish
    [xml]$project = Get-Content src/ClipDrop.Windows/ClipDrop.Windows.csproj
    $skiaVersion = $project.SelectSingleNode("/Project/ItemGroup/PackageReference[@Include='SkiaSharp']").Version
    $packageCache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages' }
    Copy-Item (Join-Path $packageCache "skiasharp/$skiaVersion/LICENSE.txt") "$publish/SKIASHARP-LICENSE.txt"
    Copy-Item (Join-Path $packageCache "skiasharp.nativeassets.win32/$skiaVersion/THIRD-PARTY-NOTICES.txt") "$publish/SKIA-THIRD-PARTY-NOTICES.txt"
    if (!(Test-Path "$publish/libSkiaSharp.dll")) { throw 'Bundled raster decoder is missing.' }
    if (Test-Path "$publish/libSkiaSharp.pdb") { Remove-Item "$publish/libSkiaSharp.pdb" }
    Invoke-WebRequest 'https://raw.githubusercontent.com/dotnet/runtime/main/LICENSE.TXT' -OutFile "$publish/DOTNET-LICENSE.txt"
    Invoke-WebRequest 'https://raw.githubusercontent.com/dotnet/runtime/main/THIRD-PARTY-NOTICES.TXT' -OutFile "$publish/DOTNET-THIRD-PARTY-NOTICES.txt"
    $portableRoot = Join-Path $projectRoot "artifacts/portable/ClipDrop_$version"
    New-Item -ItemType Directory -Force $portableRoot | Out-Null
    Copy-Item "$publish/*" $portableRoot -Recurse -Force
    Compress-Archive -Path $portableRoot -DestinationPath "$release/ClipDrop_${version}_windows-x64_portable.zip" -Force
    $compiler = "${env:ProgramFiles(x86)}/Inno Setup 6/ISCC.exe"
    if (!(Test-Path $compiler)) { throw 'Inno Setup 6 was not found on this build machine.' }
    & $compiler "/DMyAppVersion=$version" packaging/ClipDrop.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
    $checksums = Get-ChildItem $release -File | Where-Object Extension -in '.exe', '.zip' | Sort-Object Name | ForEach-Object {
        "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
    }
    $checksums | Set-Content "$release/SHA256SUMS.txt" -Encoding utf8
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead("$release/ClipDrop_${version}_windows-x64_portable.zip")
    try {
        $entries = $archive.Entries.FullName -replace '\\', '/'
        if ($entries -notcontains "ClipDrop_$version/ClipDrop.exe") { throw 'Portable executable missing from archive' }
        if ($entries -notcontains "ClipDrop_$version/LICENSE") { throw 'License missing from archive' }
        if ($entries -notcontains "ClipDrop_$version/ClipDrop.BrowserHost.exe" -or $entries -notcontains "ClipDrop_$version/browser/chrome/worker.mjs") { throw 'Chrome companion missing from archive' }
        if ($entries | Where-Object { $_ -match '(^|/)(\.git|obj|\.env)(/|$)' }) { throw 'Unexpected private/build files in archive' }
    } finally { $archive.Dispose() }
} finally { Pop-Location }
