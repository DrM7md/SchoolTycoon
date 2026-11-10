#nullable enable
using System;
using System.Collections.Generic;
using SchoolDirectorTycoon.Bridge;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolDirectorTycoon.UI
{
    /// <summary>School Life v3 screens: the career on the setup page, day report, month certificate, celebrations,
    /// chat rooms, timetable, Hall of Fame and album. Holds no rules; every number comes from the Bridge.</summary>
    public sealed partial class LifeController
    {
        private const string Lrm = "‎";
        private const float CountUpSeconds = 1.1f;
        private static readonly Color TitleA = new Color(0.48f, 0.11f, 0.22f), TitleB = new Color(0.84f, 0.56f, 0.12f);

        private readonly List<string> timetableDraft = new List<string>();
        private int reportSeenDay = -1;
        private bool giftOpened;
        private string? sheet;
        private int celeTarget, celeShown;
        private float celeRate, celeCarry;
        private bool celeCountCash;
        private bool celeActive;

        /// <summary>True while a celebration, report or sheet covers the school and the clock should wait.</summary>
        public bool OverlayUp =>
            game != null && (sheet != null || (game.Phase != "Setup" && game.CelebrationCount > 0) || Q<VisualElement>("report").ClassListContains("hidden") == false);

        public string? OpenSheetId => sheet;

        private void ResetV3()
        {
            reportSeenDay = game != null && game.Phase == "Evening" ? game.Day : -1;
            giftOpened = false;
            sheet = null;
            timetableDraft.Clear();
        }

        private void WireV3()
        {
            Q<Button>("report-ok").clicked += () => { reportSeenDay = game?.Day ?? -1; Refresh(); };
            Q<Button>("month-ok").clicked += () => game?.ResolveCard("ok");
            Q<Button>("cele-gift").clicked += () => { giftOpened = true; Refresh(); };
            Q<Button>("cele-ok").clicked += () => { giftOpened = false; game?.AckCelebration(); };
            Q<Button>("sheet-close").clicked += () => OpenSheet(null);
            foreach (string gear in new[] { "app-settings", "app-settings-setup" })
            {
                Button button = Q<Button>(gear);
                button.Add(new GearMark());
                button.clicked += () => OpenSheet("app");
            }
            Q<Button>("evening-timetable").clicked += () => OpenSheet("timetable");

            for (int i = 0; i < 5; i++)
            {
                var star = new StarMark();
                star.AddToClassList("ch-star");
                Q<VisualElement>("stars").Add(star);
            }
            for (int i = 0; i < 5; i++)
            {
                var star = new StarMark();
                star.AddToClassList("ch-star");
                Q<VisualElement>("cele-stars").Add(star);
            }
            Q<VisualElement>("month-spark").Add(new SparkLine { style = { flexGrow = 1 } });
        }

        private void ApplyLanguageV3()
        {
            Q<Button>("evening-timetable").text = text.Get("life.timetable.title");
            Q<Label>("report-title").text = text.Get("life.report.title");
            Q<Label>("report-best-label").text = text.Get("life.report.best");
            Q<Label>("report-tease-label").text = text.Get("life.report.tomorrow");
            Q<Button>("report-ok").text = text.Get("life.card.ok");
            Q<Button>("month-ok").text = text.Get("life.card.ok");
            Q<Label>("month-compare").text = text.Get("life.month.compare");
            Q<Label>("month-spark-label").text = text.Get("life.month.standing");
            Q<Button>("cele-ok").text = text.Get("life.celebrate.tap");
            Q<Button>("sheet-close").text = text.Get("life.sheet.close");
            Q<Label>("ending-title-label").text = text.Get("life.ending.title_label");
            Q<Label>("ending-epilogue-title").text = text.Get("life.epilogue.title");
            shown.Clear();
            if (sheet != null) RenderSheet();
        }

        // Header and school tab

        private void RefreshHeaderV3()
        {
            VisualElement stars = Q<VisualElement>("stars");
            for (int i = 0; i < stars.childCount; i++) ((StarMark)stars[i]).On = i < game!.Stars;
            Q<Label>("conscience").text = text.Get("life.conscience.title") + " · " + text.Get("life.conscience." + game!.ConscienceBand);
            Show("sheet", sheet != null);
        }

        private void RefreshChallenge()
        {
            bool has = game!.ChallengeId != null && game.Ending == "InProgress";
            Show("challenge", has);
            if (!has) return;
            string id = game.ChallengeId!;
            string? verdict = game.ChallengeVerdict;
            if (Same("challenge", id + "|" + verdict)) return;
            Q<Label>("challenge-title").text = text.Get("life.challenge.title") + ": " + text.Get("life.challenge." + id);
            Q<Label>("challenge-info").text = text.Get($"life.challenge.{id}.info") + " · " + Cash(game.ChallengeReward);
            Label status = Q<Label>("challenge-status");
            status.text = verdict == null ? text.Get("life.challenge.status") : text.Get("life.challenge." + verdict);
            // Verdict lines are long; the status slot keeps the short running word only.
            status.EnableInClassList("hidden", verdict != null);
            if (verdict != null) Q<Label>("challenge-info").text = text.Get("life.challenge." + verdict);
        }

        // Day report

        private void FillReport()
        {
            if (Same("report", game!.ReportDay + "|" + game.ReportCash)) return;
            int cash = game.ReportCash;
            Q<Label>("report-cash").text = text.Get("life.report.cash") + ": " + text.Get("ch.cash", Args(("cash", Signed(cash) + Math.Abs(cash))));
            VisualElement chips = Q<VisualElement>("report-chips");
            chips.Clear();
            foreach (KeyValuePair<string, int> change in game.ReportChanges())
                chips.Add(Chip(change.Key, change.Value));
            int best = game.ReportBestLine;
            Q<Label>("report-best").text = best >= 0 && best < game.LineCount ? LineText(game.Line(best)) : text.Get("life.report.none");
            Q<Label>("report-tease").text = text.Get(game.ReportTease);
        }

        private string Signed(int value) => (IsArabic ? Lrm : "") + (value > 0 ? "+" : value < 0 ? "-" : "");

        private Label Chip(string stat, int value)
        {
            string number = (IsArabic ? Lrm : "") + (value > 0 ? "+" + value : value.ToString());
            var chip = new Label(text.Get("ch.delta", Args(("value", number), ("name", text.Get("ch.stat." + stat)))));
            chip.AddToClassList("ch-chip");
            chip.AddToClassList(value > 0 ? "ch-chip--good" : "ch-chip--bad");
            return chip;
        }

        // Month certificate

        private void FillMonth()
        {
            var cards = game!.MonthCards();
            if (cards.Count == 0) return;
            var now = cards[cards.Count - 1];
            if (Same("month", now.month + "|" + cards.Count)) return;
            Q<Label>("month-title").text = text.Get("life.month.title") + " · " + text.Get("life.month.number", Args(("month", now.month)));
            Q<Label>("month-grade").text = text.Get("life.month.grade", Args(("grade", now.grade)));
            Q<Label>("month-body").text = cards.Count < 2 ? text.Get("life.month.first") : text.Get("life.month.body");
            VisualElement rows = Q<VisualElement>("month-rows");
            rows.Clear();
            var before = cards.Count >= 2 ? cards[cards.Count - 2] : now;
            bool first = cards.Count < 2;
            CompareRow(rows, text.Get("life.month.cash"), Cash(now.cash), first ? 0 : now.cash - before.cash);
            CompareRow(rows, text.Get("ch.stat.Students"), now.students.ToString(), first ? 0 : now.students - before.students);
            CompareRow(rows, text.Get("ch.stat.Reputation"), now.rep.ToString(), first ? 0 : now.rep - before.rep);
            CompareRow(rows, text.Get("ch.stat.Parents"), now.parents.ToString(), first ? 0 : now.parents - before.parents);
            CompareRow(rows, text.Get("ch.stat.Staff"), now.staff.ToString(), first ? 0 : now.staff - before.staff);
            CompareRow(rows, text.Get("ch.stat.Academics"), now.acad.ToString(), first ? 0 : now.acad - before.acad);
            var points = new List<float>();
            foreach (var m in cards) points.Add((m.rep + m.parents + m.staff + m.acad) / 4f);
            ((SparkLine)Q<VisualElement>("month-spark")[0]).SetPoints(points);
        }

        private void CompareRow(VisualElement parent, string name, string value, int delta)
        {
            VisualElement row = Box(parent, "ch-compare");
            var nameLabel = new Label(name);
            nameLabel.AddToClassList("ch-compare__name");
            row.Add(nameLabel);
            var valueLabel = new Label(value);
            valueLabel.AddToClassList("ch-compare__value");
            row.Add(valueLabel);
            var deltaLabel = new Label(delta == 0 ? "" : (IsArabic ? Lrm : "") + (delta > 0 ? "+" + delta : delta.ToString()));
            deltaLabel.AddToClassList("ch-compare__delta");
            deltaLabel.AddToClassList(delta > 0 ? "ch-compare__delta--good" : "ch-compare__delta--bad");
            row.Add(deltaLabel);
        }

        // Celebrations

        private void RefreshCelebrations()
        {
            bool up = game!.CelebrationCount > 0;
            Show("celebrate", up);
            celeActive = up;
            if (!up) { giftOpened = false; return; }
            var c = game.TopCelebration!.Value;
            bool wrapped = c.kind == "gift" && !giftOpened;
            if (Same("celebrate", $"{c.kind}|{c.id}|{c.value}|{giftOpened}|{game.CelebrationCount}")) return;

            VisualElement card = Q<VisualElement>("cele-card");
            card.AddToClassList("ch-cele--start");
            card.schedule.Execute(() => card.RemoveFromClassList("ch-cele--start")).ExecuteLater(30);

            Show("cele-gift", wrapped);
            Show("cele-stars", c.kind == "star" && !wrapped);
            Show("cele-ok", !wrapped);
            Label title = Q<Label>("cele-title"), detail = Q<Label>("cele-detail"), value = Q<Label>("cele-value");
            value.text = "";
            value.EnableInClassList("hidden", true);
            celeTarget = 0;
            celeShown = 0;
            celeCarry = 0f;
            celeCountCash = false;

            if (wrapped)
            {
                title.text = text.Get("life.celebrate.gift.title");
                detail.text = text.Get("life.gift.tap");
                return;
            }

            title.text = c.kind == "challenge" ? text.Get("life.challenge.won") : text.Get($"life.celebrate.{c.kind}.title");
            switch (c.kind)
            {
                case "star":
                    detail.text = text.Get("life.celebrate.star.body", Args(("stars", c.value)));
                    VisualElement stars = Q<VisualElement>("cele-stars");
                    for (int i = 0; i < stars.childCount; i++) ((StarMark)stars[i]).On = i < c.value;
                    break;
                case "level":
                    detail.text = text.Get("life.celebrate.level.body", Args(("level", c.value)));
                    StartCount(c.value, false);
                    break;
                case "achievement":
                    detail.text = text.Get("life.ach." + c.id) + "\n" + text.Get($"life.ach.{c.id}.info");
                    break;
                case "title":
                    detail.text = text.Get("life.title." + c.id) + "\n" + text.Get($"life.title.{c.id}.info");
                    break;
                case "promotion":
                    detail.text = text.Get("life.promote." + c.id, Args(("name", NameText(c.value))));
                    break;
                case "challenge":
                    detail.text = text.Get("life.challenge." + c.id);
                    StartCount(c.value, true);
                    break;
                case "gift":
                    detail.text = text.Get(c.id);
                    StartCount(c.value, c.id.StartsWith("life.up.", StringComparison.Ordinal));
                    break;
                default:
                    detail.text = "";
                    break;
            }
        }

        private void StartCount(int target, bool cash)
        {
            celeTarget = target;
            celeShown = 0;
            celeCarry = 0f;
            celeCountCash = cash;
            celeRate = Mathf.Max(1f, target / CountUpSeconds);
            Label value = Q<Label>("cele-value");
            value.text = CountText(0);
            value.EnableInClassList("hidden", false);
        }

        private string CountText(int number) => celeCountCash ? Cash(number) : (IsArabic ? Lrm : "") + "+" + number;

        private void UpdateCelebrations()
        {
            if (!celeActive || root == null) return;
            float pulse = (Mathf.Sin(Time.unscaledTime * 5f) + 1f) * 0.5f;
            Q<Label>("cele-title").style.color = Color.Lerp(TitleA, TitleB, pulse);
            if (celeShown >= celeTarget) return;
            celeCarry += celeRate * Time.unscaledDeltaTime;
            int step = (int)celeCarry;
            if (step <= 0) return;
            celeCarry -= step;
            celeShown = Mathf.Min(celeTarget, celeShown + step);
            Q<Label>("cele-value").text = CountText(celeShown);
        }

        // Ending

        private void FillEndingV3()
        {
            string title = game!.Title;
            if (Same("ending-v3", game.Ending + "|" + title + "|" + game.CareerXp)) return;
            Q<Label>("ending-title-name").text = text.Get("life.title." + title);
            Q<Label>("ending-title-info").text = text.Get($"life.title.{title}.info");
            Q<Label>("ending-conscience").text = text.Get(game.ConscienceEnding);
            Q<Label>("ending-xp").text = text.Get("life.ending.xp", Args(("xp", game.XpGained)));
            VisualElement list = Q<VisualElement>("ending-epilogues");
            list.Clear();
            foreach (var e in game.Epilogues()) Text(list, "ch-epilogue", text.Get(e.key, Args(("name", NameText(e.name)))));
        }

        // Director tab

        private void RenderHome()
        {
            if (Same("home", $"{game!.Stars}|{game.ConscienceBand}|{game.CareerLevel}|{game.CareerXp}|{game.Phase}")) return;
            ScrollView panel = Q<ScrollView>("panel-home");
            panel.Clear();
            Text(panel, "ch-section", text.Get("life.tab.home"));
            VisualElement director = Box(panel, "ch-item");
            ItemHead(director, text.Get("life.stars.title"), game.Stars + " / 5");
            Text(director, "ch-item__info", text.Get("life.conscience.title") + " · " + text.Get("life.conscience." + game.ConscienceBand));
            Text(director, "ch-item__soft", text.Get("life.director.energy") + " " + game.Energy + "   ·   " + text.Get("life.director.family") + " " + game.Family);

            BuildCareerCard(panel);
            Text(panel, "ch-section", text.Get("life.chat.parents_title") + " · " + text.Get("life.chat.staff_title"));
            VisualElement rooms = Box(Box(panel, "ch-item"), "ch-item__buttons");
            rooms.AddToClassList("ch-item__buttons--row");
            Press(rooms, "home-parents", "ch-mini", text.Get("life.chat.parents_title"), () => OpenSheet("parents"));
            Press(rooms, "home-staff", "ch-mini", text.Get("life.chat.staff_title"), () => OpenSheet("staff"));
            VisualElement more = Box(Box(panel, "ch-item"), "ch-item__buttons");
            more.AddToClassList("ch-item__buttons--row");
            Press(more, "home-album", "ch-mini", text.Get("life.album.title"), () => OpenSheet("album"));
            Press(more, "home-hall", "ch-mini", text.Get("life.career.hall"), () => OpenSheet("hall"));
            Press(more, "home-timetable", "ch-mini", text.Get("life.timetable.title"), () => OpenSheet("timetable")).SetEnabled(game.Phase == "Evening");
        }

        private void BuildCareerCard(VisualElement parent)
        {
            VisualElement item = Box(parent, "ch-item");
            ItemHead(item, text.Get("life.career.title"), text.Get("life.career.level") + " " + game!.CareerLevel);
            int next = game.NextLevelXp;
            Text(item, "ch-item__info", text.Get("life.career.xp") + " " + game.CareerXp + (next > 0 ? " / " + next : ""));
            if (next > 0)
            {
                VisualElement track = Box(item, "ch-career__track");
                VisualElement fill = Box(track, "ch-career__fill");
                fill.style.width = Length.Percent(Mathf.Clamp01(game.CareerXp / (float)next) * 100f);
                Text(item, "ch-item__soft", text.Get("life.career.next") + ": " + Math.Max(0, next - game.CareerXp));
            }
        }

        private void BuildCareerStrip(VisualElement form)
        {
            BuildCareerCard(form);
            if (game!.HasQuickStart)
                Press(form, "quick-start", "ch-mini", text.Get("life.career.quick"), () => { draft = game.QuickDraft(); OpenSchool(); });
            VisualElement buttons = Box(form, "ch-item__buttons");
            buttons.AddToClassList("ch-item__buttons--row");
            Press(buttons, "setup-album", "ch-mini", text.Get("life.album.title"), () => OpenSheet("album")).AddToClassList("ch-mini--quiet");
            Press(buttons, "setup-hall", "ch-mini", text.Get("life.career.hall"), () => OpenSheet("hall")).AddToClassList("ch-mini--quiet");

            Text(form, "ch-section", text.Get("life.scenario.title"));
            foreach (string id in LifeGame.Scenarios)
            {
                string scenario = id;
                int needs = LifeGame.ScenarioLevel(id);
                bool locked = !game.Unlocked(needs);
                var card = new Button(() => { draft.Scenario = scenario; Edit(null); }) { name = "s-" + id };
                card.AddToClassList("ch-building");
                card.EnableInClassList("ch-building--on", (draft.Scenario ?? "standard") == id);
                card.EnableInClassList("ch-building--locked", locked);
                card.SetEnabled(!locked);
                var title = new Label(text.Get("life.scenario." + id));
                title.AddToClassList("ch-building__name");
                var info = new Label(locked ? LockedText(needs) : text.Get($"life.scenario.{id}.info"));
                info.AddToClassList("ch-building__info");
                card.Add(title);
                card.Add(info);
                form.Add(card);
            }
        }

        private string LockedText(int level) => text.Get("life.locked", Args(("level", level)));

        private string Lockable(string value, int level) => game!.Unlocked(level) ? value : value + " · " + LockedText(level);

        // Sheets: chats, timetable, album, Hall of Fame

        private void OpenSheet(string? which)
        {
            sheet = which;
            if (which == "timetable")
            {
                timetableDraft.Clear();
                timetableDraft.AddRange(game!.Morning);
            }
            RenderSheet();
            Refresh();
        }

        private void RenderSheet()
        {
            ScrollView body = Q<ScrollView>("sheet-body");
            body.Clear();
            Show("sheet", sheet != null);
            if (sheet == null) return;
            switch (sheet)
            {
                case "parents": RenderChat(body, "parents", "life.chat.parents_title"); break;
                case "staff": RenderChat(body, "staff", "life.chat.staff_title"); break;
                case "timetable": RenderTimetable(body); break;
                case "album": RenderAlbum(body); break;
                case "hall": RenderHall(body); break;
                case "app": RenderAppSettings(body); break;
            }
        }

        // Game-wide options. New options get their own section here.
        private void RenderAppSettings(ScrollView body)
        {
            Q<Label>("sheet-title").text = text.Get("life.app.title");
            Text(body, "ch-section", text.Get("life.app.language"));
            VisualElement langs = Box(body, "ch-langs");
            foreach (string code in new[] { "ar", "en" })
            {
                bool current = (code == "ar") == IsArabic;
                Button pick = Press(langs, "lang-" + code, "ch-pick", text.Get("life.app.lang." + code), () =>
                {
                    if (!current) StartCoroutine(ToggleLanguage());
                });
                pick.EnableInClassList("ch-pick--on", current);
            }
        }

        private void RenderChat(ScrollView body, string group, string titleKey)
        {
            Q<Label>("sheet-title").text = text.Get(titleKey);
            var chat = game!.Chat(group);
            if (chat.Count == 0)
            {
                Text(Box(body, "ch-item"), "ch-item__info", text.Get("life.chat.empty"));
                return;
            }
            VisualElement? last = null;
            for (int i = Math.Max(0, chat.Count - 60); i < chat.Count; i++)
            {
                var c = chat[i];
                VisualElement bubble = Box(body, "ch-bubble");
                Text(bubble, "ch-bubble__name", group == "parents" ? text.Get("life.parent." + c.sender) : NameText(c.sender));
                Text(bubble, "ch-bubble__text", text.Get(c.key, Args(("name", NameText(c.name)))));
                Text(bubble, "ch-bubble__day", text.Get("life.log.day", Args(("day", c.day))));
                last = bubble;
            }
            if (last != null) body.schedule.Execute(() => body.ScrollTo(last)).ExecuteLater(60);
        }

        private void RenderTimetable(ScrollView body)
        {
            Q<Label>("sheet-title").text = text.Get("life.timetable.title");
            Text(body, "ch-section", text.Get("life.timetable.morning"));
            Text(body, "ch-item__soft", text.Get("life.timetable.hint"));
            VisualElement picks = Box(body, "ch-chipbox");
            foreach (string subject in LifeGame.Subjects)
            {
                string id = subject;
                Button pick = Press(picks, "tt-" + id, "ch-pick", text.Get("life.subject." + id), () =>
                {
                    if (timetableDraft.Contains(id)) timetableDraft.Remove(id);
                    else timetableDraft.Add(id);
                    RenderSheet();
                });
                pick.EnableInClassList("ch-pick--on", timetableDraft.Contains(id));
            }
            Button save = Press(body, "tt-save", "ch-next", text.Get("life.timetable.save"), () =>
            {
                game!.SetTimetable(timetableDraft);
                if (SameMorning())
                {
                    Toast(text.Get("life.timetable.saved"), true);
                    OpenSheet(null);
                }
            });
            save.AddToClassList("ch-next--card");
        }

        private bool SameMorning()
        {
            var now = game!.Morning;
            if (now.Count != timetableDraft.Count) return false;
            foreach (string id in timetableDraft) { bool has = false; foreach (string m in now) if (m == id) has = true; if (!has) return false; }
            return true;
        }

        private void RenderAlbum(ScrollView body)
        {
            Q<Label>("sheet-title").text = text.Get("life.album.title");
            Text(body, "ch-section", text.Get("life.ach.title"));
            foreach (string id in LifeGame.AchievementIds) AlbumEntry(body, "ach." + id, "life.ach." + id, $"life.ach.{id}.info");
            Text(body, "ch-section", text.Get("life.album.titles"));
            foreach (string id in LifeGame.TitleIds) AlbumEntry(body, "title." + id, "life.title." + id, $"life.title.{id}.info");
        }

        private void AlbumEntry(VisualElement body, string albumId, string nameKey, string infoKey)
        {
            int count = game!.AlbumCount(albumId);
            if (count <= 0)
            {
                VisualElement hidden = Box(body, "ch-silhouette");
                Box(hidden, "ch-silhouette__mark");
                Text(hidden, "ch-silhouette__text", text.Get("life.album.locked"));
                return;
            }
            VisualElement item = Box(body, "ch-item");
            item.AddToClassList("ch-item--done");
            ItemHead(item, text.Get(nameKey), count > 1 ? (IsArabic ? Lrm : "") + "×" + count : "");
            Text(item, "ch-item__soft", text.Get(infoKey));
        }

        private void RenderHall(ScrollView body)
        {
            Q<Label>("sheet-title").text = text.Get("life.career.hall");
            var hall = game!.HallOfFame();
            if (hall.Count == 0)
            {
                Text(Box(body, "ch-item"), "ch-item__info", text.Get("life.career.empty"));
                return;
            }
            foreach (var h in hall)
            {
                VisualElement item = Box(body, "ch-item");
                ItemHead(item, text.Get("life.school." + h.school), text.Get("ch.grade", Args(("grade", h.grade))));
                Text(item, "ch-item__info", text.Get("life.title." + h.title) + "   ·   " + text.Get("life.rank", Args(("rank", h.rank))));
                Text(item, "ch-item__soft", text.Get("life.scenario." + h.scenario) + "   ·   " + text.Get("ch.ending." + h.ending) + "   ·   " + text.Get("life.log.day", Args(("day", h.day))));
                if (h.memoryKey.Length > 0)
                {
                    Text(item, "ch-tag", text.Get("life.hall.memory"));
                    Text(item, "ch-item__info", text.Get(h.memoryKey, Args(("name", NameText(h.memoryName)))));
                }
            }
        }

        /// <summary>A five-point star drawn with the panel painter, so no font glyph or image is needed.</summary>
        private sealed class GearMark : VisualElement
        {
            public GearMark()
            {
                AddToClassList("ch-gear");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect r = contentRect;
                if (r.width <= 0f) return;
                Painter2D p = context.painter2D;
                var center = new Vector2(r.width * 0.5f, r.height * 0.5f);
                float radius = Mathf.Min(r.width, r.height) * 0.5f;
                p.strokeColor = Color.white;
                p.lineWidth = radius * 0.26f;
                p.BeginPath();
                p.Arc(center, radius * 0.56f, 0f, 360f);
                p.ClosePath();
                p.Stroke();
                p.lineWidth = radius * 0.3f;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4f;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    p.BeginPath();
                    p.MoveTo(center + direction * radius * 0.68f);
                    p.LineTo(center + direction * radius);
                    p.Stroke();
                }
            }
        }

        private sealed class StarMark : VisualElement
        {
            private static readonly Color Lit = new Color(0.84f, 0.64f, 0.18f), Dim = new Color(0.55f, 0.5f, 0.5f, 0.35f);
            private bool on;

            public bool On
            {
                get => on;
                set { if (on != value) { on = value; MarkDirtyRepaint(); } }
            }

            public StarMark()
            {
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect r = contentRect;
                if (r.width <= 0f) return;
                Painter2D p = context.painter2D;
                p.fillColor = on ? Lit : Dim;
                p.BeginPath();
                var center = new Vector2(r.width * 0.5f, r.height * 0.5f);
                float outer = Mathf.Min(r.width, r.height) * 0.5f, inner = outer * 0.42f;
                for (int i = 0; i < 10; i++)
                {
                    float radius = i % 2 == 0 ? outer : inner;
                    float angle = -Mathf.PI / 2f + i * Mathf.PI / 5f;
                    var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (i == 0) p.MoveTo(point); else p.LineTo(point);
                }
                p.ClosePath();
                p.Fill();
            }
        }

        /// <summary>A small line of the school's standing over the months so far.</summary>
        private sealed class SparkLine : VisualElement
        {
            private readonly List<float> points = new List<float>();

            public SparkLine()
            {
                generateVisualContent += Draw;
            }

            public void SetPoints(IReadOnlyList<float> values)
            {
                points.Clear();
                points.AddRange(values);
                MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect r = contentRect;
                if (r.width <= 0f || points.Count == 0) return;
                float min = float.MaxValue, max = float.MinValue;
                foreach (float v in points) { min = Mathf.Min(min, v); max = Mathf.Max(max, v); }
                if (max - min < 1f) { max += 0.5f; min -= 0.5f; }
                Painter2D p = context.painter2D;
                p.strokeColor = new Color(0.48f, 0.11f, 0.22f);
                p.lineWidth = 3f;
                p.BeginPath();
                Vector2 last = default;
                for (int i = 0; i < points.Count; i++)
                {
                    float x = points.Count == 1 ? r.width * 0.5f : 6f + (r.width - 12f) * i / (points.Count - 1);
                    float y = r.height - 6f - (r.height - 12f) * (points[i] - min) / (max - min);
                    last = new Vector2(x, y);
                    if (i == 0) p.MoveTo(last); else p.LineTo(last);
                }
                if (points.Count > 1) p.Stroke();
                p.fillColor = new Color(0.84f, 0.56f, 0.12f);
                p.BeginPath();
                p.Arc(last, 4.5f, 0f, 360f);
                p.Fill();
            }
        }
    }
}
