$ErrorActionPreference = 'Stop'

if (-not $IsWindows) {
    throw 'This smoke test requires Windows and an interactive desktop session.'
}

$logDirectory = if ($env:PORTAL_CLIENT_ARTIFACTS) {
    [System.IO.Path]::GetFullPath($env:PORTAL_CLIENT_ARTIFACTS)
} else {
    Join-Path $PSScriptRoot 'artifacts'
}
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$stdout = Join-Path $logDirectory 'window-smoke-stdout.log'
$stderr = Join-Path $logDirectory 'window-smoke-stderr.log'
$diagnostics = Join-Path $logDirectory 'window-smoke.log'
"Starting Windows window smoke test." | Set-Content -LiteralPath $diagnostics

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
    'WebView2 Evergreen Runtime was not found.' | Add-Content -LiteralPath $diagnostics
    throw 'WebView2 Evergreen Runtime was not found in the machine or user registry. Install it before running the smoke test.'
}
"WebView2 Evergreen Runtime: $version ($key)" | Add-Content -LiteralPath $diagnostics

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
    "Debug executable not found: $executable" | Add-Content -LiteralPath $diagnostics
    throw "Debug executable not found: $executable"
}

$process = $null
try {
    $process = Start-Process -FilePath $executable -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    Write-Host "Started $executable (PID $($process.Id))"
    "Started $executable (PID $($process.Id))" | Add-Content -LiteralPath $diagnostics
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
                'Portal window remained visible and the process stayed running for 10 seconds.' | Add-Content -LiteralPath $diagnostics
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
} catch {
    $_ | Out-String | Add-Content -LiteralPath $diagnostics
    throw
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
}
