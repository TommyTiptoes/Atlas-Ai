# Atlas AI - Shell Architecture

## Overview

Atlas AI now supports two operational modes:
1. **Floating Avatar Mode** (Default) - The traditional floating butler avatar
2. **Shell Mode** (Experimental) - A unified window with integrated navigation

## Architecture Components

### Core Infrastructure

#### AppState (`Core/AppState.cs`)
- Singleton instance managing global application state
- Properties:
  - `IsShellMode`: Boolean flag for current mode (avatar vs shell)
  - `CurrentUser`: User profile identifier
- Implements INotifyPropertyChanged for data binding

#### NavigationService (`Core/NavigationService.cs`)
- Manages in-shell navigation between feature views
- Methods:
  - `RegisterRoute(key, factory)`: Register a view factory for a route
  - `Navigate(key)`: Navigate to a registered route
  - `CanNavigate(key)`: Check if route exists
- Properties:
  - `CurrentView`: The currently displayed view (bindable)

#### ModuleRegistry (`Core/ModuleRegistry.cs`)
- Centralized registry of available feature modules
- Tracks module metadata (key, display name, icon, description)
- Used for generating navigation menus and feature discovery

### Views

Located in `/Views` directory, these UserControls provide shell-mode interfaces:

- **ChatView** - Bridge to ChatWindow
- **SettingsView** - Bridge to SettingsWindow  
- **SystemControlView** - Bridge to SystemControlWindow

Each view:
- Has a consistent header with icon and title
- Provides a button to open the full-featured Window
- Can be extended to host inline functionality in the future

### MainWindow Dual-Mode Support

#### Floating Avatar Mode (Default)
- Small (200x200) transparent window
- Draggable butler avatar
- Right-click context menu for:
  - Open Chat
  - Open Settings
  - Open System Control
  - Hide Avatar
  - Toggle Shell Mode
  - Exit
- Double-click or Ctrl+Alt+A opens ChatWindow
- Positioned in top-right corner

#### Shell Mode (Experimental)
- Full-sized window (1200x800, maximizable)
- Navigation bar with feature buttons
- ContentControl bound to NavigationService.CurrentView
- Window controls (minimize, close)
- Toggle back to avatar mode via context menu

## Usage

### For End Users

**To Switch to Shell Mode:**
1. Right-click the floating avatar
2. Select "Toggle Shell Mode (Experimental)"
3. The window expands to show the navigation interface

**To Return to Avatar Mode:**
1. Right-click anywhere in shell mode
2. Select "Toggle Shell Mode (Experimental)" again
3. Or minimize and interact with avatar

### For Developers

**Adding a New Feature View:**

1. Create a UserControl in `/Views`:
```csharp
public partial class MyFeatureView : UserControl
{
    public MyFeatureView()
    {
        InitializeComponent();
    }
}
```

2. Register route in MainWindow.RegisterRoutes():
```csharp
_navigationService.RegisterRoute("myfeature", () => new Views.MyFeatureView());
```

3. Register module in MainWindow.RegisterModules():
```csharp
registry.RegisterModule("myfeature", "My Feature", "🎯", "Feature description");
```

4. Add navigation button to MainWindow.xaml (optional):
```xml
<Button Content="🎯 My Feature" Style="{StaticResource NavButton}" Click="NavMyFeature_Click"/>
```

5. Add click handler in MainWindow.xaml.cs:
```csharp
private void NavMyFeature_Click(object sender, RoutedEventArgs e)
{
    _navigationService?.Navigate("myfeature");
}
```

## Design Philosophy

### Minimal Changes
- All existing Windows remain fully functional
- No code duplication - views bridge to existing Windows
- Incremental migration path from Windows to integrated views
- Backward compatible with existing behavior

### Transitional Architecture
Current implementation provides:
- Shell infrastructure in place
- Navigation framework working
- Views serve as launch pads to full Windows
- Preserves all existing functionality

Future evolution can:
- Gradually move functionality from Windows into Views
- Add new features as shell-native views
- Maintain Windows for complex/legacy features

### Benefits
- Single entry point for all features
- Consistent navigation experience
- Reduced window management complexity
- Better for tablet/touch scenarios
- Improved discoverability of features
- Maintains avatar for quick access

## Implementation Notes

### Why Not Full Migration?
- ChatWindow alone is 14,000+ lines of code
- Each Window has complex dependencies
- Risk of breaking existing functionality
- Better to stabilize infrastructure first

### Current Limitations
- Shell views are currently just bridges
- Windows still separate from shell
- Can't embed existing Windows due to WPF limitations
- Navigation doesn't replace Windows (yet)

### Future Roadmap
1. ✅ Core infrastructure (AppState, NavigationService, ModuleRegistry)
2. ✅ Dual-mode MainWindow with shell UI
3. ✅ Bridge views for major features
4. 🔄 Gradually migrate simple features to inline views
5. 🔄 Add new features as shell-native
6. 🔄 Consider UserControl extraction for complex features
7. 🔄 Enhanced navigation (breadcrumbs, back/forward, etc.)

## Testing

**Manual Testing Checklist:**
- [ ] Floating avatar appears in top-right corner
- [ ] Avatar is draggable
- [ ] Double-click avatar opens ChatWindow
- [ ] Ctrl+Alt+A opens ChatWindow
- [ ] Right-click shows context menu
- [ ] "Toggle Shell Mode" switches to shell
- [ ] Shell mode shows navigation bar
- [ ] Navigation buttons switch views
- [ ] View buttons open respective Windows
- [ ] "Toggle Shell Mode" switches back to avatar
- [ ] All Windows open and function correctly
- [ ] No crashes or exceptions

## Files Modified/Added

**Added:**
- `Core/AppState.cs` - Global state management
- `Core/ModuleRegistry.cs` - Feature registry
- `Converters/BooleanToVisibilityConverter.cs` - UI converter
- `Views/ChatView.xaml[.cs]` - Chat bridge view
- `Views/SettingsView.xaml[.cs]` - Settings bridge view
- `Views/SystemControlView.xaml[.cs]` - System Control bridge view
- `SHELL_ARCHITECTURE.md` - This document

**Modified:**
- `MainWindow.xaml` - Added shell mode UI
- `MainWindow.xaml.cs` - Added navigation logic

**Unchanged:**
- All existing Windows (ChatWindow, SettingsWindow, etc.)
- All existing features and functionality
- App.xaml and startup logic
