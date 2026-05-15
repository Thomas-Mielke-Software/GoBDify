@echo off
setlocal enabledelayedexpansion
set CSPROJ=GoBDify.Cli\GoBDify.Cli.csproj
set FLAGS=-c Release --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=true

rem Version aus csproj ziehen (PowerShell -> Temp-Datei)
set VERFILE=%TEMP%\gobdify_version.txt
powershell -NoProfile -Command "(Select-String -Path '%CSPROJ%' -Pattern '<Version>(\d+\.\d+\.\d+)' | Select-Object -First 1).Matches.Groups[1].Value" > "%VERFILE%"
set /p VERSION=<"%VERFILE%"
del "%VERFILE%"
if "%VERSION%"=="" set VERSION=0.0.0
echo Version: %VERSION%

if not exist dist mkdir dist

call :build win-x64      windows  x64    gobdify.exe || goto :fail
call :build win-arm64    windows  arm64  gobdify.exe || goto :fail
call :build linux-x64    linux    x64    gobdify     || goto :fail
call :build linux-arm64  linux    arm64  gobdify     || goto :fail
call :build osx-x64      macos    x64    gobdify     || goto :fail
call :build osx-arm64    macos    arm64  gobdify     || goto :fail

echo.
echo Fertig. Pakete unter dist\:
dir /b dist
goto :eof

:build
set RID=%~1
set PLATFORM=%~2
set ARCH=%~3
set BIN=%~4
set OUTDIR=publish\cli-%RID%
echo === %RID% ===
dotnet publish %CSPROJ% -r %RID% %FLAGS% -o "%OUTDIR%" || exit /b 1
set ZIP=dist\gobdify-cli-%VERSION%-%PLATFORM%-%ARCH%.zip
if exist "%ZIP%" del "%ZIP%"
powershell -NoProfile -Command "Compress-Archive -Path '%OUTDIR%\%BIN%' -DestinationPath '%ZIP%'" || exit /b 1
echo   -^> %ZIP%
exit /b 0

:fail
echo FEHLER beim letzten Schritt.
exit /b 1
