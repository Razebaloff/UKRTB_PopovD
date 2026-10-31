param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'Exports32K')
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'App/LP_PopovD_App.csproj'
dotnet build $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать приложение.' }

$app = Join-Path $PSScriptRoot 'App/bin/Release/net10.0-windows/LP_PopovD_App.dll'
$destination = [System.IO.Path]::GetFullPath($OutputDirectory)
Write-Host '32K: 30720 x 17280'
dotnet $app --export-all $destination 30720
if ($LASTEXITCODE -ne 0) { throw 'Ошибка экспорта 32K.' }
Write-Host "Готово: $destination"
