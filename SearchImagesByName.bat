@echo off
chcp 65001 >nul
title ImgSeek - Image Name Searcher

echo.
echo ==========================================
echo        ImgSeek - Image Name Searcher
echo   Finds photos where a name appears
echo ==========================================
echo.

set /p "FOLDER=Enter folder path to scan: "
set /p "TERM=Enter name to search for: "

echo.
echo Scanning "%FOLDER%" for "%TERM%"...
echo.

REM -- Find the script's own directory so paths are always correct --
set "SCRIPT_DIR=%~dp0"
set "CSPROJ=%SCRIPT_DIR%OcrScanner.csproj"

REM -- Check that the project file exists --
if not exist "%CSPROJ%" (
    echo ERROR: Cannot find OcrScanner.csproj at:
    echo   %CSPROJ%
    echo Make sure this .bat file is in the same folder as OcrScanner.csproj
    pause
    exit /b 1
)

REM -- Check dotnet is installed --
where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: dotnet is not installed or not on PATH.
    echo Download .NET 6 SDK from: https://dotnet.microsoft.com/download/dotnet/6.0
    pause
    exit /b 1
)

REM -- Build first, then run --
echo Building project...
dotnet build "%CSPROJ%" -c Release -o "%SCRIPT_DIR%bin\Release" >nul 2>&1
if errorlevel 1 (
    echo Build failed. Running with dotnet run instead...
    dotnet run --project "%CSPROJ%" -c Release -- "%FOLDER%" "%TERM%"
) else (
    dotnet run --project "%CSPROJ%" -c Release -- "%FOLDER%" "%TERM%"
)

echo.
pause
