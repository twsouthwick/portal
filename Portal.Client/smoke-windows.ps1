$ErrorActionPreference = 'Stop'

if (-not $IsWindows) {
    throw 'This smoke test requires Windows and an interactive desktop session.'
}

$runtimeKeys = @(
    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
    'HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
    'HKCU:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'
)
$runtimeFound = $false
foreach ($key in $runtimeKeys) {
    $value = Get-ItemPropertyValue -Path $key -Name pv -ErrorAction SilentlyContinue
    $version = [version]'0.0'
    if ($value -and [version]::TryParse($value, [ref]$version) -and $version -gt [version]'0.0.0.0') {
        Write-Host "WebView2 Evergreen Runtime: $version ($key)"
        $runtimeFound = $true
        break
    }
}
if (-not $runtimeFound) {
    throw 'WebView2 Evergreen Runtime was not found in the machine or user registry. Install it before running the smoke test.'
}

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PortalWindow {
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);
}
'@

$executable = Join-Path $PSScriptRoot 'src-tauri\target\debug\portalclient.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Debug executable not found: $executable"
}

$logDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "portal-smoke-$([guid]::NewGuid())"
New-Item -ItemType Directory -Path $logDirectory | Out-Null
$stdout = Join-Path $logDirectory 'stdout.txt'
$stderr = Join-Path $logDirectory 'stderr.txt'
$process = $null
try {
    $process = Start-Process -FilePath $executable -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    Write-Host "Started $executable (PID $($process.Id))"
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $visibleSince = $null
    while ($timer.Elapsed.TotalSeconds -lt 60) {
        $process.Refresh()
        if ($process.HasExited) {
            throw "Portal exited before the smoke test completed (exit code $($process.ExitCode))."
        }

        $handle = $process.MainWindowHandle
        $visible = $handle -ne [IntPtr]::Zero -and
            $process.MainWindowTitle -eq 'Portal' -and [PortalWindow]::IsWindowVisible($handle)
        if ($visible) {
            if ($null -eq $visibleSince) {
                $visibleSince = $timer.Elapsed
                Write-Host "Portal window opened (handle $handle); observing for 10 seconds."
            }
            if (($timer.Elapsed - $visibleSince).TotalSeconds -ge 10) {
                Write-Host 'Portal window remained visible and the process stayed running.'
                break
            }
        } elseif ($null -ne $visibleSince) {
            throw "Portal window disappeared during observation (handle: $handle, title: '$($process.MainWindowTitle)')."
        }
        Start-Sleep -Seconds 1
    }
    if ($null -eq $visibleSince -or ($timer.Elapsed - $visibleSince).TotalSeconds -lt 10) {
        throw "Timed out waiting for a visible Portal window (handle: $handle, title: '$($process.MainWindowTitle)')."
    }
} finally {
    if ($null -ne $process) {
        $process.Refresh()
        if (-not $process.HasExited) {
            $process.Kill($true)
            $process.WaitForExit()
        }
        $process.Dispose()
    }
    foreach ($log in @($stdout, $stderr)) {
        if (Test-Path -LiteralPath $log) {
            Write-Host "--- $(Split-Path $log -Leaf) ---"
            Get-Content -LiteralPath $log
        }
    }
    Remove-Item -LiteralPath $logDirectory -Recurse -Force
}
