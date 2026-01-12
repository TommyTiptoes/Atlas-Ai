# Atlas AI Design System Rules

This document defines the design system for integrating Figma designs with the Atlas AI WPF application.

## Technology Stack

- **Framework**: WPF (.NET 8.0-windows)
- **Markup**: XAML with data binding
- **Styling**: ResourceDictionary-based theming
- **3D Rendering**: Unity 3D Engine for avatar visualization

## Design Token Locations

### Colors
- **File**: `AtlasAI/Theme/Colors.xaml`
- **Format**: XAML ResourceDictionary with Color and SolidColorBrush resources

### Typography
- **File**: `AtlasAI/Theme/Typography.xaml`
- **Format**: XAML Styles targeting TextBlock

### Controls
- **File**: `AtlasAI/Theme/Controls.xaml`
- **Format**: XAML ControlTemplates and Styles

## Color Palette

### Background Colors (Deep Space Theme)
| Token | Hex | Usage |
|-------|-----|-------|
| AtlasBgDeep | #050508 | Deepest background |
| AtlasBgDark | #08080c | Dark background |
| AtlasBgMedium | #0c0c12 | Medium background |
| AtlasBgLight | #12121a | Light background |
| AtlasSurface | #16161f | Surface elements |
| AtlasSurfaceHover | #1a1a24 | Hover state |
| AtlasSurfaceActive | #1e1e28 | Active/pressed state |

### Accent Colors (Violet/Cyan Spectrum)
| Token | Hex | Usage |
|-------|-----|-------|
| AtlasViolet | #8b5cf6 | Primary accent |
| AtlasCyan | #22d3ee | Secondary accent |
| AtlasTeal | #2dd4bf | Tertiary accent |
| AtlasBlue | #3b82f6 | Info/links |

### Semantic Colors
| Token | Hex | Usage |
|-------|-----|-------|
| AtlasSuccess | #22c55e | Success states |
| AtlasWarning | #f59e0b | Warning states |
| AtlasError | #ef4444 | Error states |

### Text Colors
| Token | Hex | Usage |
|-------|-----|-------|
| AtlasTextPrimary | #f1f5f9 | Primary text |
| AtlasTextSecondary | #94a3b8 | Secondary text |
| AtlasTextMuted | #64748b | Muted/disabled text |
| AtlasTextDisabled | #475569 | Disabled text |

## Typography Scale

### Font Families
```xaml
<FontFamily x:Key="AtlasFontPrimary">Segoe UI Variable, Segoe UI, Arial</FontFamily>
<FontFamily x:Key="AtlasFontMono">Cascadia Code, Consolas, Courier New</FontFamily>
```

### Text Styles
| Style | Size | Weight | Usage |
|-------|------|--------|-------|
| AtlasH1 | 20px | SemiBold | App titles |
| AtlasH2 | 16px | SemiBold | Section headers |
| AtlasH3 | 14px | SemiBold | Subsection headers |
| AtlasBody | 14px | Normal | Primary body text |
| AtlasBodySecondary | 13px | Normal | Secondary text |
| AtlasCaption | 11px | Normal | Small labels |
| AtlasCode | 13px | Normal | Code snippets (mono) |

## Component Patterns

### Buttons
- **AtlasPrimaryButton**: Green gradient, white text, 8px corner radius
- **AtlasSecondaryButton**: Surface background, 6px corner radius
- **AtlasIconButton**: Transparent, 36x36px, 6px corner radius
- **AtlasPillButton**: Cyan accent, 16px corner radius (pill shape)

### Cards
- Use `AtlasCard` style with glass gradient background
- 8px corner radius
- 1px border with AtlasBorderBrush

### Input Fields
- Use `AtlasTextBox` style
- 6px corner radius
- Cyan focus border color
- Surface hover background

## Figma to WPF Translation

When converting Figma designs to WPF:

1. **Colors**: Map Figma colors to existing Atlas tokens. Do not create new colors unless absolutely necessary.

2. **Spacing**: Use consistent spacing values (4, 8, 12, 16, 20, 24, 32px)

3. **Corner Radius**: Standard values are 4, 6, 8, 16px

4. **Shadows**: Use DropShadowEffect with:
   - Color: #000000
   - BlurRadius: 8-16
   - ShadowDepth: 2-6
   - Opacity: 0.2-0.4

5. **Gradients**: Prefer existing gradient brushes:
   - AtlasSpaceGradient (backgrounds)
   - AtlasAccentGradient (violet to cyan)
   - AtlasGlassGradient (glass effects)

## Asset Management

- **Icons**: Use Segoe Fluent Icons or Segoe MDL2 Assets font
- **Images**: Store in project resources, reference via pack:// URIs
- **Animations**: Lottie JSON files in `AtlasAI/Animations/`

## Component Library Location

UI components are organized in:
- `AtlasAI/Controls/` - Custom controls
- `AtlasAI/UI/` - UI panels and windows
- `AtlasAI/Theme/` - Styles and templates

## Code Connect Mapping

When mapping Figma components to code:
- Button components → `AtlasPrimaryButton`, `AtlasSecondaryButton`, `AtlasPillButton`
- Input fields → `AtlasTextBox`
- Dropdowns → `AtlasComboBox`
- Cards/containers → `AtlasCard` style on Border
- Text → Apply appropriate `AtlasH1`, `AtlasBody`, etc. styles

## Best Practices

1. Always use DynamicResource for theme-aware bindings
2. Prefer existing styles over inline properties
3. Maintain glassmorphism aesthetic with translucent surfaces
4. Use subtle animations for state transitions
5. Ensure accessibility with sufficient color contrast
