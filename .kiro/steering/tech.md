# Technology Stack & Build System

## Primary Technologies

### Frontend & UI
- **WPF (Windows Presentation Foundation)**: Primary UI framework using .NET 8.0-windows
- **XAML**: Declarative UI markup with data binding and styling
- **Unity 3D Engine**: Avatar rendering and 3D visualization (Unity 6000.3.2f1)
- **C# Language Version**: Latest (C# 12.0)

### Backend & Runtime
- **.NET 8.0**: Target framework with Windows-specific features
- **SQLite**: Local database with AES-256 encryption for data persistence
- **System.Speech**: Windows Speech Recognition integration
- **NAudio**: Audio processing and voice handling

### AI & External Services
- **Multi-Provider AI Support**:
  - Anthropic Claude API
  - OpenAI API
  - Azure OpenAI
  - Google AI
  - Local model support for privacy mode

### Key NuGet Packages
```xml
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="System.Data.SQLite.Core" Version="1.0.118" />
<PackageReference Include="System.Speech" Version="8.0.0" />
<PackageReference Include="System.Drawing.Common" Version="8.0.0" />
<PackageReference Include="WPF-UI" Version="3.0.5" />
<PackageReference Include="NAudio" Version="2.2.1" />
<PackageReference Include="System.Management" Version="8.0.0" />
<PackageReference Include="System.ServiceProcess.ServiceController" Version="8.0.0" />
```

## Build System

### Solution Structure
- **Main Solution**: `VisualAIVirtualAssistant.slnx` (Visual Studio solution)
- **Primary Project**: `VisualAIAssistant.WPF/VisualAIAssistant.WPF/VisualAIAssistant.WPF.csproj`
- **Unity Project**: `Assets/` folder with Unity-specific scripts
- **Alternative App**: `MinimalApp/MinimalApp.csproj` (secondary implementation)

### Common Build Commands

#### Development Build
```bash
# Build main application
dotnet build "VisualAIAssistant.WPF\VisualAIAssistant.WPF\VisualAIAssistant.WPF.csproj" --configuration Debug

# Run application directly
dotnet run --project "VisualAIAssistant.WPF\VisualAIAssistant.WPF" --configuration Debug

# Build minimal app alternative
dotnet build "MinimalApp\MinimalApp.csproj" --configuration Debug
```

#### Quick Run Scripts
```bash
# Simple development run
simple-run.bat

# Build and run with full output
run-app.bat

# Build and install
build-and-install.bat
```

#### Production Build
```bash
# Self-contained executable
dotnet publish "VisualAIAssistant.WPF\VisualAIAssistant.WPF\VisualAIAssistant.WPF.csproj" ^
  --configuration Release ^
  --runtime win-x64 ^
  --self-contained true ^
  --single-file true
```

### Unity Integration
- **Unity Version**: 6000.3.2f1
- **Target Framework**: .NET Standard 2.1 for Unity scripts
- **Build Target**: Windows Standalone (64-bit)
- **Scripting Backend**: Mono
- **API Compatibility**: .NET Standard 2.1

### Testing
```bash
# Run unit tests
dotnet test

# Run specific test project
dotnet test "TestClaude\TestClaude.csproj"
```

## Development Environment

### Required Tools
- **Visual Studio 2022** (recommended) or Visual Studio Code
- **Unity Hub** with Unity 6000.3.2f1
- **.NET 8.0 SDK**
- **Windows 10/11** (target platform)

### Optional Tools
- **WiX Toolset** (for MSI installer creation)
- **PowerShell** (for automation scripts)

## Architecture Patterns

### Design Patterns Used
- **MVVM (Model-View-ViewModel)**: WPF UI architecture
- **Event-Driven Architecture**: Inter-component communication
- **Provider Pattern**: AI service abstraction
- **Repository Pattern**: Data access layer
- **Command Pattern**: Action execution and undo/redo
- **Observer Pattern**: State change notifications

### Code Organization
- **Namespace Convention**: `VisualAIAssistant.WPF.{FeatureArea}`
- **Async/Await**: Extensive use for non-blocking operations
- **Dependency Injection**: Service registration and resolution
- **Interface Segregation**: Clean abstractions for testability