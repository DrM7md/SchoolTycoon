# Market research: comparable games (2026-11-07)

> **ملخص عربي:** دراسة ألعاب مشابهة ودروس قابلة للنقل لقرارات التصميم، دون نسخ أي لعبة.

Notes for design decisions. Do not copy any game; borrow transferable lessons only.

## Closest comparables
| Game | Strength to learn from | Main criticism |
| --- | --- | --- |
| Two Point Campus | Course → room requirements, yearly intake/graduation, warm readable UI, star goals | Too easy, shallow late game, DRM/crashes |
| Let's School | Timetable & teacher-subject assignment, exam-driven reputation | Repetitive early timetable, low replayability |
| Academia: School Simulator | Deep customization, student needs, emergent incidents | Plain 2D art, school can feel like a prison |
| Prison Architect | Overlays, schedules, objectives economy | Slow late game |
| Planet Zoo / JWE / Parkitect | Beautiful 3D, cinematic camera, stylized low-poly scales well | Fiddly building, late-game CPU load, repetitive missions |
| RimWorld | Adaptive event storyteller, characters with traits | — |

## Borrow
- Tiered (bronze/silver/gold) scenario goals.
- Short, auto-fillable timetable and teacher-subject fit.
- Map overlays: satisfaction, coverage, crowding, noise.
- A few named teachers/students with traits driving events.
- Seeded, state-weighted event director (keeps Core deterministic).
- Visible payoff days: exams, graduation.

## Gaps we can own
- Parents as a third party: admissions interviews, complaints, fee negotiation, sibling discounts, scholarships.
- Admissions funnel: marketing → inquiries → tours → applications → waitlist, with local competitors.
- Middle East / bilingual setting: international curricula, Ramadan schedule, buses, inspection ratings unlocking higher fees. Real RTL UI is rare in the genre.
- Realistic private-school finance: payroll, unpaid fees, loans, teacher retention.

## Avoid
- No depth after the first hour — systems must interact (fees → parent mix → behavior → reputation).
- Mid/late-game repetition — vary scenario goals, rare events.
- Fiddly 3D building — grid snapping and prefab buildings; decoration optional.
- Crowd performance — LOD and simplified off-screen agents planned early.
- Cold "prison" look — warm, stylized, student-centred art; no photorealism.
- DRM/online requirements and mobile pay-to-wait patterns.

Review figures for the school sims came from web sources (vaporlens, Metacritic, SteamPulse, OpenCritic, KeenGamer); genre-leader notes are unverified general knowledge.
