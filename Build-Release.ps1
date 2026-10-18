#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$GameRoot,
    [string]$Revision = 'HEAD',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'releases')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Run-Native {
    param([string]$Program, [string[]]$Arguments)
    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) { throw ($Program + ': код завершения ' + $LASTEXITCODE) }
}

$repo = $PSScriptRoot
$commit = & git -C $repo rev-parse --verify --end-of-options ($Revision + '^{commit}')
if ($LASTEXITCODE -ne 0) { throw 'Указанная ревизия не является коммитом.' }
$commit = $commit.Trim()
$expectedScript = & git -C $repo rev-parse ($commit + ':Build-Release.ps1')
if ($LASTEXITCODE -ne 0) { throw 'В выбранном коммите нет Build-Release.ps1.' }
$actualScript = & git -C $repo hash-object --path=Build-Release.ps1 $PSCommandPath
if ($LASTEXITCODE -ne 0 -or $actualScript -ne $expectedScript) {
    throw 'Для сборки нужен неизмененный Build-Release.ps1 из выбранного коммита.'
}

$game = (Resolve-Path -LiteralPath $GameRoot).Path
$work = Join-Path $repo ('work\release-' + [guid]::NewGuid().ToString('N'))
$snapshot = Join-Path $work 'source'
[IO.Directory]::CreateDirectory($snapshot) | Out-Null
$sourceZip = Join-Path $work 'source.zip'
Run-Native 'git' @('-C', $repo, 'archive', '--format=zip', ('--output=' + $sourceZip), $commit)
[IO.Compression.ZipFile]::ExtractToDirectory($sourceZip, $snapshot)
$settings = Get-Content -LiteralPath (Join-Path $snapshot 'release-settings.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($entry in $settings.gameFiles) {
    $file = Join-Path $game $entry.path
    if (!(Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.sha256) {
        throw ('Не поддерживается файл игры: ' + $entry.path)
    }
}

$project = Join-Path $snapshot $settings.project
[xml]$projectXml = Get-Content -LiteralPath $project -Raw -Encoding UTF8
$version = [string]$projectXml.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Неверный номер версии проекта.' }
$assembly = [string]$projectXml.Project.PropertyGroup.AssemblyName
$framework = [string]$projectXml.Project.PropertyGroup.TargetFramework
$name = $settings.name + '-' + $version
$output = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($output) | Out-Null
$archive = Join-Path $output ($name + '.zip')
$checksum = $archive + '.sha256'
$record = Join-Path $output ($name + '.release.json')
foreach ($path in @($archive, $checksum, $record)) {
    if (Test-Path -LiteralPath $path) { throw ('Готовый файл уже существует и не будет заменен: ' + $path) }
}

Write-Host ('Сборка сохраненного коммита: ' + $commit)
Write-Host 'Несохраненные в Git изменения в архив не входят.'
Run-Native 'dotnet' @('run', '--project', (Join-Path $snapshot $settings.tests), '-c', 'Release')
Run-Native 'dotnet' @('build', $project, '-c', 'Release', ('-p:GameRoot=' + $game), ('-p:SourceRevisionId=' + $commit), '--nologo')
$dll = Join-Path (Split-Path -Parent $project) ('bin\Release\' + $framework + '\' + $assembly + '.dll')
if (!(Test-Path -LiteralPath $dll)) { throw 'DLL мода не создана.' }
$dllVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll).ProductVersion.Split('+')[0]
if ($dllVersion -ne $version) { throw 'Версия DLL не совпадает с версией проекта.' }
$pluginSource = Get-Content -LiteralPath (Join-Path $snapshot 'source\Plugin.cs') -Raw -Encoding UTF8
if ($pluginSource -notmatch ('const string Version = "' + [regex]::Escape($version) + '";')) {
    throw 'Версия загрузчика в Plugin.cs не совпадает с версией проекта.'
}

$package = Join-Path $work 'package'
$pluginFolder = Join-Path $package ('BepInEx\plugins\' + $settings.mod)
[IO.Directory]::CreateDirectory($pluginFolder) | Out-Null
Copy-Item -LiteralPath $dll -Destination $pluginFolder
Copy-Item -LiteralPath (Join-Path $snapshot 'ПРОЧИТАЙ.md') -Destination $package
$utf8 = [Text.UTF8Encoding]::new($false)
$files = @(Get-ChildItem -LiteralPath $package -File -Recurse | Sort-Object FullName | ForEach-Object {
    [ordered]@{
        path = $_.FullName.Substring($package.Length + 1).Replace('\', '/')
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
})
$manifest = [ordered]@{
    name = $settings.name
    version = $version
    commit = $commit
    steamBuild = $settings.steamBuild
    loader = $settings.loader
    validation = 'Автоматические проверки и сборка Release пройдены. Игра этим скриптом не запускается.'
    files = $files
}
[IO.File]::WriteAllText((Join-Path $package 'release-manifest.json'), ($manifest | ConvertTo-Json -Depth 8), $utf8)
$tempArchive = Join-Path $work ($name + '.zip')
[IO.Compression.ZipFile]::CreateFromDirectory($package, $tempArchive, [IO.Compression.CompressionLevel]::Optimal, $false)
[IO.File]::Copy($tempArchive, $archive, $false)
$archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
[IO.File]::WriteAllText($checksum, ($archiveHash + '  ' + [IO.Path]::GetFileName($archive) + [Environment]::NewLine), [Text.Encoding]::ASCII)
$release = [ordered]@{
    repository = $settings.repository
    name = $settings.name
    version = $version
    commit = $commit
    archive = [IO.Path]::GetFileName($archive)
    sha256 = $archiveHash
    gameplayVerified = $false
}
[IO.File]::WriteAllText($record, ($release | ConvertTo-Json -Depth 4), $utf8)
Write-Host ('Подготовлено локально: ' + $archive)
Write-Host ('SHA256: ' + $archiveHash)
Write-Host 'Публикация, создание тега, установка мода и запуск игры не выполнялись.'
