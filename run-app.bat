@echo off
echo ========================================
echo Atlas AI - Build and Run
echo ========================================
echo.

echo Building Atlas AI...
dotnet build "AtlasAI\AtlasAI.csproj" --configuration Debug --verbosity minimal

if %errorLevel% neq 0 (
    echo Build failed! Please check for errors.
    pause
    exit /b 1
)

echo.
echo Running Atlas AI...
echo.

set "EXE_PATH=AtlasAI\bin\Debug\net8.0-windows\win-x64\VisualAIAssistant.exe"

if not exist "%EXE_PATH%" (
    echo Error: Executable not found at %EXE_PATH%
    pause
    exit /b 1
)

echo Starting Atlas AI...
start "" "%EXE_PATH%"

echo.
echo Atlas AI should now be running!
pause
