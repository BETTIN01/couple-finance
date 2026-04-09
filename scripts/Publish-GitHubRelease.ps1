param(
    [string]$Version = "",
    [string]$ReleaseNotes = "",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$githubToken = if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
    $env:GITHUB_TOKEN
}
elseif (-not [string]::IsNullOrWhiteSpace($env:GH_TOKEN)) {
    $env:GH_TOKEN
}
else {
    ""
}

if ([string]::IsNullOrWhiteSpace($githubToken)) {
    throw "Configure GITHUB_TOKEN ou GH_TOKEN no Windows antes de publicar releases automaticamente."
}

$publishReleaseScript = Join-Path $PSScriptRoot "Publish-Release.ps1"

& $publishReleaseScript `
    -Version $Version `
    -ReleaseNotes $ReleaseNotes `
    -GitHubRepo "BETTIN01/couple-finance" `
    -GitHubToken $githubToken `
    -InstallerOnlyManifest `
    -SkipTests:$SkipTests
