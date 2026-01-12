# Project Structure & Organization

## Root Directory Layout

```
VisualAIAssistant/
├── .kiro/                          # Kiro IDE configuration
├── Assets/                         # Unity 3D project files
├── VisualAIAssistant.WPF/         # Main WPF application
├── MinimalApp/                     # Alternative/simplified app
├── Installer/                      # MSI installer files
├── Library/                        # Unity build artifacts
├── ProjectSettings/                # Unity project settings
├── UserSettings/                   # Unity user settings
├── Logs/                          # Unity build logs
├── Documentation/                  # Project documentation
└── Build Scripts/                  # Batch files for building
```

## Main Application Structure

### VisualAIAssistant.WPF/VisualAIAssistant.WPF/
```
├── AI/                            # AI service providers and management
│   ├── Providers/                 # Claude, OpenAI, Azure providers
│   └── AIServiceManager.cs        # Central AI coordination
├── Conversation/                  # Conversation branching system
│   ├── Models/                    # Conversation data models
│   ├── Services/                  # Branching logic
│   └── UI/                        # Conversation UI components
├── ErrorPrevention/               # Proactive error prevention
├── Execution/                     # Task execution tracking
├── Explanation/                   # Adaptive explanation system
├── Integration/                   # System integration layer
├── Memory/                        # Memory management system
├── Performance/                   # Performance monitoring
├── Privacy/                       # Privacy controls
├── Roles/                         # Multi-role system
├── Rollback/                      # Rollback and restoration
├── Security/                      # Security and permissions
├── Skills/                        # Skill learning system
├── TaskPlanning/                  # Task planning and approval
├── Timeline/                      # Action timeline
├── Tools/                         # File system and utilities
├── UI/                           # Advanced UI features
├── UX/                           # User experience enhancements
├── VoiceSystem/                  # Voice control system
├── Models/                       # Shared data models
├── Tests/                        # Unit and integration tests
└── Documentation/                # Technical documentation
```

## Feature Area Organization

### Core Systems (by folder)
- **AI/**: Multi-provider AI integration with streaming support
- **Memory/**: Encrypted SQLite storage with semantic search
- **Skills/**: Watch Me recording, pattern analysis, skill generation
- **Roles/**: Multiple AI personas with role-specific behaviors
- **Timeline/**: Complete action history with undo/rollback
- **Privacy/**: Local-only mode, topic blocking, privacy controls
- **Security/**: Multi-tier permissions, audit logging, encryption
- **VoiceSystem/**: Speech recognition, TTS, wake word detection

### UI Components (UI/ folder)
- **Advanced Features**: Highlight overlays, diff previews, progress tracking
- **Controls**: Custom WPF controls and user controls
- **Windows**: Main windows, dialogs, and panels
- **Themes**: XAML resource dictionaries for styling

### Integration Layer (Integration/ folder)
- **SystemIntegrator**: Central coordination hub
- **HealthMonitor**: System health and diagnostics
- **EventBus**: Inter-component communication
- **ServiceRegistry**: Dependency injection container

## Unity Project Structure

### Assets/
```
├── Scenes/                        # Unity scenes
├── Scripts/                       # C# scripts for Unity
│   ├── AvatarController.cs        # Main avatar control
│   ├── AvatarManager.cs          # Avatar management
│   ├── UnityWPFBridge.cs         # WPF-Unity communication
│   └── Various avatar scripts...
├── Lightbulb/                     # 3D lightbulb model assets
│   ├── Model/                     # 3D models
│   ├── Materials/                 # Materials and shaders
│   ├── Textures/                  # Texture assets
│   └── Prefab/                    # Unity prefabs
├── Settings/                      # Render pipeline settings
└── TutorialInfo/                  # Unity tutorial assets
```

## Alternative Application (MinimalApp/)

### Simplified Structure
```
MinimalApp/
├── AI/                           # Basic AI integration
├── Voice/                        # Voice system
├── Avatar/                       # Avatar integration
├── UI/                          # Simplified UI
├── Tools/                       # Basic tools
├── Theme/                       # UI theming
└── Core Windows/                # Main application windows
```

## Configuration & Build Files

### Project Files
- **VisualAIVirtualAssistant.slnx**: Main solution file
- **VisualAIAssistant.WPF.csproj**: Primary project file
- **MinimalApp.csproj**: Alternative app project
- **Assembly-CSharp.csproj**: Unity-generated project

### Build Scripts
- **simple-run.bat**: Quick development run
- **run-app.bat**: Build and run with output
- **build-and-install.bat**: Full build and install
- **build-unity-avatar.bat**: Unity-specific build

## Naming Conventions

### Files & Folders
- **PascalCase**: For all C# files, classes, and folders
- **camelCase**: For local variables and private fields
- **UPPER_CASE**: For constants and static readonly fields
- **kebab-case**: For XAML resource keys and some config files

### Namespaces
```csharp
VisualAIAssistant.WPF.{FeatureArea}
VisualAIAssistant.WPF.{FeatureArea}.{SubArea}

Examples:
- VisualAIAssistant.WPF.AI.Providers
- VisualAIAssistant.WPF.Memory.Services
- VisualAIAssistant.WPF.Skills.Learning
```

### Class Organization
```csharp
// 1. Using statements
// 2. Namespace declaration
// 3. Class with regions:
//    - Fields & Properties
//    - Constructor(s)
//    - Public Methods
//    - Private Methods
//    - Event Handlers
//    - IDisposable (if applicable)
```

## Documentation Structure

### Phase Documentation
- **PHASE_X_CHECKPOINT.md**: Completion documentation for each phase
- **PROJECT_SUMMARY.md**: Overall project summary
- **PROJECT_STATUS.md**: Current status and metrics

### Technical Documentation
- **API documentation**: Inline XML comments
- **Architecture docs**: In Documentation/ folder
- **User guides**: Markdown files in root

## Testing Organization

### Test Structure
```
Tests/
├── Unit/                         # Unit tests by feature
├── Integration/                  # Integration tests
├── Examples/                     # Example usage (100+ methods)
└── Performance/                  # Performance benchmarks
```

### Test Naming
- **Test Classes**: `{FeatureName}Tests.cs`
- **Test Methods**: `{MethodName}_{Scenario}_{ExpectedResult}`
- **Example Methods**: `Example_{FeatureName}_{UseCase}`