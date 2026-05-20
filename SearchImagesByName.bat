@echo off
chcp 65001 >nul
title ImgSeek - Image Name Searcher

echo.
echo ==========================================
echo        ImgSeek - Image Name Searcher
echo   Finds photos where a name appears
echo ==========================================
echo.

REM -- Resolve script directory (always correct even if run from another folder) --
set "SCRIPT_DIR=%~dp0"
set "CSPROJ=%SCRIPT_DIR%OcrScanner.csproj"

REM -- Check dotnet is installed --
where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: dotnet is not installed or not on PATH.
    echo.
    echo Download .NET 6 SDK from:
    echo   https://dotnet.microsoft.com/download/dotnet/6.0
    echo.
    pause
    exit /b 1
)

REM -- Check the project file exists --
if not exist "%CSPROJ%" (
    echo ERROR: Cannot find OcrScanner.csproj
    echo Expected at: %CSPROJ%
    echo.
    echo Make sure SearchImagesByName.bat is in the same folder as OcrScanner.csproj
    echo.
    pause
    exit /b 1
)

REM -- Ask user for inputs --
set /p "FOLDER=Enter folder path to scan: "
set /p "TERM=Enter name to search for: "

echo.
echo Scanning "%FOLDER%" for "%TERM%"...
echo.

dotnet run --project "%CSPROJ%" -c Release -- "%FOLDER%" "%TERM%"

echo.
pause
