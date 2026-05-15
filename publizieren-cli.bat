@echo off
setlocal enabledelayedexpansion
set CSPROJ=GoBDify.Cli\GoBDify.Cli.csproj
set FLAGS=-c Release --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=true

rem Version aus csproj ziehen (robust via PowerShell)
for /f "delims=" %%a in ('powershell -NoProfile -Command "(Select-String -Path '%CSPROJ%' -Pattern '<Version>(\d+\.\d+\.\d+)' ^| Select-Object -First 1).Matches.Groups[1].Value"') do set VERSION=%%a
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
echo === %RID% ===
dotnet publish %CSPROJ% -r %RID% %FLAGS% || exit /b 1
set SRC=GoBDify.Cli\bin\Release\net8.0\%RID%\publish\%BIN%
set ZIP=dist\gobdify-cli-%VERSION%-%PLATFORM%-%ARCH%.zip
if exist "%ZIP%" del "%ZIP%"
powershell -NoProfile -Command "Compress-Archive -Path '%SRC%' -DestinationPath '%ZIP%'" || exit /b 1
echo   -^> %ZIP%
exit /b 0

:fail
echo FEHLER beim letzten Schritt.
exit /b 1
