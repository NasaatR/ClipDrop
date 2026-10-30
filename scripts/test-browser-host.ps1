$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$origin = 'chrome-extension://iedbmkchlbjjabmopniipnffmdjdpmmn/'
$profile = [Guid]::NewGuid().ToString('D')
$catalog = Join-Path $env:LOCALAPPDATA "NasaatR/ClipDrop/browser-downloads/$profile.json"
$fixture = Join-Path $env:RUNNER_TEMP 'Пример — browser host.txt'
[IO.File]::WriteAllText($fixture, 'Original bytes retained')
function Send-Native([string]$caller, [byte[]]$bytes) {
    $info = [Diagnostics.ProcessStartInfo]::new((Join-Path $PWD 'artifacts/publish/ClipDrop.BrowserHost.exe'))
    $info.UseShellExecute = $false
    $info.RedirectStandardInput = $true; $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    $info.ArgumentList.Add($caller)
    $process = [Diagnostics.Process]::Start($info)
    try {
        if ($bytes.Length -gt 0) {
            $prefix = [BitConverter]::GetBytes([uint32]$bytes.Length)
            $process.StandardInput.BaseStream.Write($prefix)
            $process.StandardInput.BaseStream.Write($bytes)
        }
        $process.StandardInput.Close()
        if (!$process.WaitForExit(10000)) { throw 'Native host did not exit' }
        $stream = $process.StandardOutput.BaseStream
        $output = [IO.MemoryStream]::new(); $stream.CopyTo($output)
        return @{ Code = $process.ExitCode; Bytes = $output.ToArray(); Error = $process.StandardError.ReadToEnd() }
    } finally { if (!$process.HasExited) { $process.Kill() }; $process.Dispose() }
}
try {
    $now = [DateTimeOffset]::UtcNow.ToString('o')
    $message = @{ version = 1; type = 'downloads'; profileId = $profile; revision = 1; capturedUtc = $now; files = @(@{ path = $fixture; completedUtc = $now }) } | ConvertTo-Json -Depth 5 -Compress
    $reply = Send-Native $origin ([Text.Encoding]::UTF8.GetBytes($message))
    if ($reply.Code -ne 0 -or $reply.Bytes.Length -lt 5) { throw "Published WinExe host pipe failed: $($reply.Code), $($reply.Error)" }
    $length = [BitConverter]::ToUInt32($reply.Bytes, 0)
    if ($length -ne $reply.Bytes.Length - 4) { throw 'Native response has an incorrect byte frame' }
    $result = [Text.Encoding]::UTF8.GetString($reply.Bytes, 4, $length) | ConvertFrom-Json
    if (!$result.ok -or !(Test-Path $catalog) -or [IO.File]::ReadAllText($fixture) -ne 'Original bytes retained') { throw 'Native host did not acknowledge intact local metadata' }
    $saved = [IO.File]::ReadAllText($catalog)
    $reply = Send-Native 'chrome-extension://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/' ([byte[]]::new(0))
    if ($reply.Code -ne 2 -or [IO.File]::ReadAllText($catalog) -ne $saved) { throw 'Native host accepted an unlisted extension origin' }
    $reply = Send-Native $origin ([Text.Encoding]::UTF8.GetBytes('{broken'))
    if ($reply.Code -eq 0 -or [IO.File]::ReadAllText($catalog) -ne $saved) { throw 'Malformed native input changed a usable snapshot' }
    Write-Output 'PASS Published WinExe native host receives UTF-8 binary pipes, acknowledges persisted metadata, rejects another origin and preserves data on malformed input.'
} finally { Remove-Item $catalog, $fixture -Force -ErrorAction SilentlyContinue }
