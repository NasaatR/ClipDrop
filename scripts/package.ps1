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
    Copy-Item LICENSE, README.md, README.ru.md, THIRD-PARTY-NOTICES.md $publish
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
        if ($entries | Where-Object { $_ -match '(^|/)(\.git|obj|\.env)(/|$)' }) { throw 'Unexpected private/build files in archive' }
    } finally { $archive.Dispose() }
} finally { Pop-Location }
