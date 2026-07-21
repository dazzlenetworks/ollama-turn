:: Ollama Turn build (Debug)

:: Created 7/21/2026


@echo off

setlocal


:: Welcome
echo This script builds Ollama Turn in the Debug configuration.
echo.


:: Resolve the project root (the parent of this scripts folder)
pushd "%~dp0.."


:: Build
echo Building...
echo.
dotnet build -c Debug
set "exitCode=%errorlevel%"

popd


:: Exit
if "%exitCode%"=="0" (
    echo.
    echo Operation completed successfully
) else (
    echo.
    echo Build failed
)

echo.
@pause
exit /b %exitCode%
