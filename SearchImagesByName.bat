@echo off
chcp 65001 >nul
title Image Name Searcher

echo ╔══════════════════════════════════════╗
echo ║        Image Name Searcher           ║
echo ║  Finds photos where a name appears   ║
echo ╚══════════════════════════════════════╝
echo.

set /p "FOLDER=Enter folder path to scan: "
set /p "NAME=Enter name to search for: "

if "%FOLDER%"=="" (echo ERROR: No folder entered. & pause & exit /b 1)
if "%NAME%"=="" (echo ERROR: No name entered. & pause & exit /b 1)

set "GALLERY=%TEMP%\img_search_%NAME%_gallery.html"
set "SCANNER=C:\Users\aadit\.gemini\antigravity\OcrScanner\OcrScanner.csproj"

echo.
echo Scanning "%FOLDER%" for "%NAME%"...
echo.

dotnet run --project "%SCANNER%" -c Release -- "%FOLDER%" "%NAME%" "%GALLERY%"

if exist "%GALLERY%" (
    echo.
    echo Opening gallery...
    start "" "%GALLERY%"
)

echo.
pause
