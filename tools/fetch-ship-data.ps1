# Fetches the ship data release that build/ship-data.json pins (T12.5, D113) into .ship-data/<tag>/, in the layout of a
# download (ShipDataStore): manifest.json, ships/**, outlines.json, NOTICE.md. Every file of ships.zip is checked against
# the manifest's hash before the folder gets its name; anything wrong leaves no folder and no .ship-data/current.
# A finished folder is checked again offline and needs no network. The build calls this once (CargoPlan.Core.csproj).
#
# Usage: fetch-ship-data.ps1 -ReadableSchemaVersions 4,5,6 [-Pin <ship-data.json>] [-Source <url base | folder>] [-Target <folder>]
#   -Source: the release download base (default GitHub), or a folder laid out like it, <folder>/<tag>/<asset> (tests).
# Runs in Windows PowerShell 5.1 (the build on Windows, no install needed) and PowerShell 7 (Linux).
# Exit 0 = the release is in place, 1 = it isn't (the reason on stderr).

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ReadableSchemaVersions,
    [string]$Pin,
    [string]$Source = 'https://github.com/MRiediger/sc-cargoplan-shipdata/releases/download',
    [string]$Target
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# 5.1: the progress bar slows Invoke-WebRequest down many times over.
$ProgressPreference = 'SilentlyContinue'

# Defaults here, not in param(): Windows PowerShell 5.1 leaves $PSScriptRoot empty in parameter defaults.
$repo = Split-Path $PSScriptRoot -Parent
if (-not $Pin) { $Pin = Join-Path $repo 'build/ship-data.json' }
if (-not $Target) { $Target = Join-Path $repo '.ship-data' }
$Target = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Target)

# The same contract as the app's (ShipDataVersion, ShipDataManifestReader): -cmatch, \z, ASCII digits.
$tagPattern = '^ships-[0-9]{4}\.(0[1-9]|1[0-2])(\.[1-9][0-9]{0,3})?\z'
$assetPattern = '^[A-Za-z0-9_-][A-Za-z0-9_.-]*\z'
$filePattern = '^(?:outlines\.json|ships/(?:modules/)?[A-Za-z0-9_-]+\.json)\z'
$hashPattern = '^[0-9a-fA-F]{64}\z'
$maxUnpackedBytes = 64MB
$maxNoticeBytes = 64KB
$utf8 = New-Object System.Text.UTF8Encoding $false

# ZipFile, not Expand-Archive: only listed entries reach the disk (zip slip), and 5.1 has no -PassThru to check.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

function Get-Sha256Hex([byte[]]$Data) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return ([System.BitConverter]::ToString($sha.ComputeHash($Data)) -replace '-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Get-Field($Object, [string]$Name, [string]$Where) {
    if ($null -eq $Object -or -not $Object.PSObject.Properties[$Name]) { throw "$Where lacks '$Name'." }
    return $Object.PSObject.Properties[$Name].Value
}

function Read-PinTag {
    if (-not (Test-Path -LiteralPath $Pin -PathType Leaf)) { throw "The pin $Pin is missing." }
    $json = [System.IO.File]::ReadAllText($Pin) | ConvertFrom-Json
    $tag = [string](Get-Field $json 'dataVersion' "The pin $Pin")
    if ($tag -cnotmatch $tagPattern) { throw "The pin $Pin names '$tag', not a release tag like ships-2026.11.1." }
    return $tag
}

# The manifest as the app reads it; returns files as an ordered path -> hash table.
function Read-Manifest([byte[]]$Bytes, [string]$Tag, [int[]]$Schemas) {
    $json = $utf8.GetString($Bytes) | ConvertFrom-Json
    $version = [string](Get-Field $json 'dataVersion' 'manifest.json')
    if ($version -cne $Tag) { throw "manifest.json: dataVersion '$version' is not the pinned $Tag." }
    $schema = [int](Get-Field $json 'schemaVersion' 'manifest.json')
    if ($Schemas -notcontains $schema) { throw "manifest.json: schemaVersion $schema needs a newer app (reads $($Schemas -join ', '))." }
    $asset = [string](Get-Field (Get-Field $json 'assets' 'manifest.json') 'ships' 'manifest.json assets')
    if ($asset -cnotmatch $assetPattern) { throw "manifest.json: assets.ships '$asset' is not a file name of the release." }
    $files = [ordered]@{}
    foreach ($property in (Get-Field $json 'files' 'manifest.json').PSObject.Properties) {
        if ($property.Name -cnotmatch $filePattern) { throw "manifest.json: files has '$($property.Name)', which is not a path of $asset." }
        if ([string]$property.Value -cnotmatch $hashPattern) { throw "manifest.json: the hash of '$($property.Name)' is not a SHA-256." }
        $files[$property.Name] = ([string]$property.Value).ToLowerInvariant()
    }
    foreach ($required in 'ships/hull_a.json', 'outlines.json') {
        if (-not $files.Contains($required)) { throw "manifest.json: files lacks '$required'." }
    }
    return @{ Asset = $asset; Files = $files }
}

# R4 of the T12.5 approval: the notice has no hash in the manifest; present, not empty, not bigger than a notice is.
function Test-Notice([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return 'NOTICE.md is missing' }
    $length = (Get-Item -LiteralPath $Path).Length
    if ($length -eq 0) { return 'NOTICE.md is empty' }
    if ($length -gt $maxNoticeBytes) { return "NOTICE.md has $length bytes, more than $maxNoticeBytes" }
    return $null
}

# A finished folder, offline: its manifest, every listed file's hash, the notice. Returns $null or what is wrong.
function Test-Folder([string]$Folder, [string]$Tag, [int[]]$Schemas) {
    try {
        $manifest = Read-Manifest ([System.IO.File]::ReadAllBytes((Join-Path $Folder 'manifest.json'))) $Tag $Schemas
        foreach ($path in $manifest.Files.Keys) {
            $file = Join-Path $Folder $path
            if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { return "$path is missing" }
            if ((Get-Sha256Hex ([System.IO.File]::ReadAllBytes($file))) -ne $manifest.Files[$path]) { return "$path doesn't match its hash" }
        }
        $notice = Test-Notice (Join-Path $Folder 'NOTICE.md')
        if ($notice) { return $notice }
        return $null
    }
    catch {
        return $_.Exception.Message
    }
}

function Get-Asset([string]$Tag, [string]$Name, [string]$Destination) {
    if ($Source -match '^https?://') {
        # 5.1 runs on .NET Framework, whose default may lack TLS 1.2, which GitHub requires.
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls12
        $url = "$($Source.TrimEnd('/'))/$Tag/$Name"
        try { Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $Destination }
        catch { throw "$Name of $Tag could not be downloaded from $url ($($_.Exception.Message))" }
    }
    else {
        $file = Join-Path (Join-Path $Source $Tag) $Name
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "$Name of $Tag is not in $Source." }
        Copy-Item -LiteralPath $file -Destination $Destination
    }
}

# Only entries the manifest lists, each once, read in pieces and counted (a zip's own sizes are claims), checked by hash.
function Expand-Release([string]$Zip, $Manifest, [string]$Into) {
    $unpacked = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
    $total = [long]0
    try { $archive = [System.IO.Compression.ZipFile]::OpenRead($Zip) }
    catch { throw "$($Manifest.Asset) is not a zip that can be read: $($_.Exception.Message)" }
    try {
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName
            if ($name.EndsWith('/')) { continue }
            if (-not $Manifest.Files.Contains($name)) { throw "$($Manifest.Asset) has '$name', which manifest.json doesn't list." }
            if (-not $unpacked.Add($name)) { throw "$($Manifest.Asset) has '$name' twice." }

            $buffer = New-Object System.IO.MemoryStream
            $stream = $entry.Open()
            try {
                $chunk = New-Object byte[] 81920
                while (($read = $stream.Read($chunk, 0, $chunk.Length)) -gt 0) {
                    $total += $read
                    if ($total -gt $maxUnpackedBytes) { throw "$($Manifest.Asset) unpacks to more than $maxUnpackedBytes bytes." }
                    $buffer.Write($chunk, 0, $read)
                }
            }
            finally { $stream.Dispose() }

            $data = $buffer.ToArray()
            if ((Get-Sha256Hex $data) -ne $Manifest.Files[$name]) { throw "'$name' in $($Manifest.Asset) doesn't match its hash in manifest.json." }
            $file = Join-Path $Into $name
            [void](New-Item -ItemType Directory -Force -Path (Split-Path $file -Parent))
            [System.IO.File]::WriteAllBytes($file, $data)
        }
    }
    finally { $archive.Dispose() }

    foreach ($path in $Manifest.Files.Keys) {
        if (-not $unpacked.Contains($path)) { throw "'$path' is listed in manifest.json but missing from $($Manifest.Asset)." }
    }
    return $unpacked.Count
}

function Write-Current([string]$Folder) {
    [System.IO.File]::WriteAllText((Join-Path $Target 'current'), "$Folder`n", $utf8)
}

$code = 0
$temp = $null
try {
    $tag = Read-PinTag
    $schemas = @($ReadableSchemaVersions -split ',' | ForEach-Object { [int]$_.Trim() })
    $folder = Join-Path $Target $tag

    $fetch = $true
    if (Test-Path -LiteralPath (Join-Path $folder 'manifest.json') -PathType Leaf) {
        $problem = Test-Folder $folder $tag $schemas
        if (-not $problem) {
            Write-Current $folder
            Write-Output "Ship data $tag already there, checked offline, nothing fetched: $folder"
            $fetch = $false
        }
        else {
            Write-Output "Ship data $tag in $folder doesn't check out ($problem); fetching it again."
        }
    }

    if ($fetch) {
        [void](New-Item -ItemType Directory -Force -Path $Target)
        $temp = Join-Path $Target ("$tag.tmp-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
        $download = (New-Item -ItemType Directory -Path (Join-Path $temp 'download')).FullName
        $data = (New-Item -ItemType Directory -Path (Join-Path $temp 'data')).FullName

        Get-Asset $tag 'manifest.json' (Join-Path $download 'manifest.json')
        $manifestBytes = [System.IO.File]::ReadAllBytes((Join-Path $download 'manifest.json'))
        $manifest = Read-Manifest $manifestBytes $tag $schemas
        Get-Asset $tag $manifest.Asset (Join-Path $download $manifest.Asset)
        Get-Asset $tag 'NOTICE.md' (Join-Path $download 'NOTICE.md')
        $notice = Test-Notice (Join-Path $download 'NOTICE.md')
        if ($notice) { throw "$notice in $tag." }
        $bytes = (Get-ChildItem -LiteralPath $download -File | Measure-Object -Property Length -Sum).Sum

        $checked = Expand-Release (Join-Path $download $manifest.Asset) $manifest $data
        Copy-Item -LiteralPath (Join-Path $download 'NOTICE.md') -Destination $data
        # The manifest last, as it came: a folder counts once its manifest is in it (ShipDataStore).
        [System.IO.File]::WriteAllBytes((Join-Path $data 'manifest.json'), $manifestBytes)

        if (Test-Path -LiteralPath $folder) { Remove-Item -LiteralPath $folder -Recurse -Force }
        try { [System.IO.Directory]::Move($data, $folder) }
        catch {
            # Another build fetched the same release at the same time: its folder counts if it checks out.
            if (Test-Folder $folder $tag $schemas) { throw }
        }
        Write-Current $folder
        Write-Output "Ship data $tag fetched: $bytes bytes, $checked files checked against manifest.json, in $folder"
    }
}
catch {
    [Console]::Error.WriteLine("fetch-ship-data: $($_.Exception.Message)")
    $code = 1
}
finally {
    if ($temp -and (Test-Path -LiteralPath $temp)) { Remove-Item -LiteralPath $temp -Recurse -Force }
}

exit $code
