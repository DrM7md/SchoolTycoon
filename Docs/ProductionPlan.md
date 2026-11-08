# Unity 6 Production Plan (draft, 2026-11-07)

> **ملخص عربي:** خطة إنتاج عميل Unity 6: الركائز، وشريحة العرض، والمراحل حتى 1.0، والمخاطر. الحملة ذات 30 يومًا نموذج أولي فقط؛ النموذج طويل المدى في [نموذج البيانات 3](DataModelV3.md).

Companion to [UnityMigration.md](UnityMigration.md) and [MarketResearch.md](MarketResearch.md). Solo developer, ~25 h/week.

## Pillars
**Fantasy:** "I founded a small private school and I'm turning it into the most respected campus in town — and I can see it grow."
1. **Readable consequences** — every decision shows its cause in the next day's report.
2. **A campus you're proud of** — placement is creative; the world reflects how the school is doing.
3. **Calm, deterministic strategy** — same seed + decisions = same result; fully offline.
4. **Built for two languages** — English first, Arabic RTL as a peer, not a retrofit.

## Loops
- **Core (one day, 30–90 s):** read dashboard → build / hire / set fees → End Day → read report → resolve incident.
- **Meta (30-day campaign — prototype only):** 40 → 65+ students, reputation 70+, cash ≥ 0.
- **Long-term target (owner decision):** consecutive academic years on a Qatar calendar, individual teachers and student cohorts — see [DataModelV3.md](DataModelV3.md). Also: academic year and terms, scenarios with tiered goals, parents and admissions funnel (see MarketResearch).

## Core portability (verified in code)
| Issue | Location | Fix |
| --- | --- | --- |
| Primary constructors (C# 12) | `SaveStore`, `RuleException` | Explicit constructors |
| Collection expression `[]` | `Simulation.cs:15` | `ImmutableArray<DayReport>.Empty` |
| `readonly record struct` (C# 10) | `CampusLayout.cs:5-6` | `readonly struct` + manual equality |
| Implicit usings | csproj | Disable; explicit `using` |
| `record`/`init` | all records | `IsExternalInit` polyfill under `#if NETSTANDARD2_1` |
| `File.Move(..., overwrite: true)` | `SaveStore.cs:31` | `File.Replace` when target exists, else `File.Move` (keeps atomic replace) |
| System.Text.Json 9 options | `SaveStore.cs` | Ship STJ 9.x + deps in `Plugins/Core`; use Mono backend for 1.0 or a source-generated `JsonSerializerContext` |
| Hard-coded Arabic exception text | `RuleException`, `SaveStore` | **Done (2026-11-07):** `RuleCode` on `RuleException`; Unity localizes by code. Save error codes (`SaveErrorCode`) in progress |

Status: multi-target `net10.0;netstandard2.1` and `LangVersion` 9 are done. Running the existing Core tests against the `netstandard2.1` build is in progress; until it lands, tests run on `net10.0` only.

## Vertical slice — acceptance criteria
One URP scene, full 30-day prototype campaign, English only, final art for Classroom, placeholders for the rest.
1. Terrain with 14×12 grid and reserved path; placement via `CampusLayout` + `Simulation.Build`; rejections show localized `RuleCode` text.
2. Camera: smooth pan (WASD, edge, RMB drag), wheel zoom, Q/E rotate, clamped pitch; keyboard tile selection as in CampusWorld.md.
3. HUD: hire/fire, tuition, day report, incident modal — all strings from String Tables.
4. **Parity test:** a scripted 30-day decision list gives byte-identical final state JSON in Unity PlayMode and in the .NET tests.
5. Loads WPF `Version: 2` saves; v1 migrates.
6. ≥120 fps at 1080p on GTX 1060-class, ≥60 fps on Iris Xe, with 150 cosmetic students.
7. Offline: no Unity Services/Analytics packages (CI check).

## Milestones (~26–28 weeks)
| Phase | Weeks | Deliverables |
| --- | --- | --- |
| Pre-production | 3 | Core multi-target, `RuleCode`, JSON decision; Unity 6 LTS skeleton (asmdefs, packages); greybox camera; art style test (3 options × 1 building); performance budget; Arabic UI Toolkit spike |
| Vertical slice | 6–8 | Criteria above; go/no-go on art direction |
| Alpha (parity with WPF 0.3) | 8 | All 5 facilities in final art, day/night, NavMesh students, save slots and menus, onboarding, options |
| Beta | 6 | Arabic table + RTL, playtests, balance, accessibility, parity test in CI |
| Release 1.0 | 3 | DRM-free offline build, local crash log, installer, retire WPF |

## No audio

Owner decision (2026-11-08): the game ships with **no sound at all** — no music, SFX or voice. Every event, warning and result must read visually (animation, color, icons, text, highlights). No audio assets, no audio settings/menu, no AudioSources. The built-in `com.unity.modules.audio` engine module stays only because other packages may depend on it; it is unused.

## Unity architecture
```
Unity/SchoolDirectorTycoon/Assets/
  Plugins/Core/          Core DLL + JSON deps (copied by build script, read-only)
  _Project/Scripts/Runtime/
    Bridge/  SDT.Bridge  GameSession, CommandBus, SaveService (only asmdef that references Core)
    World/   SDT.World   GridView, BuildingView, CameraRig, Students
    UI/      SDT.UI      UXML controllers, presenters
    Data/    SDT.Data    ScriptableObject definitions (presentation only)
  _Project/Tests/        EditMode, PlayMode
```
- **GameSession** owns the immutable `SchoolState`; `Dispatch` swaps state and raises `StateChanged(old,new)` or `CommandRejected(code)`. Views diff old vs new.
- **DayClock** (0/1×/2×/4×) calls `AdvanceDay`, pauses on incidents; visuals interpolate from day progress and never write state. Auto-save after each successful day.
- **Balance numbers stay in Core.** ScriptableObjects hold prefabs, icons, string keys, VFX; an editor validator ensures one per enum value.
- **Input System** action maps (Camera, Build, UI); rebinds in `settings.json`, separate from campaign saves.
- **Camera:** Cinemachine 3 orbital rig clamped to grid bounds, plus Reset view.
- **UI:** UI Toolkit with flex layouts and a root `.rtl` class; validate Arabic shaping in the pre-production spike, fall back to uGUI + TextMeshPro + RTLTMPro if it fails.
- **Localization:** `com.unity.localization`, tables `UI`, `Sim`, `Onboarding`; Smart Strings with named args, never concatenation. Seed `ar` from existing `InterfaceText.cs` and Core strings.

## Art direction
- Options: (A) saturated low-poly, (B) clean soft-PBR "diorama" with tilt-shift feel, (C) semi-realistic. **Recommended: B** — readable at tycoon zoom, achievable solo, good under day/night.
- One consistent Asset Store kit for nature/props/characters; custom or kitbashed hero facilities for identity. Log every license in [ThirdParty.md](ThirdParty.md).
- Budgets (1080p mid GPU): <300k tris, <400 draw calls, buildings ≤15k tris LOD0 (+50%/15% LODs), students ~1.5k tris instanced, 2K atlases, <2 GB VRAM, <16 ms CPU.
- Pipeline: Blender → FBX, `SM_/SK_/M_/T_` naming, `AssetPostprocessor` for scale/LOD/material presets.

## Saves
- `SaveStore` in Core remains the only serializer; schema stays v2. Camera/quality/keybinds go to `settings.json`.
- Commit golden v1/v2 WPF saves under `tests/Fixtures`, loaded by both .NET and Unity tests.
- Any grid change → v3 + migration + tests. The planned v3 schema is in [DataModelV3.md](DataModelV3.md).
- Save hardening in progress: `.bak` fallback, retries, `SaveErrorCode`/`SaveDataException`, golden fixtures.

## Testing & CI
- .NET tests on net10.0; a netstandard2.1 test run is being added (in progress).
- Unity Test Framework: Bridge, localization key coverage (every `RuleCode`/enum has `en` and `ar`), SO validator, 30-day parity, save round-trip.
- GameCI on a Windows runner or nightly local script; gates: Core tests, Unity tests, network-package scan, build size.

## Top risks
| Risk | Mitigation |
| --- | --- |
| JSON/IL2CPP stripping breaks saves | Mono for 1.0, or source-generated context + `link.xml`; parity test |
| Determinism drift between runtimes | Integer rules, custom RNG; golden-state test on both |
| Arabic in UI Toolkit fails | Week-1 spike; uGUI + RTLTMPro fallback |
| Art scope creep | Lock style B at vertical slice; 5 hero buildings for 1.0 |
| Core too thin for a tycoon | Prove fun at slice scope; expand Core test-first post-1.0 |
| Logic leaks into MonoBehaviours | asmdef wall — only Bridge sees Core |
| Solo burnout / two clients | Freeze WPF features now (bug fixes only) |
