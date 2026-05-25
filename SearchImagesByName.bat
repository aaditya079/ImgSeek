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

REM -- Locate the built EXE --
set "EXE=%SCRIPT_DIR%bin\Release\net6.0-windows10.0.19041.0\win-x64\ImgSeek.exe"

if not exist "%EXE%" (
    echo ImgSeek.exe not found at:
    echo   %EXE%
    echo.
    echo Attempting to build the project first...
    echo This may take a minute.
    echo.

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

    dotnet build "%CSPROJ%" -c Release -r win-x64
    if errorlevel 1 (
        echo.
        echo ERROR: Build failed. See errors above.
        pause
        exit /b 1
    )
    echo.
)

if not exist "%EXE%" (
    echo ERROR: ImgSeek.exe still not found after build.
    echo Expected at: %EXE%
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

"%EXE%" "%FOLDER%" "%TERM%"

echo.
pause
