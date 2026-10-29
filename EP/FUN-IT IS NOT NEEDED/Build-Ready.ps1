$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet run --project Verdant.Checks -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Gameplay checks failed.' }
    dotnet publish Verdant.Game -c Release -r win-x64 --self-contained false -o Ready
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    Write-Host 'Done: Ready\VerdantGuard.exe (requires .NET Desktop Runtime)'
} finally { Pop-Location }
