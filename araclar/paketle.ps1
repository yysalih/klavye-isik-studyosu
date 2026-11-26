# Yayın paketi üretir: dist\KlavyeIsik-v<sürüm>-win-x64.zip (tek exe, .NET gerektirmez + OKU_BENI.txt)
# Kullanım (depo kökünden):  powershell -ExecutionPolicy Bypass -File araclar\paketle.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

$version = ([xml](Get-Content KlavyeIsik.csproj -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
dotnet publish -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o dist\app -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Derleme başarısız" }

Copy-Item araclar\OKU_BENI.txt dist\app\OKU_BENI.txt -Force
(Get-Content dist\app\OKU_BENI.txt -Raw -Encoding UTF8) -replace 'v\d+\.\d+\.\d+', "v$version" |
    Set-Content dist\app\OKU_BENI.txt -Encoding UTF8

$zip = "dist\KlavyeIsik-v$version-win-x64.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path dist\app\KlavyeIsik.exe, dist\app\OKU_BENI.txt -DestinationPath $zip
"Hazır: $zip ({0:N1} MB)" -f ((Get-Item $zip).Length / 1MB)
