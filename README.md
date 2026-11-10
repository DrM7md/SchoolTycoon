# School Director Tycoon · مدير المدرسة

A portrait mobile life sim where you run a private school, day by day. You rent a building, hire teachers, set the school hours, and watch the lesson clock tick. Then you live with your choices: parents' group chats, staff room gossip, crises that last for days, and a conscience that keeps score.

Arabic and English. Fully offline. No audio: every piece of feedback is visual.

<p>
  <img src="Docs/images/life-en-school.png" width="200" alt="School tab with the lesson clock">
  <img src="Docs/images/life-ar-chat-parents.png" width="200" alt="Parents' group chat in Arabic">
  <img src="Docs/images/life-en-month2.png" width="200" alt="Monthly certificate">
  <img src="Docs/images/life-ar-cele-star.png" width="200" alt="Star celebration in Arabic">
</p>

> **Status:** early development and not yet released. The current focus is making the game fun and worth coming back to every day. The first-run tutorial, notifications and the Android and iOS builds come later.

## What you do as director

- **Set up your school:** choose a building, seats, fees, teachers, admins and the school day. Pick one of five scenarios: standard, rescue, village, girls or international.
- **Run the day:** a live lesson clock that you can pause, speed up, or extend. Each morning you do director actions, and each evening you choose how to spend the night: tutoring, renting the gym, parents' night, or going home.
- **Grow:** a training centre and building upgrades that take real time to finish, promotions, Teacher of the Month, and a timetable.
- **Live with consequences:** decision cards, delayed outcomes that return weeks later, a hidden conscience, results day, and multi-day crises.
- **Progress:** a career that lasts across runs, with levels and unlocks, a Hall of Fame, an album, school stars, and a weekly challenge.

## Repository layout

| Path | What it is |
|---|---|
| `src/SchoolDirectorTycoon.Core` | All game rules in plain C# (netstandard2.1 and net10.0). Deterministic: the same state and choices always give the same result. |
| `Unity/SchoolDirectorTycoon` | The Unity 6 mobile client (UI Toolkit). This is the main client going forward. |
| `src/SchoolDirectorTycoon.Desktop` | Older WPF prototype for Windows, kept as a reference until the Unity client reaches parity. |
| `tests/` | Core rule and balance tests, the netstandard check, and the WPF tests. |
| `tools/CopyCoreToUnity.ps1` | Builds Core and copies it into the Unity project. |
| `Docs/` | Design, architecture and system documents. Most of them are written in Arabic. |

## Getting started

Requirements: Windows, the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (version pinned in `global.json`), Unity **6000.0.85f2**, and [Git LFS](https://git-lfs.com).

```bash
git clone https://github.com/DrM7md/SchoolTycoon.git
```

Run the Core tests from the repository root. They include the balance checks:

```bash
dotnet run --project tests/SchoolDirectorTycoon.Tests -c Release -m:1
```

After changing Core, copy it into Unity:

```bash
powershell -ExecutionPolicy Bypass -File tools/CopyCoreToUnity.ps1
```

Then open `Unity/SchoolDirectorTycoon` in Unity Hub and open the `Chronicle` scene in `Assets/_Project/Scenes`. EditMode tests live in `Assets/_Project/Tests/EditMode`.

## Ground rules for contributions

1. **Rules belong in Core.** Unity only shows state and passes in player input. Real time reaches Core only as integer arguments, so results stay reproducible.
2. **No hard-coded text.** Every visible string goes in the localization tables in English and Arabic. Core returns keys and rule codes, never sentences. Check right-to-left layout for anything you add.
3. **Keep old saves working.** If you change the save format, add a migration and a test that loads a save written in the previous version.
4. **No audio, no network.** If you add a dependency, record its version, license and its effect on offline play in [Docs/ThirdParty.md](Docs/ThirdParty.md).
5. **Run the tests** and update [Docs/Changelog.md](Docs/Changelog.md) before you open a pull request.
6. **Original work only.** The game takes inspiration from life sims, but its names, art and text are its own.

Agents and new contributors should start with [AGENTS.md](AGENTS.md), then read [Development](Docs/Development.md), [Architecture](Docs/Architecture.md) and [Unity migration](Docs/UnityMigration.md). Planned work is in the [Roadmap](Docs/Roadmap.md).

Have an idea or found a bug? Open an issue. Small and focused pull requests are easiest to review.

## License

Source-available under the [PolyForm Noncommercial License 1.0.0](LICENSE.md). You may read, run, change and share the code for any noncommercial purpose: learning, personal projects, research or helping this project. Selling the game or a derivative, or publishing it on any store, needs written permission from the owner. See [NOTICE](NOTICE).

Contributions are welcome under the terms in [CONTRIBUTING.md](CONTRIBUTING.md). They let the owner include your work in the commercial release, and you keep the copyright.

---

## بالعربية

**مدير المدرسة** لعبة محاكاة لإدارة مدرسة خاصة على الجوال بالوضع العمودي. تستأجر المبنى وتوظف المعلمين وتحدد وقت الدوام، ثم تعيش يومك مع ساعة الحصص وقروب الأهالي وغرفة المعلمين وقرارات تلاحقك عواقبها. اللعبة بالعربية والإنجليزية، وتعمل دون اتصال، وبلا صوت.

**الحالة:** قيد التطوير ولم تصدر بعد. الأولوية الآن أن تكون اللعبة ممتعة ويعود إليها اللاعب كل يوم.

**للمساهمة:** ابدأ بملف [AGENTS.md](AGENTS.md) ثم [دليل التطوير](Docs/Development.md). القواعد في النواة، ولا نص ثابت في الكود، ويبقى فتح ملفات الحفظ القديمة ممكنًا. شغّل الاختبارات وحدّث [سجل التغييرات](Docs/Changelog.md) قبل طلب الدمج.

**الرخصة:** الكود منشور برخصة PolyForm Noncommercial 1.0.0. يحق لأي أحد قراءته وتشغيله وتعديله لأغراض غير تجارية. أما بيع اللعبة أو نشرها في أي متجر فيحتاج إذنًا مكتوبًا من صاحب المشروع. وبإرسال أي مساهمة يمنح المساهم صاحب المشروع حق استخدامها في النسخة التجارية، ويبقى حق المؤلف له، كما في [CONTRIBUTING.md](CONTRIBUTING.md).
