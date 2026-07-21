@echo off
rem Launch Ollama Turn. Builds first if the exe isn't there yet.
cd /d "%~dp0"
set "EXE=bin\Debug\net9.0-windows\OllamaTurn.exe"
if not exist "%EXE%" (
  echo Building Ollama Turn for the first time...
  dotnet build -c Debug || goto :error
)
start "" "%EXE%"
exit /b 0

:error
echo.
echo Build failed. Make sure the .NET SDK is installed.
pause
