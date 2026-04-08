param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$InstallerUrl,

    [string]$InstallerPath = ".\artifacts\installer\CoupleFinance-Setup.exe",

    [string]$PortableUrl = "",

    [string]$PortablePath = "",

    [string]$OutputPath = ".\artifacts\installer\update-manifest.json",

    [string]$ReleaseNotes = "",

    [switch]$Mandatory
)

$resolvedInstallerPath = Resolve-Path $InstallerPath
$hash = (Get-FileHash -Algorithm SHA256 $resolvedInstallerPath).Hash.ToLowerInvariant()
$portableHash = $null

if ($PortablePath) {
    $resolvedPortablePath = Resolve-Path $PortablePath
    $portableHash = (Get-FileHash -Algorithm SHA256 $resolvedPortablePath).Hash.ToLowerInvariant()
}

$manifest = [ordered]@{
    version = $Version
    installerUrl = $InstallerUrl
    portableUrl = $(if ($PortableUrl) { $PortableUrl } else { $null })
    portableSha256 = $portableHash
    releaseNotes = $ReleaseNotes
    publishedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    sha256 = $hash
    mandatory = [bool]$Mandatory
}

$outputDirectory = Split-Path -Parent $OutputPath
if ($outputDirectory) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$manifest | ConvertTo-Json -Depth 4 | Set-Content -Path $OutputPath -Encoding UTF8
Write-Host "Manifesto salvo em $OutputPath"
