@echo off
setlocal enabledelayedexpansion
set CSPROJ=GoBDify\GoBDify.csproj
set TFM=net8.0-windows10.0.19041.0

rem Version (Display) aus csproj ziehen
for /f "tokens=2 delims=<>" %%a in ('findstr "<ApplicationDisplayVersion>" %CSPROJ%') do set VERSION=%%a
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
set MSIXDIR=GoBDify\bin\Release\%TFM%\%RID%\AppPackages
for /d %%d in ("%MSIXDIR%\*Test") do set MSIXFOLDER=%%d
set ZIP=dist\gobdify-gui-%VERSION%-windows-%ARCH%.zip
if exist "%ZIP%" del "%ZIP%"
powershell -NoProfile -Command "Compress-Archive -Path '!MSIXFOLDER!\*' -DestinationPath '%ZIP%'" || exit /b 1
echo   -^> %ZIP%
exit /b 0

:fail
echo FEHLER beim letzten Schritt.
exit /b 1
