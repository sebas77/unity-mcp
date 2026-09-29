[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath,

    [switch]$IncludeTests,

    [switch]$Clean,

    [ValidateSet('Assets', 'Package')]
    [string]$Destination = 'Assets',

    [switch]$Remove
)

$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourceRoot = Join-Path $repoRoot 'CustomTools\OpenCodeUnityUI'
$projectRoot = [System.IO.Path]::GetFullPath($ProjectPath)
$assetsPath = Join-Path $projectRoot 'Assets'
$packagePath = Join-Path $projectRoot 'Packages\unity-mcp'
$destinationPath = if ($Destination -eq 'Package') {
    Join-Path $packagePath 'CustomTools\OpenCodeUnityUI'
} else {
    Join-Path $assetsPath 'Editor\OpenCodeUnityUI'
}

if ((Test-Path -LiteralPath $sourceRoot -PathType Container) -eq $false) {
    throw "Canonical source directory does not exist: $sourceRoot"
}

if ((Test-Path -LiteralPath $assetsPath -PathType Container) -eq $false) {
    throw "ProjectPath is not a Unity project with an Assets directory: $projectRoot"
}

if ($Destination -eq 'Package' -and (Test-Path -LiteralPath $packagePath -PathType Container) -eq $false) {
    throw "The local MCP package directory does not exist: $packagePath"
}

if ($Remove) {
    if (Test-Path -LiteralPath $destinationPath) {
        Remove-Item -LiteralPath $destinationPath -Recurse -Force
    }
    $destinationMeta = "$destinationPath.meta"
    if (Test-Path -LiteralPath $destinationMeta -PathType Leaf) {
        Remove-Item -LiteralPath $destinationMeta -Force
    }
    $destinationParent = Split-Path $destinationPath -Parent
    if ((Test-Path -LiteralPath $destinationParent -PathType Container) -and
        @(Get-ChildItem -LiteralPath $destinationParent -Force).Count -eq 0) {
        Remove-Item -LiteralPath $destinationParent -Force
        $parentMeta = "$destinationParent.meta"
        if (Test-Path -LiteralPath $parentMeta -PathType Leaf) {
            Remove-Item -LiteralPath $parentMeta -Force
        }
    }
    [pscustomobject]@{
        project = $projectRoot.Replace('\\', '/')
        destination = $destinationPath.Replace('\\', '/')
        removed = $true
    } | ConvertTo-Json -Depth 3
    exit 0
}

if ($Clean -and (Test-Path -LiteralPath $destinationPath)) {
    Remove-Item -LiteralPath $destinationPath -Recurse -Force
}

New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
Copy-Item -Path (Join-Path $sourceRoot 'Editor\*') -Destination $destinationPath -Recurse -Force

if ($IncludeTests) {
    $testDestination = Join-Path $destinationPath 'Tests'
    New-Item -ItemType Directory -Path $testDestination -Force | Out-Null
    Copy-Item -Path (Join-Path $sourceRoot 'Tests\*') -Destination $testDestination -Recurse -Force
}

$copiedFiles = Get-ChildItem -LiteralPath $destinationPath -File -Recurse |
    ForEach-Object { $_.FullName.Replace('\\', '/') } |
    Sort-Object

[pscustomobject]@{
    project = $projectRoot.Replace('\\', '/')
    destination = $destinationPath.Replace('\\', '/')
    destination_kind = $Destination
    included_tests = [bool]$IncludeTests
    file_count = @($copiedFiles).Count
    files = @($copiedFiles)
} | ConvertTo-Json -Depth 4
