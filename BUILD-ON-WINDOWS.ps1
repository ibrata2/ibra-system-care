# Builds IBRA SYSTEM CARE on your own Windows computer: the app, a self-test, and the installer.
# Needs: .NET 10 SDK (https://dotnet.microsoft.com/download) and Inno Setup 6 (https://jrsoftware.org/isdl.php).
# Run in PowerShell from this folder:   powershell -ExecutionPolicy Bypass -File .\BUILD-ON-WINDOWS.ps1
param([string]$Version = "1.0.0")
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "1/4  Publishing the app $Version…" -ForegroundColor Yellow
dotnet publish src/IbraCare.App/IbraCare.App.csproj -c Release -r win-x64 --self-contained -p:Version=$Version -p:DebugType=none -o build/publish
if ($LASTEXITCODE) { throw "dotnet publish failed" }

Write-Host "2/4  Self-test on this computer (reads only)…" -ForegroundColor Yellow
$p = Start-Process build/publish/IbraSystemCare.exe -ArgumentList "--selftest", "build/selftest.txt" -Wait -PassThru
Get-Content build/selftest.txt
if ($p.ExitCode -ne 0) { throw "self-test failed (see build/selftest.txt)" }

Write-Host "3/4  Installer…" -ForegroundColor Yellow
$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (!(Test-Path $iscc)) { throw "Inno Setup 6 is not installed" }
$arabic = "${env:ProgramFiles(x86)}\Inno Setup 6\Languages\Arabic.isl"
if (!(Test-Path $arabic)) {
  Write-Host "Arabic.isl is missing from Inno Setup; downloading it needs an elevated PowerShell:" -ForegroundColor Red
  Write-Host "Invoke-WebRequest https://raw.githubusercontent.com/jrsoftware/issrc/main/Files/Languages/Unofficial/Arabic.isl -OutFile `"$arabic`""
  throw "Arabic.isl missing"
}
Push-Location installer
& $iscc /Q /DAppVersion=$Version "/DSource=..\build\publish" IbraSystemCare.iss
if ($LASTEXITCODE) { Pop-Location; throw "ISCC failed" }
Pop-Location

Write-Host "4/4  Portable zip…" -ForegroundColor Yellow
Compress-Archive -Path build/publish/* -DestinationPath build/installer/IBRA-System-Care-Portable-x64.zip -Force
Get-ChildItem build/installer | Format-Table Name, Length
Write-Host "Done: build\installer\IBRA-System-Care-Setup.exe" -ForegroundColor Green
