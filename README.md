# Revit Family Creator UI

First milestone: an Add-Ins ribbon panel and **Family Creator** button opening one modeless WPF window. Repeated clicks activate the existing window; closing it allows a fresh window next time. The window is owned by Revit and closes during add-in shutdown. This milestone does not edit documents or include sign-in or generation.

## Build and install

Requires Windows, .NET 10 SDK and installed Revit 2027 APIs.

```powershell
dotnet build .\src\FamilyCreator.Revit\FamilyCreator.Revit.csproj -c Release
.\deploy\install.ps1
```

The installer copies only this add-in into `%APPDATA%\Autodesk\Revit\Addins\2027\FamilyCreator` and writes `FamilyCreator.addin` beside it. It does not copy Autodesk DLLs. Close Revit before updating a loaded assembly. Save your work and restart Revit to discover the new add-in if it is already running. Revit may ask you to review the unsigned local add-in on first launch.

## Verification

On 2026-10-30, Release build passed with zero warnings and zero errors against installed Revit 2027 API assemblies. Manifest XML, installed assembly path and installed/source assembly hashes were checked. Live Revit loading and interaction remain pending; the existing Project1 session was left open.

Live acceptance checklist:

- [ ] Start Revit 2027 and verify Add-Ins > Family Creator panel/button appears.
- [ ] Click Family Creator and verify the window is displayed and owned by Revit.
- [ ] Click again: the same window comes forward, with no duplicate.
- [ ] Close and reopen; minimize and click again to restore.
- [ ] Keep the window open while opening/closing a project; the shell remains usable.
- [ ] Close Revit normally and verify the owned window closes.

Revit document operations will need ExternalEvent dispatch when introduced. This shell only opens a window and does not need a transaction or ExternalEvent.
