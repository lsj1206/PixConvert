[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$SkipTests,
    [switch]$AllowDirty
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\PixConvert.csproj"
$testProjectPath = Join-Path $repoRoot "PixConvert.Tests\PixConvert.Tests.csproj"
$releaseRoot = Join-Path $repoRoot "_temp\release"
$publishDir = Join-Path $releaseRoot "publish"
$packageName = "PixConvert-v$Version-$RuntimeIdentifier"
$stageDir = Join-Path $releaseRoot $packageName
$zipPath = Join-Path $releaseRoot "$packageName.zip"
$checksumPath = Join-Path $releaseRoot "SHA256SUMS.txt"
$releaseNotesPath = Join-Path $releaseRoot "RELEASE_NOTES.md"

function Assert-CommandSucceeded {
    param([string]$CommandName)

    if ($LASTEXITCODE -ne 0) {
        throw "$CommandName failed with exit code $LASTEXITCODE."
    }
}

function Copy-RequiredFile {
    param(
        [string]$SourcePath,
        [string]$DestinationDirectory
    )

    if (-not (Test-Path -LiteralPath $SourcePath -PathType Leaf)) {
        throw "Required file is missing: $SourcePath"
    }

    Copy-Item -LiteralPath $SourcePath -Destination $DestinationDirectory -Force
}

function Get-ProjectVersion {
    param([string]$Path)

    [xml]$project = Get-Content -LiteralPath $Path -Raw
    $versions = @($project.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })

    if ($versions.Count -eq 0) {
        throw "Project version was not found in $Path."
    }

    return [string]$versions[0]
}

Push-Location $repoRoot
try {
    $expectedTag = "v$Version"
    $projectVersion = Get-ProjectVersion -Path $projectPath

    if ($projectVersion -ne $Version) {
        throw "Project version is $projectVersion, expected $Version."
    }

    $existingTag = git tag --list $expectedTag
    Assert-CommandSucceeded "git tag --list"

    if ($existingTag) {
        throw "Git tag already exists: $expectedTag"
    }

    if (-not $AllowDirty) {
        $status = git status --short
        Assert-CommandSucceeded "git status"

        if ($status) {
            throw "Working tree must be clean before release packaging. Use -AllowDirty only for local dry-runs."
        }
    }

    if (-not $SkipTests) {
        dotnet test $testProjectPath -v minimal
        Assert-CommandSucceeded "dotnet test"
    }

    if (Test-Path -LiteralPath $releaseRoot) {
        Remove-Item -LiteralPath $releaseRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
    New-Item -ItemType Directory -Path $stageDir -Force | Out-Null

    dotnet publish $projectPath `
        -c Release `
        -r $RuntimeIdentifier `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishTrimmed=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $publishDir
    Assert-CommandSucceeded "dotnet publish"

    Copy-RequiredFile -SourcePath (Join-Path $publishDir "PixConvert.exe") -DestinationDirectory $stageDir
    Copy-RequiredFile -SourcePath (Join-Path $publishDir "libvips-42.dll") -DestinationDirectory $stageDir
    Copy-RequiredFile -SourcePath (Join-Path $repoRoot "README.md") -DestinationDirectory $stageDir
    Copy-RequiredFile -SourcePath (Join-Path $repoRoot "README.ko.md") -DestinationDirectory $stageDir
    Copy-RequiredFile -SourcePath (Join-Path $repoRoot "LICENSE") -DestinationDirectory $stageDir
    Copy-RequiredFile -SourcePath (Join-Path $repoRoot "THIRD-PARTY-NOTICES.md") -DestinationDirectory $stageDir

    $stageFiles = @(Get-ChildItem -LiteralPath $stageDir -Recurse -File)
    $requiredRootFiles = @(
        "PixConvert.exe",
        "libvips-42.dll",
        "README.md",
        "README.ko.md",
        "LICENSE",
        "THIRD-PARTY-NOTICES.md"
    )

    foreach ($fileName in $requiredRootFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $stageDir $fileName) -PathType Leaf)) {
            throw "Release staging is missing required file: $fileName"
        }
    }

    $unexpectedDlls = @($stageFiles | Where-Object { $_.Extension -ieq ".dll" -and $_.Name -ne "libvips-42.dll" })
    if ($unexpectedDlls.Count -gt 0) {
        $names = ($unexpectedDlls | Select-Object -ExpandProperty Name) -join ", "
        throw "Release staging contains unexpected DLL files: $names"
    }

    $forbiddenFiles = @($stageFiles | Where-Object {
        $_.Extension -ieq ".pdb" -or
        $_.Name.EndsWith(".deps.json", [System.StringComparison]::OrdinalIgnoreCase) -or
        $_.Name.EndsWith(".runtimeconfig.json", [System.StringComparison]::OrdinalIgnoreCase)
    })

    if ($forbiddenFiles.Count -gt 0) {
        $names = ($forbiddenFiles | Select-Object -ExpandProperty Name) -join ", "
        throw "Release staging contains forbidden files: $names"
    }

    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in $stageFiles) {
            $entryName = [System.IO.Path]::GetRelativePath($releaseRoot, $file.FullName).Replace("\", "/")
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $zip.Dispose()
    }

    $zipReader = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $zipEntries = @($zipReader.Entries | ForEach-Object { $_.FullName })
    }
    finally {
        $zipReader.Dispose()
    }
    $requiredZipEntries = @($requiredRootFiles | ForEach-Object { "$packageName/$_" })

    foreach ($entry in $requiredZipEntries) {
        if ($zipEntries -notcontains $entry) {
            throw "Release zip is missing required entry: $entry"
        }
    }

    $unexpectedZipDlls = @($zipEntries | Where-Object { $_.EndsWith(".dll", [System.StringComparison]::OrdinalIgnoreCase) -and $_ -ne "$packageName/libvips-42.dll" })
    if ($unexpectedZipDlls.Count -gt 0) {
        throw "Release zip contains unexpected DLL files: $($unexpectedZipDlls -join ', ')"
    }

    $forbiddenZipEntries = @($zipEntries | Where-Object {
        $_.EndsWith(".pdb", [System.StringComparison]::OrdinalIgnoreCase) -or
        $_.EndsWith(".deps.json", [System.StringComparison]::OrdinalIgnoreCase) -or
        $_.EndsWith(".runtimeconfig.json", [System.StringComparison]::OrdinalIgnoreCase)
    })

    if ($forbiddenZipEntries.Count -gt 0) {
        throw "Release zip contains forbidden entries: $($forbiddenZipEntries -join ', ')"
    }

    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $(Split-Path -Leaf $zipPath)" | Set-Content -LiteralPath $checksumPath -Encoding ASCII

    @"
# PixConvert v$Version

- Initial public release
- Static image conversion: JPEG, PNG, BMP, WebP, AVIF
- Animated image conversion: GIF, WebP
- Presets, CPU usage control, and signature-based format detection
- Portable zip layout with settings, presets, and logs stored next to the executable
- ``libvips-42.dll`` is intentionally shipped as an external LGPL-related native library used by the NetVips path
"@ | Set-Content -LiteralPath $releaseNotesPath -Encoding UTF8

    Write-Host "Release package created:"
    Write-Host "  $zipPath"
    Write-Host "  $checksumPath"
    Write-Host "  $releaseNotesPath"
}
finally {
    Pop-Location
}
