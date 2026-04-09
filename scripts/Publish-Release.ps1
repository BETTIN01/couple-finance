param(
    [string]$Version = "",
    [string]$ReleaseNotes = "",
    [string]$PublicBaseUrl = "",
    [string]$ManifestUrl = "",
    [string]$GitHubRepo = "",
    [string]$GitHubToken = "",
    [string]$GitHubTag = "",
    [string]$GitHubReleaseName = "",
    [string]$SupabaseUrl = "",
    [string]$SupabaseAnonKey = "",
    [string]$StorageProjectUrl = "",
    [string]$StorageApiKey = "",
    [string]$StorageBucket = "",
    [string]$StoragePrefix = "couple-finance/stable",
    [switch]$InstallerOnlyManifest,
    [switch]$AllowOfflineDistribution,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repoRoot "CoupleFinance.sln"
$desktopProjectPath = Join-Path $repoRoot "CoupleFinance.Desktop\CoupleFinance.Desktop.csproj"
$setupProjectPath = Join-Path $repoRoot "CoupleFinance.Setup\CoupleFinance.Setup.csproj"
$testsProjectPath = Join-Path $repoRoot "CoupleFinance.Tests\CoupleFinance.Tests.csproj"
$portableOutputPath = Join-Path $repoRoot "artifacts\portable"
$installerOutputPath = Join-Path $repoRoot "artifacts\installer"
$setupPublishPath = Join-Path $installerOutputPath "setup-publish"
$portableZipPath = Join-Path $installerOutputPath "CoupleFinance-portable.zip"
$setupExePath = Join-Path $installerOutputPath "CoupleFinance-Setup.exe"
$manifestOutputPath = Join-Path $installerOutputPath "update-manifest.json"
$portableAppSettingsPath = Join-Path $portableOutputPath "appsettings.json"
$manifestGenerated = $false
$gitHubReleaseUrl = ""

function Get-ProjectVersion {
    param([string]$ProjectPath)

    [xml]$projectXml = Get-Content -Path $ProjectPath -Raw
    $versionNode = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($versionNode)) {
        throw "Nao foi possivel localizar a versao em $ProjectPath."
    }

    return [string]$versionNode
}

function Get-AssemblyVersion {
    param([string]$SemanticVersion)

    $parts = $SemanticVersion.Split(".")
    if ($parts.Count -eq 3) {
        return "$SemanticVersion.0"
    }

    if ($parts.Count -eq 4) {
        return $SemanticVersion
    }

    throw "Versao invalida: $SemanticVersion"
}

function Normalize-BaseUrl {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ""
    }

    return $Value.Trim().TrimEnd("/")
}

function Normalize-GitHubRepo {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ""
    }

    $normalized = $Value.Trim()
    if ($normalized -match "^https?://github\.com/(?<repo>[^/]+/[^/]+?)(?:\.git)?/?$") {
        $normalized = $Matches["repo"]
    }

    if ($normalized.EndsWith(".git", [System.StringComparison]::OrdinalIgnoreCase)) {
        $normalized = $normalized.Substring(0, $normalized.Length - 4)
    }

    $normalized = $normalized.Trim("/")

    if ($normalized -notmatch "^[^/]+/[^/]+$") {
        throw "GitHubRepo deve estar no formato owner/repo ou URL completa do repositiorio."
    }

    return $normalized
}

function Encode-StoragePath {
    param([string]$Value)

    return (($Value -split "/") | Where-Object { $_ -ne "" } | ForEach-Object {
        [Uri]::EscapeDataString($_)
    }) -join "/"
}

function Invoke-Step {
    param(
        [string]$Message,
        [scriptblock]$Action
    )

    Write-Host "==> $Message"
    & $Action
}

function Update-PortableSettings {
    param(
        [string]$SettingsPath,
        [string]$ResolvedManifestUrl,
        [string]$ResolvedSupabaseUrl,
        [string]$ResolvedSupabaseAnonKey
    )

    $config = Get-Content -Path $SettingsPath -Raw | ConvertFrom-Json

    $config.Updates.Enabled = -not [string]::IsNullOrWhiteSpace($ResolvedManifestUrl)
    $config.Updates.CheckOnStartup = $true
    $config.Updates.AutoInstallOnStartup = $true
    $config.Updates.PeriodicCheckIntervalMinutes = 45
    $config.Updates.PreferBackgroundPackage = $true
    $config.Updates.RelaunchAfterInstall = $true
    $config.Updates.ManifestUrl = $ResolvedManifestUrl
    $config.Updates.StartupDelaySeconds = 3
    $config.Updates.DownloadFolderName = "Updates"

    if (-not [string]::IsNullOrWhiteSpace($ResolvedSupabaseUrl)) {
        $config.Supabase.Url = $ResolvedSupabaseUrl
    }

    if (-not [string]::IsNullOrWhiteSpace($ResolvedSupabaseAnonKey)) {
        $config.Supabase.AnonKey = $ResolvedSupabaseAnonKey
    }

    $config | ConvertTo-Json -Depth 10 | Set-Content -Path $SettingsPath -Encoding UTF8
}

function Stop-ProcessesFromFolder {
    param([string]$FolderPath)

    $normalizedFolderPath = [System.IO.Path]::GetFullPath($FolderPath).TrimEnd("\")
    $processes = Get-Process -Name "CoupleFinance.Desktop" -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -and ([System.IO.Path]::GetFullPath($_.Path).StartsWith($normalizedFolderPath, [System.StringComparison]::OrdinalIgnoreCase))
    }

    foreach ($process in $processes) {
        Stop-Process -Id $process.Id -Force
        Start-Sleep -Milliseconds 500
    }
}

function Upload-SupabaseStorageObject {
    param(
        [string]$FilePath,
        [string]$ObjectPath,
        [string]$ProjectUrl,
        [string]$ApiKey,
        [string]$BucketName
    )

    $headers = @{
        Authorization = "Bearer $ApiKey"
        apikey = $ApiKey
        "x-upsert" = "true"
    }

    $encodedObjectPath = Encode-StoragePath -Value $ObjectPath
    $uploadUri = "{0}/storage/v1/object/{1}/{2}" -f $ProjectUrl.TrimEnd("/"), [Uri]::EscapeDataString($BucketName), $encodedObjectPath

    Invoke-RestMethod -Method Post -Uri $uploadUri -Headers $headers -Form @{
        file = Get-Item -LiteralPath $FilePath
    } | Out-Null
}

function Get-HttpStatusCode {
    param([System.Management.Automation.ErrorRecord]$ErrorRecord)

    if ($null -eq $ErrorRecord.Exception -or $null -eq $ErrorRecord.Exception.Response) {
        return -1
    }

    return [int]$ErrorRecord.Exception.Response.StatusCode.value__
}

function Get-GitHubApiHeaders {
    param([string]$Token)

    return @{
        Accept = "application/vnd.github+json"
        Authorization = "Bearer $Token"
        "User-Agent" = "CoupleFinance-ReleasePublisher"
        "X-GitHub-Api-Version" = "2022-11-28"
    }
}

function Invoke-GitHubJsonRequest {
    param(
        [string]$Method,
        [string]$Uri,
        [string]$Token,
        $Body = $null
    )

    $headers = Get-GitHubApiHeaders -Token $Token
    $invokeParams = @{
        Method = $Method
        Uri = $Uri
        Headers = $headers
    }

    if ($null -ne $Body) {
        $invokeParams.Body = ($Body | ConvertTo-Json -Depth 10 -Compress)
        $invokeParams.ContentType = "application/json"
    }

    return Invoke-RestMethod @invokeParams
}

function Get-GitHubReleaseByTag {
    param(
        [string]$Repository,
        [string]$Tag,
        [string]$Token
    )

    $releaseEndpoint = "https://api.github.com/repos/$Repository/releases/tags/$([Uri]::EscapeDataString($Tag))"

    try {
        return Invoke-GitHubJsonRequest -Method Get -Uri $releaseEndpoint -Token $Token
    }
    catch {
        if ((Get-HttpStatusCode -ErrorRecord $_) -eq 404) {
            return $null
        }

        throw
    }
}

function Upsert-GitHubRelease {
    param(
        [string]$Repository,
        [string]$Tag,
        [string]$ReleaseName,
        [string]$Token,
        [string]$Body
    )

    $existingRelease = Get-GitHubReleaseByTag -Repository $Repository -Tag $Tag -Token $Token
    $payload = @{
        tag_name = $Tag
        name = $ReleaseName
        body = $Body
        draft = $false
        prerelease = $false
    }

    if ($null -ne $existingRelease) {
        $releaseEndpoint = "https://api.github.com/repos/$Repository/releases/$($existingRelease.id)"
        return Invoke-GitHubJsonRequest -Method Patch -Uri $releaseEndpoint -Token $Token -Body $payload
    }

    $createEndpoint = "https://api.github.com/repos/$Repository/releases"
    return Invoke-GitHubJsonRequest -Method Post -Uri $createEndpoint -Token $Token -Body $payload
}

function Remove-GitHubReleaseAsset {
    param(
        [string]$Repository,
        [object]$Release,
        [string]$AssetName,
        [string]$Token
    )

    $asset = @($Release.assets) | Where-Object { $_.name -eq $AssetName } | Select-Object -First 1
    if ($null -eq $asset) {
        return
    }

    $headers = Get-GitHubApiHeaders -Token $Token
    $deleteEndpoint = "https://api.github.com/repos/$Repository/releases/assets/$($asset.id)"
    Invoke-RestMethod -Method Delete -Uri $deleteEndpoint -Headers $headers | Out-Null
}

function Get-AssetContentType {
    param([string]$FilePath)

    $extension = [System.IO.Path]::GetExtension($FilePath)
    switch ($extension.ToLowerInvariant()) {
        ".json" { return "application/json" }
        ".zip" { return "application/zip" }
        default { return "application/octet-stream" }
    }
}

function Upload-GitHubReleaseAsset {
    param(
        [object]$Release,
        [string]$AssetName,
        [string]$FilePath,
        [string]$Token
    )

    $uploadBaseUrl = ($Release.upload_url -replace "\{\?name,label\}$", "")
    $uploadUrl = "{0}?name={1}" -f $uploadBaseUrl, [Uri]::EscapeDataString($AssetName)
    $headers = Get-GitHubApiHeaders -Token $Token

    Invoke-RestMethod `
        -Method Post `
        -Uri $uploadUrl `
        -Headers $headers `
        -InFile $FilePath `
        -ContentType (Get-AssetContentType -FilePath $FilePath) | Out-Null
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-ProjectVersion -ProjectPath $desktopProjectPath
}

$assemblyVersion = Get-AssemblyVersion -SemanticVersion $Version
$normalizedPublicBaseUrl = Normalize-BaseUrl -Value $PublicBaseUrl
$normalizedStorageProjectUrl = Normalize-BaseUrl -Value $StorageProjectUrl
$normalizedStoragePrefix = ($StoragePrefix -replace "\\", "/").Trim("/")
$normalizedGitHubRepo = Normalize-GitHubRepo -Value $GitHubRepo
$canUploadToSupabaseStorage =
    -not [string]::IsNullOrWhiteSpace($normalizedStorageProjectUrl) -and
    -not [string]::IsNullOrWhiteSpace($StorageApiKey) -and
    -not [string]::IsNullOrWhiteSpace($StorageBucket)

if ([string]::IsNullOrWhiteSpace($GitHubToken)) {
    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
        $GitHubToken = $env:GITHUB_TOKEN
    }
    elseif (-not [string]::IsNullOrWhiteSpace($env:GH_TOKEN)) {
        $GitHubToken = $env:GH_TOKEN
    }
}

if (-not [string]::IsNullOrWhiteSpace($normalizedGitHubRepo) -and [string]::IsNullOrWhiteSpace($GitHubTag)) {
    $GitHubTag = "v$Version"
}

if (-not [string]::IsNullOrWhiteSpace($normalizedGitHubRepo) -and [string]::IsNullOrWhiteSpace($GitHubReleaseName)) {
    $GitHubReleaseName = "Couple Finance $Version"
}

$installerUrl = ""
$portableUrl = ""
$usesGitHubReleaseUrls = $false

if (-not [string]::IsNullOrWhiteSpace($normalizedGitHubRepo) -and [string]::IsNullOrWhiteSpace($normalizedPublicBaseUrl)) {
    $usesGitHubReleaseUrls = $true

    if ([string]::IsNullOrWhiteSpace($ManifestUrl)) {
        $ManifestUrl = "https://github.com/$normalizedGitHubRepo/releases/latest/download/update-manifest.json"
    }

    $installerUrl = "https://github.com/$normalizedGitHubRepo/releases/download/$GitHubTag/CoupleFinance-Setup.exe"
    $portableUrl = "https://github.com/$normalizedGitHubRepo/releases/download/$GitHubTag/CoupleFinance-portable.zip"
}

if ([string]::IsNullOrWhiteSpace($normalizedPublicBaseUrl) -and $canUploadToSupabaseStorage -and -not $usesGitHubReleaseUrls) {
    $normalizedPublicBaseUrl = "{0}/storage/v1/object/public/{1}/{2}" -f $normalizedStorageProjectUrl, $StorageBucket.Trim("/"), $normalizedStoragePrefix
}

if ([string]::IsNullOrWhiteSpace($ManifestUrl) -and -not [string]::IsNullOrWhiteSpace($normalizedPublicBaseUrl)) {
    $ManifestUrl = "{0}/update-manifest.json" -f $normalizedPublicBaseUrl
}

if (-not [string]::IsNullOrWhiteSpace($normalizedPublicBaseUrl)) {
    $installerUrl = "{0}/packages/{1}/CoupleFinance-Setup.exe" -f $normalizedPublicBaseUrl, $Version
    $portableUrl = "{0}/packages/{1}/CoupleFinance-portable.zip" -f $normalizedPublicBaseUrl, $Version
}

$hasRemoteUpdateChannel =
    -not [string]::IsNullOrWhiteSpace($ManifestUrl) -and
    -not [string]::IsNullOrWhiteSpace($installerUrl) -and
    -not [string]::IsNullOrWhiteSpace($portableUrl)

if (-not $hasRemoteUpdateChannel -and -not $AllowOfflineDistribution) {
    throw "Esta release esta sem canal remoto de atualizacao. Informe -GitHubRepo, -PublicBaseUrl/-ManifestUrl ou configure o upload no Supabase Storage. Use -AllowOfflineDistribution apenas se quiser distribuir um setup sem auto-update."
}

New-Item -ItemType Directory -Force -Path $portableOutputPath | Out-Null
New-Item -ItemType Directory -Force -Path $installerOutputPath | Out-Null
if (Test-Path -LiteralPath $setupPublishPath) {
    Remove-Item -LiteralPath $setupPublishPath -Recurse -Force
}

if (Test-Path -LiteralPath $manifestOutputPath) {
    Remove-Item -LiteralPath $manifestOutputPath -Force
}

Invoke-Step "Build da solucao" {
    dotnet build $solutionPath -c Release "/p:Version=$Version" "/p:AssemblyVersion=$assemblyVersion" "/p:FileVersion=$assemblyVersion"
    if ($LASTEXITCODE -ne 0) {
        throw "Falha no build da solucao."
    }
}

if (-not $SkipTests) {
    Invoke-Step "Testes automatizados" {
        dotnet test $testsProjectPath -c Release --no-build
        if ($LASTEXITCODE -ne 0) {
            throw "Falha nos testes."
        }
    }
}

Invoke-Step "Publicacao portatil do app" {
    Stop-ProcessesFromFolder -FolderPath $portableOutputPath

    if (Test-Path -LiteralPath $portableOutputPath) {
        Remove-Item -LiteralPath $portableOutputPath -Recurse -Force
    }

    dotnet publish $desktopProjectPath `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=false `
        "/p:Version=$Version" `
        "/p:AssemblyVersion=$assemblyVersion" `
        "/p:FileVersion=$assemblyVersion" `
        -o $portableOutputPath

    if ($LASTEXITCODE -ne 0) {
        throw "Falha na publicacao portatil."
    }
}

Invoke-Step "Ajuste do appsettings para distribuicao" {
    Update-PortableSettings `
        -SettingsPath $portableAppSettingsPath `
        -ResolvedManifestUrl $ManifestUrl `
        -ResolvedSupabaseUrl $SupabaseUrl `
        -ResolvedSupabaseAnonKey $SupabaseAnonKey
}

Invoke-Step "Geracao do pacote para update em segundo plano" {
    if (Test-Path -LiteralPath $portableZipPath) {
        Remove-Item -LiteralPath $portableZipPath -Force
    }

    Compress-Archive -Path (Join-Path $portableOutputPath "*") -DestinationPath $portableZipPath -Force
}

Invoke-Step "Build do setup proprio" {
    dotnet publish $setupProjectPath `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        "/p:PayloadZipPath=$portableZipPath" `
        "/p:Version=$Version" `
        "/p:AssemblyVersion=$assemblyVersion" `
        "/p:FileVersion=$assemblyVersion" `
        -o $setupPublishPath

    if ($LASTEXITCODE -ne 0) {
        throw "Falha na publicacao do setup."
    }

    Copy-Item -LiteralPath (Join-Path $setupPublishPath "CoupleFinance.Setup.exe") -Destination $setupExePath -Force
}

if ($hasRemoteUpdateChannel) {
    Invoke-Step "Geracao do manifesto de atualizacao" {
        $manifestPortableUrl = if ($InstallerOnlyManifest) { "" } else { $portableUrl }
        $manifestPortablePath = if ($InstallerOnlyManifest) { "" } else { $portableZipPath }

        & (Join-Path $PSScriptRoot "New-UpdateManifest.ps1") `
            -Version $Version `
            -InstallerUrl $installerUrl `
            -InstallerPath $setupExePath `
            -PortableUrl $manifestPortableUrl `
            -PortablePath $manifestPortablePath `
            -OutputPath $manifestOutputPath `
            -ReleaseNotes $ReleaseNotes

        if ($LASTEXITCODE -ne 0) {
            throw "Falha na geracao do manifesto."
        }

        $script:manifestGenerated = $true
    }
}

if ($canUploadToSupabaseStorage) {
    $installerObjectPath = "{0}/packages/{1}/CoupleFinance-Setup.exe" -f $normalizedStoragePrefix, $Version
    $portableObjectPath = "{0}/packages/{1}/CoupleFinance-portable.zip" -f $normalizedStoragePrefix, $Version
    $manifestObjectPath = "{0}/update-manifest.json" -f $normalizedStoragePrefix

    Invoke-Step "Upload do pacote ZIP para o bucket publico" {
        Upload-SupabaseStorageObject `
            -FilePath $portableZipPath `
            -ObjectPath $portableObjectPath `
            -ProjectUrl $normalizedStorageProjectUrl `
            -ApiKey $StorageApiKey `
            -BucketName $StorageBucket
    }

    Invoke-Step "Upload do setup para o bucket publico" {
        Upload-SupabaseStorageObject `
            -FilePath $setupExePath `
            -ObjectPath $installerObjectPath `
            -ProjectUrl $normalizedStorageProjectUrl `
            -ApiKey $StorageApiKey `
            -BucketName $StorageBucket
    }

    if ($manifestGenerated -and (Test-Path -LiteralPath $manifestOutputPath)) {
        Invoke-Step "Upload do manifesto estavel" {
            Upload-SupabaseStorageObject `
                -FilePath $manifestOutputPath `
                -ObjectPath $manifestObjectPath `
                -ProjectUrl $normalizedStorageProjectUrl `
                -ApiKey $StorageApiKey `
                -BucketName $StorageBucket
        }
    }
}

$canUploadToGitHub =
    -not [string]::IsNullOrWhiteSpace($normalizedGitHubRepo) -and
    -not [string]::IsNullOrWhiteSpace($GitHubToken) -and
    $manifestGenerated

if ($canUploadToGitHub) {
    Invoke-Step "Publicacao no GitHub Releases" {
        $releaseBody = if ([string]::IsNullOrWhiteSpace($ReleaseNotes)) {
            "Release automatica do Couple Finance $Version."
        }
        else {
            $ReleaseNotes
        }

        $release = Upsert-GitHubRelease `
            -Repository $normalizedGitHubRepo `
            -Tag $GitHubTag `
            -ReleaseName $GitHubReleaseName `
            -Token $GitHubToken `
            -Body $releaseBody

        Remove-GitHubReleaseAsset -Repository $normalizedGitHubRepo -Release $release -AssetName "CoupleFinance-Setup.exe" -Token $GitHubToken
        Remove-GitHubReleaseAsset -Repository $normalizedGitHubRepo -Release $release -AssetName "CoupleFinance-portable.zip" -Token $GitHubToken
        Remove-GitHubReleaseAsset -Repository $normalizedGitHubRepo -Release $release -AssetName "update-manifest.json" -Token $GitHubToken

        Upload-GitHubReleaseAsset -Release $release -AssetName "CoupleFinance-Setup.exe" -FilePath $setupExePath -Token $GitHubToken
        Upload-GitHubReleaseAsset -Release $release -AssetName "CoupleFinance-portable.zip" -FilePath $portableZipPath -Token $GitHubToken
        Upload-GitHubReleaseAsset -Release $release -AssetName "update-manifest.json" -FilePath $manifestOutputPath -Token $GitHubToken

        $script:gitHubReleaseUrl = $release.html_url
    }
}
elseif (-not [string]::IsNullOrWhiteSpace($normalizedGitHubRepo) -and $manifestGenerated) {
    Write-Warning "GitHubRepo configurado, mas nenhum token foi informado. A release foi gerada com URLs do GitHub; faca o upload manual de CoupleFinance-Setup.exe, CoupleFinance-portable.zip e update-manifest.json em uma release com a tag $GitHubTag."
}

Write-Host ""
Write-Host "Release pronta."
Write-Host "Versao: $Version"
Write-Host "Setup: $setupExePath"
Write-Host "Pacote ZIP: $portableZipPath"

if ($manifestGenerated -and (Test-Path -LiteralPath $manifestOutputPath)) {
    Write-Host "Manifesto: $manifestOutputPath"
}

if (-not [string]::IsNullOrWhiteSpace($ManifestUrl)) {
    Write-Host "Manifesto remoto esperado: $ManifestUrl"
}

if (-not [string]::IsNullOrWhiteSpace($normalizedGitHubRepo)) {
    Write-Host "Canal GitHub configurado para: https://github.com/$normalizedGitHubRepo"
    Write-Host "Tag da release: $GitHubTag"
}

if (-not [string]::IsNullOrWhiteSpace($gitHubReleaseUrl)) {
    Write-Host "Release publicada: $gitHubReleaseUrl"
}
