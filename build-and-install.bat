@echo off
echo ========================================
echo Atlas AI - Build and Install
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
echo Build complete!
echo.
echo Executable location: AtlasAI\bin\Debug\net8.0-windows\win-x64\VisualAIAssistant.exe
pause
