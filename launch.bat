@echo off
echo ========================================
echo Atlas AI Launcher
echo ========================================
echo.
echo Building and launching Atlas AI...
dotnet build "AtlasAI\AtlasAI.csproj" --configuration Debug -v q
start "" "AtlasAI\bin\Debug\net8.0-windows\win-x64\VisualAIAssistant.exe"
