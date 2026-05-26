param(
    [Parameter(Mandatory = $true)]
    [string] $ManifestPath,

    [string] $PackageZipPath = ''
)

function Convert-UnicodeEscape {
    param([Parameter(Mandatory = $true)][string] $Value)
    return [System.Text.RegularExpressions.Regex]::Unescape($Value)
}

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$manifestText = [System.IO.File]::ReadAllText($ManifestPath, $utf8NoBom)
$manifest = $manifestText | ConvertFrom-Json
$newLine = [System.Environment]::NewLine

$manifest.Name = Convert-UnicodeEscape '\u65e5\u968f\u4f34\u4fa3'
$manifest.InternalName = 'RouletteBuddy'
$manifest.Description = (Convert-UnicodeEscape '\u5e2e\u52a9\u4f60\u8bb0\u5f55\u6bcf\u65e5\u968f\u673a\u4efb\u52a1\u3001\u5bfc\u51fa\u62a5\u544a\uff0c\u5e76\u5b9e\u65f6\u540c\u6b65\u5230 DungeonLogger\u3002') + $newLine + $newLine + 'Help you to record your daily roulettes, export reports, and sync with DungeonLogger in real time.'
$manifest.Punchline = (Convert-UnicodeEscape '\u81ea\u52a8\u8bb0\u5f55\u6bcf\u65e5\u968f\u673a\u4efb\u52a1\uff0c\u5305\u62ec\u6307\u5bfc\u8005\u968f\u673a\u4efb\u52a1\u3002') + $newLine + $newLine + 'Auto record your daily roulettes including mentor roulettes.'
$manifest.Tags = @(
    (Convert-UnicodeEscape '\u968f\u673a\u4efb\u52a1'),
    (Convert-UnicodeEscape '\u6307\u5bfc\u8005'),
    'roulette',
    'mentor',
    'plugin'
)

[System.IO.File]::WriteAllText($ManifestPath, ($manifest | ConvertTo-Json -Depth 10), $utf8NoBom)

# Rename manifest file to match InternalName (Dalamud expects <InternalName>.json inside the zip)
$manifestDir = Split-Path -Parent $ManifestPath
$renamedManifestPath = Join-Path -Path $manifestDir -ChildPath "$($manifest.InternalName).json"
if ([System.IO.File]::Exists($renamedManifestPath)) {
    Remove-Item -LiteralPath $renamedManifestPath -Force
}
Rename-Item -LiteralPath $ManifestPath -NewName "$($manifest.InternalName).json" -Force
$ManifestPath = $renamedManifestPath

if (-not [string]::IsNullOrWhiteSpace($PackageZipPath)) {
    $outputDirectory = Split-Path -Parent $ManifestPath
    if ((Test-Path -LiteralPath $outputDirectory) -and (Test-Path -LiteralPath (Split-Path -Parent $PackageZipPath))) {
        if (Test-Path -LiteralPath $PackageZipPath) {
            Remove-Item -LiteralPath $PackageZipPath -Force
        }

        $packageFiles = Get-ChildItem -LiteralPath $outputDirectory -File |
            Where-Object { $_.Name -notin @('latest.zip', 'release_notes.md') } |
            Select-Object -ExpandProperty FullName

        if ($packageFiles.Count -gt 0) {
            Compress-Archive -LiteralPath $packageFiles -DestinationPath $PackageZipPath -Force
        }
    }
}
