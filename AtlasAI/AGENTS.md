cat <<'EOF' > AGENTS.md

\# AGENTS.md — Atlas-Ai (WPF/.NET 8) Codex Instructions



\## Repo facts (do not change)

\- This is a WPF desktop app targeting .NET 8 (AtlasAI.csproj).

\- App entry is App.xaml/App.xaml.cs.

\- Current UX uses multiple Window screens (MainWindow, ChatWindow, SettingsWindow, SystemControlWindow, etc.).

\- There are many feature folders (AI, Agent, SecuritySuite, SystemControl, Voice, UI, Theme, Tools, etc.).



\## Mission

Stabilize AtlasAI into a single “one app” shell with modular screens, without breaking existing functionality.

Prefer vertical slices and incremental refactors.



\## Non-negotiable rules

\- Do not add new NuGet packages unless explicitly requested.

\- MVVM preferred. Avoid business logic in code-behind; code-behind may remain for view-only concerns (animations, window chrome).

\- No new top-level folders unless justified in a short plan.

\- Preserve existing windows/features unless instructed to delete. Prefer deprecate/route through the shell.

\- Never commit secrets or tokens. Keep local-only settings in user profile or excluded config.

\- Keep UI responsive: long work must be async and cancellable where applicable.



\## Architecture direction (authoritative)

1\) Introduce a Core spine:

&nbsp;  - Core/AppState.cs

&nbsp;  - Core/NavigationService.cs

&nbsp;  - Core/ModuleRegistry.cs

&nbsp;  - Core/CommandRouter.cs (optional)

2\) Establish a single navigation pattern:

&nbsp;  - Preferred: MainWindow hosts a ContentControl/Frame and swaps Views (Pages/UserControls) via NavigationService.

&nbsp;  - Existing standalone windows can be kept temporarily, but new features must be navigable inside MainWindow.

3\) Features must be delivered as a vertical slice:

&nbsp;  - View (XAML)

&nbsp;  - ViewModel

&nbsp;  - Service/logic

&nbsp;  - Registered route/menu in Core navigation



\## Build / verification

\- After changes, run: `dotnet build`

\- Do not edit compiled output folders (bin/, obj/, publish/, win-x64 output). Always edit source.



\## Output requirements (every task)

\- Brief plan (bullets)

\- Files changed (list)

\- How to test (exact steps)

\- Build result (pass/fail)

EOF



