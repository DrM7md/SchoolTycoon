# First open in the Unity Editor

This project was written by hand without the editor. These steps need the editor and must be done once, then committed.

1. Open with Unity 6 LTS (`6000.0.x`). Let the Package Manager resolve `Packages/manifest.json`; accept package version bumps if prompted.
2. **Console check before anything else.** Look for plugin import errors in `Assets/Plugins/Core`, especially missing or duplicate assemblies. `System.Runtime.CompilerServices.Unsafe` must be present (verified 2026-11-08: URP does not ship it). Open the project via the short junction `D:\sdt`, because long paths break some URP imports. Do not edit the DLLs there; fix the copy list in `tools/CopyCoreToUnity.ps1` instead.
3. **URP:** Create > Rendering > URP Asset (with Universal Renderer) in `Assets/_Project/Settings`. Assign it in Project Settings > Graphics > Default Render Pipeline and in Project Settings > Quality for every quality level.
4. **Input:** Project Settings > Player > Other Settings > Active Input Handling = **Input System Package (New)**. The editor restarts. Select `Assets/_Project/Settings/SchoolDirectorTycoon.inputactions`, confirm it imports (maps Camera, Build, UI), and enable "Generate C# Class" if code will use it.
5. **Localization:** Window > Asset Management > Localization Tables. Create Localization Settings in `Assets/_Project/Localization`, add locales `en` (default) and `ar`, then create String Table Collections `UI`, `Sim` and `Onboarding`.
6. Disable Unity Analytics / cloud services (Project Settings > Services). The game stays offline.
7. Window > General > Test Runner > EditMode > Run All. All tests in `SDT.Tests.EditMode` must pass on Mono.
8. Commit every generated `.meta` file and the new `ProjectSettings/*.asset` files. Never commit `Library/`, `Temp/`, `Logs/` or `UserSettings/`.
