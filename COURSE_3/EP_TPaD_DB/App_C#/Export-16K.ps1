param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'Exports16K')
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'App/LP_PopovD_App.csproj'
dotnet build $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать приложение.' }

$app = Join-Path $PSScriptRoot 'App/bin/Release/net10.0-windows/LP_PopovD_App.dll'
$destination = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($destination) | Out-Null
$screens = [ordered]@{
    'entry' = 'Entry_16K.png'
    'login' = 'Login_16K.png'
    'registration' = 'Registration_16K.png'
    'menu' = 'Menu_16K.png'
}

# Последовательный экспорт ограничивает расход памяти при 132,7 млн пикселей.
foreach ($item in $screens.GetEnumerator()) {
    $target = Join-Path $destination $item.Value
    Write-Host "16K: $($item.Value)"
    dotnet $app "--export-$($item.Key)" $target 15360
    if ($LASTEXITCODE -ne 0) { throw "Ошибка экспорта: $($item.Value)" }
}
Write-Host "Готово: $destination"
