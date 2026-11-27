param(
    [Parameter(Mandatory)]
    [ValidateSet('win64', 'arm64')]
    [string]$Architecture
)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) {
    throw 'WebView2 WebDriver setup requires Windows.'
}

$runtimeKeys = @(
    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
    'HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
    'HKCU:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'
)
$runtimeVersion = $null
foreach ($key in $runtimeKeys) {
    $value = Get-ItemPropertyValue -Path $key -Name pv -ErrorAction SilentlyContinue
    $version = [version]'0.0'
    if ($value -match '^\d+\.\d+\.\d+\.\d+$' -and
        [version]::TryParse($value, [ref]$version) -and $version -gt [version]'0.0.0.0') {
        $runtimeVersion = $version.ToString()
        break
    }
}
if (-not $runtimeVersion) {
    throw 'WebView2 Evergreen Runtime was not found in the machine or user registry.'
}

$driverDirectory = Join-Path $env:RUNNER_TEMP "portal-webdriver-$Architecture"
$archive = Join-Path $env:RUNNER_TEMP "portal-edgedriver-$Architecture.zip"
New-Item -ItemType Directory -Force -Path $driverDirectory | Out-Null
$url = "https://msedgedriver.microsoft.com/$runtimeVersion/edgedriver_$Architecture.zip"
Write-Host "Downloading WebDriver for WebView2 $runtimeVersion from $url"
Invoke-WebRequest -Uri $url -OutFile $archive
Expand-Archive -LiteralPath $archive -DestinationPath $driverDirectory -Force
$driver = Join-Path $driverDirectory 'msedgedriver.exe'
if (-not (Test-Path -LiteralPath $driver -PathType Leaf)) {
    throw "Microsoft Edge WebDriver not found in $archive."
}
& $driver --version
if ($LASTEXITCODE -ne 0) {
    throw "Microsoft Edge WebDriver exited with code $LASTEXITCODE."
}
Add-Content -LiteralPath $env:GITHUB_PATH -Value $driverDirectory
