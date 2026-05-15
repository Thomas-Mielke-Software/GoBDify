@echo off
setlocal enabledelayedexpansion
set GUI_CSPROJ=GoBDify\GoBDify.csproj
set CLI_CSPROJ=GoBDify.Cli\GoBDify.Cli.csproj
set TFM=net8.0-windows10.0.19041.0
set CLI_FLAGS=-c Release --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=true

rem Version aus dem CLI-csproj ziehen (CLI und GUI halten beide <Version> synchron)
set VERFILE=%TEMP%\gobdify_version.txt
powershell -NoProfile -Command "(Select-String -Path '%CLI_CSPROJ%' -Pattern '<Version>(\d+\.\d+\.\d+)' | Select-Object -First 1).Matches.Groups[1].Value" > "%VERFILE%"
set /p VERSION=<"%VERFILE%"
del "%VERFILE%"
if "%VERSION%"=="" set VERSION=0.0.0
echo Version: %VERSION%

if not exist dist mkdir dist

echo.
echo ###############
echo #  MAUI/GUI   #
echo ###############
call :buildGui win10-x64    x64    || goto :fail
call :buildGui win10-arm64  arm64  || goto :fail

echo.
echo ###############
echo #    CLI      #
echo ###############
call :buildCli win-x64      windows  x64    gobdify.exe || goto :fail
call :buildCli win-arm64    windows  arm64  gobdify.exe || goto :fail
call :buildCli linux-x64    linux    x64    gobdify     || goto :fail
call :buildCli linux-arm64  linux    arm64  gobdify     || goto :fail
call :buildCli osx-x64      macos    x64    gobdify     || goto :fail
call :buildCli osx-arm64    macos    arm64  gobdify     || goto :fail

echo.
echo Fertig. Pakete unter dist\:
dir /b dist
goto :eof

:buildGui
set RID=%~1
set ARCH=%~2
echo === GUI %RID% ===
dotnet publish %GUI_CSPROJ% -f %TFM% -c Release -p:RuntimeIdentifierOverride=%RID% || exit /b 1
set ZIP=dist\gobdify-gui-%VERSION%-windows-%ARCH%.zip
if exist "%ZIP%" del "%ZIP%"
powershell -NoProfile -Command "$d = Get-ChildItem -Path 'GoBDify' -Recurse -Directory -Filter '*_Test' ^| Where-Object { $_.FullName -match '%RID%' } ^| Sort-Object LastWriteTime -Descending ^| Select-Object -First 1; if (-not $d) { Write-Error 'MSIX-Verzeichnis nicht gefunden'; exit 1 }; Compress-Archive -Path ($d.FullName + '\*') -DestinationPath '%ZIP%'" || exit /b 1
echo   -^> %ZIP%
exit /b 0

:buildCli
set RID=%~1
set PLATFORM=%~2
set ARCH=%~3
set BIN=%~4
set OUTDIR=publish\cli-%RID%
echo === CLI %RID% ===
dotnet publish %CLI_CSPROJ% -r %RID% %CLI_FLAGS% -o "%OUTDIR%" || exit /b 1
set ZIP=dist\gobdify-cli-%VERSION%-%PLATFORM%-%ARCH%.zip
if exist "%ZIP%" del "%ZIP%"
powershell -NoProfile -Command "Compress-Archive -Path '%OUTDIR%\%BIN%' -DestinationPath '%ZIP%'" || exit /b 1
echo   -^> %ZIP%
exit /b 0

:fail
echo FEHLER beim letzten Schritt.
exit /b 1
