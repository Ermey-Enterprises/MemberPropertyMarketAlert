@echo off
REM MemberPropertyMarketAlert Test Runner
REM Usage: membertests [TestType]
REM Example: membertests All

setlocal

set PROJECT_PATH=C:\Users\micha\Projects\MemberPropertyMarketAlert
set SCRIPT_PATH=%PROJECT_PATH%\scripts\Test-Local.ps1

REM Default to 'All' if no parameter provided
set TEST_TYPE=%1
if "%TEST_TYPE%"=="" set TEST_TYPE=All

REM Check if project directory exists
if not exist "%PROJECT_PATH%" (
    echo ERROR: Project directory not found: %PROJECT_PATH%
    exit /b 1
)

REM Check if test script exists
if not exist "%SCRIPT_PATH%" (
    echo ERROR: Test script not found: %SCRIPT_PATH%
    exit /b 1
)

echo.
echo 🧪 Running MemberPropertyMarketAlert Tests...
echo 📍 Project: %PROJECT_PATH%
echo 🎯 Test Type: %TEST_TYPE%
echo.

REM Change to project directory and run tests
pushd "%PROJECT_PATH%"
powershell.exe -ExecutionPolicy Bypass -File "%SCRIPT_PATH%" -TestType %TEST_TYPE%
popd

endlocal
