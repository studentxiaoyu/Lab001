param([string]$ProxyUrl, [string]$HubPath = 'D:/applications/Unity Hub/Unity Hub.exe')

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $HubPath)) {
    throw "Unity Hub was not found at $HubPath. Supply -HubPath to override."
}
if (Get-Process -Name 'Unity Hub' -ErrorAction SilentlyContinue) {
    throw 'Exit Unity Hub completely first so the new process can inherit the proxy configuration.'
}
if (-not $ProxyUrl) {
    $settings = Get-ItemProperty 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Internet Settings'
    if ($settings.ProxyEnable -ne 1 -or -not $settings.ProxyServer -or $settings.ProxyServer.Contains('=')) {
        throw 'Enable a single HTTP system proxy or supply -ProxyUrl http://host:port.'
    }
    $ProxyUrl = $settings.ProxyServer
    if ($ProxyUrl -notmatch '^https?://') { $ProxyUrl = "http://$ProxyUrl" }
}
$proxyUri = [uri]$ProxyUrl
if (-not $proxyUri.IsAbsoluteUri -or $proxyUri.Scheme -notin @('http', 'https')) {
    throw 'ProxyUrl must be an absolute HTTP or HTTPS URL.'
}
# Environment changes apply only to this launch and its child processes.
# Keep certificate verification enabled and preserve the caller's environment.
$previousHttp = $env:HTTP_PROXY
$previousHttps = $env:HTTPS_PROXY
try {
    $env:HTTP_PROXY = $ProxyUrl
    $env:HTTPS_PROXY = $ProxyUrl
    # Open the project from Hub so the Editor also receives Hub's login session.
    Start-Process -FilePath $HubPath -WindowStyle Hidden
} finally {
    $env:HTTP_PROXY = $previousHttp
    $env:HTTPS_PROXY = $previousHttps
}
