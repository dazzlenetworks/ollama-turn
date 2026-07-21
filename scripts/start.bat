:: Ollama Turn launcher

:: Created 7/21/2026


@echo off

setlocal


:: Resolve the project root (the parent of this scripts folder)
pushd "%~dp0.."


:: Variables
set "EXE=bin\Debug\net9.0-windows\OllamaTurn.exe"


:: Build on first run if the exe isn't there yet
if not exist "%EXE%" (
    echo Building Ollama Turn for the first time...
    dotnet build -c Debug
    if errorlevel 1 (
        echo.
        echo Build failed. Make sure the .NET SDK is installed.
        popd
        pause
        exit /b 1
    )
)


:: Launch (this is a wrapper: let the app take over, no success chatter or pause)
start "" "%EXE%"

popd
exit /b 0
