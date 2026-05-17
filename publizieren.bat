@echo off
setlocal enabledelayedexpansion
set GUI_CSPROJ=GoBDify\GoBDify.csproj
set CLI_CSPROJ=GoBDify.Cli\GoBDify.Cli.csproj
set TFM=net8.0-windows10.0.19041.0
set CLI_FLAGS=-c Release --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=true
set FZ="C:\Program Files\FileZilla FTP Client\filezilla.exe"
set IDENTITY_NAME=software.mielke.gobdify
set IDENTITY_PUBLISHER=CN=Thomas Mielke, O=Thomas Mielke, L=Hamburg, S=Hamburg, C=DE
set UPDATE_BASE=https://easyct.de

rem Version aus CLI-csproj ziehen
set VERFILE=%TEMP%\gobdify_version.txt
powershell -NoProfile -Command "(Select-String -Path '%CLI_CSPROJ%' -Pattern '<Version>(\d+\.\d+\.\d+)' | Select-Object -First 1).Matches.Groups[1].Value" > "%VERFILE%"
set /p VERSION=<"%VERFILE%"
del "%VERFILE%"
if "%VERSION%"=="" set VERSION=0.0.0
set MSIX_VERSION=%VERSION%.0
echo Version: %VERSION%  (MSIX %MSIX_VERSION%)

if not exist dist mkdir dist
if not exist ftp-upload mkdir ftp-upload

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
echo ###############
echo # AppInstaller#
echo ###############
call :writeAppInstaller x64    || goto :fail
call :writeAppInstaller arm64  || goto :fail

echo.
echo Pakete unter dist\:
dir /b dist
echo.
echo Upload-Ordner ftp-upload\:
dir /b ftp-upload
echo.

if exist %FZ% (
  echo Starte FileZilla mit lokalem Ordner ftp-upload ...
  start "" %FZ% --local "%CD%\ftp-upload"
) else (
  echo FileZilla nicht gefunden unter %FZ% - ueberspringe Start.
)
goto :eof

:buildGui
set RID=%~1
set ARCH=%~2
echo === GUI %RID% ===
dotnet publish %GUI_CSPROJ% -f %TFM% -c Release -p:RuntimeIdentifierOverride=%RID% || exit /b 1
set TARGET=dist\gobdify-gui-%VERSION%-windows-%ARCH%.msix
set UPLOAD=ftp-upload\GoBDify_%ARCH%.msix
if exist "%TARGET%" del "%TARGET%"
if exist "%UPLOAD%" del "%UPLOAD%"
powershell -NoProfile -Command "$src = Get-ChildItem -Path 'GoBDify' -Recurse -Filter '*.msix' | Where-Object { $_.FullName -match '%RID%' -and $_.FullName -notmatch 'Dependencies' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1; if (-not $src) { Write-Error 'MSIX-Datei nicht gefunden'; exit 1 }; Copy-Item $src.FullName '%TARGET%'; Copy-Item $src.FullName '%UPLOAD%'" || exit /b 1
echo   -^> %TARGET%
echo   -^> %UPLOAD%
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

:writeAppInstaller
set ARCH=%~1
set AIFILE=ftp-upload\GoBDify_%ARCH%.appinstaller
echo === AppInstaller %ARCH% ===
powershell -NoProfile -ExecutionPolicy Bypass -File tools\write-appinstaller.ps1 -Architecture "%ARCH%" -Version "%MSIX_VERSION%" -IdentityName "%IDENTITY_NAME%" -Publisher "%IDENTITY_PUBLISHER%" -BaseUri "%UPDATE_BASE%" -OutPath "%AIFILE%" || exit /b 1
exit /b 0

:fail
echo FEHLER beim letzten Schritt.
exit /b 1
