$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet run --project Verdant.Checks -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Gameplay checks failed.' }
    dotnet publish Verdant.Game -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Write-Host 'Done: publish\VerdantGuard.exe (Windows x64; .NET installation not required)'
} finally { Pop-Location }
