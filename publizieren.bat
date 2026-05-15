@echo off
setlocal enabledelayedexpansion
set CSPROJ=GoBDify\GoBDify.csproj
set TFM=net8.0-windows10.0.19041.0

rem Version aus csproj ziehen
set VERFILE=%TEMP%\gobdify_version.txt
powershell -NoProfile -Command "(Select-String -Path '%CSPROJ%' -Pattern '<Version>(\d+\.\d+\.\d+)' | Select-Object -First 1).Matches.Groups[1].Value" > "%VERFILE%"
set /p VERSION=<"%VERFILE%"
del "%VERFILE%"
if "%VERSION%"=="" set VERSION=0.0.0
echo Version: %VERSION%

if not exist dist mkdir dist

call :build win10-x64    x64    || goto :fail
call :build win10-arm64  arm64  || goto :fail

echo.
echo Fertig. Pakete unter dist\:
dir /b dist
goto :eof

:build
set RID=%~1
set ARCH=%~2
echo === %RID% ===
dotnet publish %CSPROJ% -f %TFM% -c Release -p:RuntimeIdentifierOverride=%RID% || exit /b 1
set ZIP=dist\gobdify-gui-%VERSION%-windows-%ARCH%.zip
if exist "%ZIP%" del "%ZIP%"
rem MSIX-Paketverzeichnis dynamisch finden (Build-Pfad variiert je SDK)
powershell -NoProfile -Command "$d = Get-ChildItem -Path 'GoBDify' -Recurse -Directory -Filter '*_Test' ^| Where-Object { $_.FullName -match '%RID%' } ^| Sort-Object LastWriteTime -Descending ^| Select-Object -First 1; if (-not $d) { Write-Error 'MSIX-Verzeichnis nicht gefunden'; exit 1 }; Compress-Archive -Path ($d.FullName + '\*') -DestinationPath '%ZIP%'" || exit /b 1
echo   -^> %ZIP%
exit /b 0

:fail
echo FEHLER beim letzten Schritt.
exit /b 1
