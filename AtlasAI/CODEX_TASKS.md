cat <<'EOF' > CODEX\_TASKS.md

\# CODEX\_TASKS.md — Safe Work Items (AtlasAI)



\## Rule

Only pick one task at a time. Each task must end with dotnet build passing.



\### Task 1 — Repository hygiene (must do first)

\- Ensure bin/, obj/, publish/, and win-x64 output are ignored and not tracked.

\- Update .gitignore accordingly and remove tracked artifacts from Git history (git rm --cached).



\### Task 2 — Core spine (minimum)

\- Add Core/AppState + Core/NavigationService + Core/ModuleRegistry.

\- Wire MainWindow to use NavigationService to display one screen inside it.



\### Task 3 — First vertical slice inside MainWindow

\- Implement a “Dashboard” view with real CPU/RAM/Disk metrics (simple, stable).

\- Add navigation button + viewmodel + service.



\### Task 4 — Convert one existing window to a navigable view

\- Convert ChatWindow to ChatView (UserControl/Page) and host it inside MainWindow.

\- Keep old window as fallback if needed, but default route uses MainWindow.



\### Task 5 — “Command Router” skeleton

\- Create a command registry so the Assistant can invoke actions: SwitchMode, QuickScan, OpenSettings, ExplainSelection.

EOF



