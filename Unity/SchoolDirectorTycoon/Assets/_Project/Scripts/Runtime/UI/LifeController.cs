#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SchoolDirectorTycoon.Bridge;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace SchoolDirectorTycoon.UI
{
    /// <summary>Portrait screens of the school life mode: setup, the five tabs, evenings, morning letters and the ending.
    /// Holds no rules and owns real time and the save file. No audio: every result is colour, motion or text.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed partial class LifeController : MonoBehaviour
    {
        private static readonly string[] StatNames = { "Reputation", "Parents", "Staff", "Academics" };
        private static readonly string[] TabIds = { "school", "people", "build", "train", "office", "home" };
        private static readonly float[] Paces = { 0f, 0.25f, 0.5f, 1f, 4f };
        private const int DefaultPace = 2;
        private const string PaceKey = "sdt.life.pace";
        private const float ToastSeconds = 2.6f, TickSeconds = 30f;
        private const int ShownLines = 30;
        private const string LanguageKey = "sdt.language";
        private const int TestStartNow = 1000000;
        private static readonly DateTime Epoch = new DateTime(2020, 1, 1);
        private static readonly DateTime EpochUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly IHudText text = new LocalizedHudText();
        private readonly Dictionary<string, (VisualElement fill, Label value)> bars = new Dictionary<string, (VisualElement, Label)>();
        private readonly Dictionary<string, string> shown = new Dictionary<string, string>();
        private LifeGame? game;
        private VisualElement? root;
        private ScrollView? log;
        private LifeDraft draft;
        private bool settingsOpen, testing;
        private int shownLines, lastLogDay = -1, lastSlot = -99, speedIndex = DefaultPace, dotCount = -1, theme = -1;
        private int trainTeacher = -1;
        private string trainCourse = "";
        private string tab = "school";
        private float toastTimer, clockCarry, tickTimer;
        private int? testNow;

        public LifeGame? Game => game;
        public bool IsBound => game != null;
        public LifeDraft Draft => draft;
        public string CurrentTab => tab;
        public int PaceIndex => speedIndex;

        public string SavePath => Path.Combine(Application.persistentDataPath, testing ? "life-test.save" : "life.save");
        public string CareerPath => Path.Combine(Application.persistentDataPath, testing ? "life-test.career" : "life.career");
        private static int RealDay => (int)(DateTime.Now.Date - Epoch).TotalDays;
        private int NowMinutes => testNow ?? (int)(DateTime.UtcNow - EpochUtc).TotalMinutes;

        private IEnumerator Start()
        {
            yield return LocalizationSettings.InitializationOperation;
            if (LocalizationSettings.SelectedLocale == null && LocalizationSettings.AvailableLocales.Locales.Count > 0)
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[0];
            yield return SelectLanguage(StartingLanguage());
            testing = Array.IndexOf(Environment.GetCommandLineArgs(), "-sdtChronicle") >= 0;
            if (testing)
            {
                testNow = TestStartNow;
                // Each automated run starts from a level-one career, so the locked options can be checked.
                try { File.Delete(CareerPath); } catch (IOException) { /* a leftover career only changes the locks shown */ }
            }
            speedIndex = Mathf.Clamp(PlayerPrefs.GetInt(PaceKey, DefaultPace), 0, Paces.Length - 1);
            root = GetComponent<UIDocument>().rootVisualElement;
            root.schedule.Execute(() => ApplySafeArea(root));
            Wire();
            Use(testing ? NewGame() : LoadOrNew());
            ApplyLanguage();
            if (!testing) game!.Greet(RealDay);
            TickNow();
        }

        private static LifeGame NewGame() => new LifeGame((uint)Environment.TickCount | 1u);

        private LifeGame LoadOrNew()
        {
            try
            {
                if (File.Exists(SavePath)) return LifeGame.Deserialize(File.ReadAllText(SavePath));
            }
            catch (Exception error) when (error is FormatException || error is IOException)
            {
                Debug.LogWarning("SDT: unreadable save set aside: " + error.Message);
                try { File.Copy(SavePath, SavePath + ".bad", true); } catch (IOException) { /* the new game still starts */ }
            }
            return NewGame();
        }

        private void Save()
        {
            if (game == null) return;
            try
            {
                string temp = SavePath + ".tmp";
                File.WriteAllText(temp, game.Serialize());
                if (File.Exists(SavePath)) File.Replace(temp, SavePath, null);
                else File.Move(temp, SavePath);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Debug.LogWarning("SDT: save failed: " + error.Message);
            }
        }

        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnApplicationQuit() => Save();

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) return;
            if (!testing) game?.Greet(RealDay);
            TickNow();
        }

        /// <summary>Lets the automated run pretend a new calendar day has begun.</summary>
        public void GreetDay(int day) => game?.Greet(day);

        /// <summary>Lets the automated run move the real-time clock.</summary>
        public void SetTestNow(int now)
        {
            testNow = now;
            TickNow();
        }

        public int TestNow => NowMinutes;

        /// <summary>Hands real time to Core and shows anything that finished. The only feedback is visual.</summary>
        private void TickNow()
        {
            if (game == null) return;
            int before = game.LineCount;
            game.Tick(NowMinutes);
            for (int i = before; i < game.LineCount; i++)
            {
                LifeLine line = game.Line(i);
                if (line.Key.EndsWith(".done", StringComparison.Ordinal)) Toast(LineText(line), true);
            }
        }

        private static string StartingLanguage()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-sdtLang");
            if (at >= 0 && at + 1 < args.Length) return args[at + 1];
            string saved = PlayerPrefs.GetString(LanguageKey, "");
            if (saved.Length > 0) return saved;
            return Application.systemLanguage == SystemLanguage.Arabic ? "ar" : "en";
        }

        private static IEnumerator SelectLanguage(string code)
        {
            Locale? locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(code));
            if (locale == null) yield break;
            LocalizationSettings.SelectedLocale = locale;
            yield return LocalizationSettings.StringDatabase.GetTableAsync(LocalizedHudText.Table, locale);
        }

        private static bool IsArabic => LocalizationSettings.SelectedLocale != null && LocalizationSettings.SelectedLocale.Identifier.Code == "ar";

        private IEnumerator ToggleLanguage()
        {
            string code = IsArabic ? "en" : "ar";
            yield return SelectLanguage(code);
            PlayerPrefs.SetString(LanguageKey, code);
            PlayerPrefs.Save();
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            VisualElement screen = Q<VisualElement>("root");
            screen.EnableInClassList("rtl", IsArabic);
            screen.languageDirection = IsArabic ? LanguageDirection.RTL : LanguageDirection.LTR;
            Q<Label>("event-tag").text = text.Get("ch.decide");
            Q<Button>("restart").text = text.Get("ch.new");
            Q<Label>("setup-title").text = text.Get("life.setup.title");
            Q<Label>("setup-intro").text = text.Get("life.setup.intro", Args(("cash", game!.StartingCash)));
            Q<Button>("setup-open").text = text.Get("life.setup.open");
            Q<Label>("settings-title").text = text.Get("life.settings.title");
            Q<Button>("settings-apply").text = text.Get("life.settings.apply");
            Q<Button>("settings-cancel").text = text.Get("life.settings.cancel");
            Q<Label>("evening-title").text = text.Get("life.evening.title");
            Q<Label>("evening-body").text = text.Get("life.evening.body");
            Q<Button>("evening-settings").text = text.Get("life.evening.settings");
            Q<Label>("letter-tease").text = text.Get("life.letter.tease");
            ApplyLanguageV3();
            Q<Label>("actions-title").text = text.Get("life.action.title");
            string lrm = IsArabic ? "‎" : "";
            Q<Button>("extend-plus").text = lrm + "+15 · " + text.Get("life.clock.extend");
            Q<Button>("extend-minus").text = lrm + "-15 · " + text.Get("life.clock.shorten");
            for (int i = 0; i < Paces.Length; i++) Q<Button>("speed-" + i).text = text.Get("life.pace." + i);
            foreach (string id in TabIds) Q<Button>("tab-" + id).text = text.Get("life.tab." + id);
            foreach (string id in LifeGame.ActionIds) Q<Button>("action-" + id).text = text.Get("life.action." + id);
            foreach (string stat in StatNames) Q<Label>("name-" + stat).text = text.Get("ch.stat." + stat);
            Q<Label>("name-Energy").text = text.Get("life.director.energy");
            Q<Label>("name-Family").text = text.Get("life.director.family");
            ResetLog();
            shown.Clear();
            RebuildForms();
            Refresh();
        }

        private static void ApplySafeArea(VisualElement element)
        {
            Rect safe = Screen.safeArea;
            if (Screen.height <= 0) return;
            float scale = element.worldBound.height / Screen.height;
            element.style.paddingTop = (Screen.height - safe.yMax) * scale;
            element.style.paddingBottom = safe.yMin * scale;
        }

        private void Wire()
        {
            log = Q<ScrollView>("log");
            Q<Button>("next").clicked += () => game?.Advance(100000);
            Q<Button>("restart").clicked += () => { Use(NewGame()); ApplyLanguage(); Save(); };
            Q<Button>("setup-open").clicked += OpenSchool;
            Q<Button>("evening-settings").clicked += () => { settingsOpen = true; draft = game!.CurrentDraft(); RebuildForms(); Refresh(); };
            Q<Button>("settings-cancel").clicked += () => { settingsOpen = false; Refresh(); };
            Q<Button>("settings-apply").clicked += () =>
            {
                settingsOpen = false;
                draft.Teachers = game!.CurrentDraft().Teachers;
                game.Adjust(draft);
                Refresh();
            };
            Q<Button>("extend-plus").clicked += () => game?.ExtendDay(15);
            Q<Button>("extend-minus").clicked += () => game?.ExtendDay(-15);
            for (int i = 0; i < Paces.Length; i++)
            {
                int index = i;
                Q<Button>("speed-" + i).clicked += () =>
                {
                    speedIndex = index;
                    PlayerPrefs.SetInt(PaceKey, index);
                    PlayerPrefs.Save();
                    Refresh();
                };
            }
            foreach (string id in TabIds)
            {
                string target = id;
                Q<Button>("tab-" + id).clicked += () => { tab = target; Refresh(); };
            }
            foreach (string id in LifeGame.ActionIds)
            {
                string action = id;
                Q<Button>("action-" + id).clicked += () => game?.DirectorAction(action);
            }

            VisualElement stats = Q<VisualElement>("stats");
            stats.Clear();
            foreach (string stat in new[] { StatNames[0], StatNames[1], StatNames[2], StatNames[3], "Energy", "Family" })
            {
                var row = new VisualElement();
                row.AddToClassList("ch-stat");
                var name = new Label { name = "name-" + stat };
                name.AddToClassList("ch-stat__name");
                var track = new VisualElement();
                track.AddToClassList("ch-stat__track");
                var fill = new VisualElement();
                fill.AddToClassList("ch-stat__fill");
                track.Add(fill);
                var value = new Label();
                value.AddToClassList("ch-stat__value");
                row.Add(name); row.Add(track); row.Add(value);
                stats.Add(row);
                bars[stat] = (fill, value);
            }
            WireV3();
        }

        private void Use(LifeGame next)
        {
            if (game != null) { game.Changed -= Refresh; game.Committed -= Save; game.Rejected -= Reject; }
            next.UseCareer(CareerPath);
            game = next;
            game.Changed += Refresh;
            game.Committed += Save;
            game.Rejected += Reject;
            draft = game.CurrentDraft();
            settingsOpen = false;
            lastSlot = -99;
            dotCount = -1;
            tab = "school";
            trainTeacher = -1;
            trainCourse = "";
            shown.Clear();
            ResetV3();
        }

        private void OpenSchool()
        {
            game!.Open(draft);
            if (game.Phase != "Setup" && !testing) game.Greet(RealDay);
            else if (game.Phase != "Setup") game.Greet(1000);
            TickNow();
        }

        private T Q<T>(string name) where T : VisualElement =>
            root!.Q<T>(name) ?? throw new InvalidOperationException($"Element '{name}' missing from Chronicle.uxml");

        private static Dictionary<string, object> Args(params (string name, object value)[] pairs)
        {
            var args = new Dictionary<string, object>(pairs.Length);
            foreach (var (name, value) in pairs) args[name] = value;
            return args;
        }

        private string Cash(int amount) => text.Get("ch.cash", Args(("cash", amount)));

        private static (int hours, int minutes) Hm(int total) => (total / 60, total % 60);

        private string Remaining(int total)
        {
            var (hours, minutes) = Hm(total);
            return text.Get("life.time.remaining", Args(("hours", hours), ("minutes", minutes)));
        }

        private string Duration(int total)
        {
            var (hours, minutes) = Hm(total);
            return text.Get("life.time.duration", Args(("hours", hours), ("minutes", minutes)));
        }

        private string NameText(int name) => name < 0 ? "" : text.Get("life.name." + name);

        private string LineText(LifeLine line)
        {
            if (line.Name < 0 && line.Subject == null) return text.Get(line.Key);
            return text.Get(line.Key, Args(("name", NameText(line.Name)), ("subject", line.Subject == null ? "" : text.Get("life.subject." + line.Subject))));
        }

        // Small builders

        private static Label Text(VisualElement parent, string cls, string value)
        {
            var label = new Label(value);
            label.AddToClassList("ch-text");
            label.AddToClassList(cls);
            parent.Add(label);
            return label;
        }

        private static Button Press(VisualElement parent, string name, string cls, string value, Action action)
        {
            var button = new Button(action) { name = name, text = value };
            button.AddToClassList(cls);
            parent.Add(button);
            return button;
        }

        private static VisualElement Box(VisualElement parent, string cls)
        {
            var box = new VisualElement();
            box.AddToClassList(cls);
            parent.Add(box);
            return box;
        }

        private static VisualElement ItemHead(VisualElement item, string nameText, string tag)
        {
            VisualElement head = Box(item, "ch-item__head");
            Text(head, "ch-item__name", nameText);
            if (tag.Length > 0) Text(head, "ch-item__tag", tag);
            return head;
        }

        // Setup and settings form

        private void RebuildForms()
        {
            BuildForm(Q<VisualElement>("setup-form"), true);
            BuildForm(Q<VisualElement>("settings-form"), false);
            RefreshEstimates();
            ApplyTheme();
        }

        private void BuildForm(VisualElement form, bool setup)
        {
            form.Clear();
            if (setup)
            {
                BuildCareerStrip(form);
                foreach ((string id, int capacity, int rent, bool _) in LifeGame.Buildings())
                {
                    string building = id;
                    var card = new Button(() => { draft.Building = building; Edit(null); }) { name = "b-" + id };
                    card.AddToClassList("ch-building");
                    card.EnableInClassList("ch-building--on", draft.Building == id);
                    int needs = LifeGame.BuildingLevel(id);
                    bool levelLocked = !game!.Unlocked(needs);
                    bool barred = levelLocked || (draft.Scenario == "village" && id != "villa");
                    card.SetEnabled(!barred);
                    card.EnableInClassList("ch-building--locked", barred);
                    var title = new Label(text.Get("life.building." + id));
                    title.AddToClassList("ch-building__name");
                    var info = new Label(levelLocked ? LockedText(needs) : text.Get($"life.building.{id}.info", Args(("capacity", capacity), ("rent", rent))));
                    info.AddToClassList("ch-building__info");
                    card.Add(title);
                    card.Add(info);
                    form.Add(card);
                }

                Text(form, "ch-section", text.Get("life.identity.title"));
                Row(form, "identity.name", "name", Lockable(text.Get("life.school." + draft.SchoolName), LifeGame.NameLevel(draft.SchoolName)), d => { d.SchoolName++; return d; }, d => { d.SchoolName--; return d; });
                Row(form, "identity.crest", "crest", Lockable(text.Get("life.crest." + draft.Crest), LifeGame.CrestLevel(draft.Crest)), d => { d.Crest++; return d; }, d => { d.Crest--; return d; });
                VisualElement colour = Row(form, "identity.colour", "colour", "", d => { d.Colour++; return d; }, d => { d.Colour--; return d; });
                var swatch = new VisualElement { name = "colour-swatch" };
                swatch.AddToClassList("ch-swatch");
                swatch.AddToClassList("ch-swatch--" + draft.Colour);
                colour.Insert(2, swatch);
                colour.RemoveAt(3);
                Row(form, "identity.philosophy", "philosophy", Lockable(text.Get("life.phil." + draft.Philosophy), LifeGame.PhilosophyLevel(draft.Philosophy)), d => CyclePhil(d, 1), d => CyclePhil(d, -1));
                Text(form, "ch-step__info", text.Get($"life.phil.{draft.Philosophy}.info"));
                Text(form, "ch-section", text.Get("life.settings.title"));
            }

            Row(form, "form.seats", "seats", draft.Seats.ToString(), d => { d.Seats += 10; return d; }, d => { d.Seats -= 10; return d; });
            Row(form, "form.fee", "fee", Cash(draft.Fee), d => { d.Fee += 10; return d; }, d => { d.Fee -= 10; return d; });
            if (setup) Row(form, "form.teachers", "teachers", draft.Teachers.ToString(), d => { d.Teachers++; return d; }, d => { d.Teachers--; return d; });
            Row(form, "form.admins", "admins", draft.Admins.ToString(), d => { d.Admins++; return d; }, d => { d.Admins--; return d; });
            Row(form, "form.start", "start", LifeGame.TimeText(draft.Start), d => { d.Start += 15; return d; }, d => { d.Start -= 15; return d; });
            Row(form, "form.periods", "periods", draft.Periods.ToString(), d => { d.Periods++; return d; }, d => { d.Periods--; return d; });
            Row(form, "form.period_minutes", "period-minutes", draft.PeriodMinutes.ToString(), d => { d.PeriodMinutes += 5; return d; }, d => { d.PeriodMinutes -= 5; return d; });
            Row(form, "form.break_minutes", "break-minutes", draft.BreakMinutes.ToString(), d => { d.BreakMinutes += 5; return d; }, d => { d.BreakMinutes -= 5; return d; });
        }

        private static LifeDraft CyclePhil(LifeDraft d, int step)
        {
            IReadOnlyList<string> all = LifeGame.Philosophies;
            int index = 0;
            for (int i = 0; i < all.Count; i++) if (all[i] == d.Philosophy) index = i;
            index = (index + step + all.Count) % all.Count;
            if (d.Scenario == "girls" && all[index] == "sports") index = (index + step + all.Count) % all.Count;
            d.Philosophy = all[index];
            return d;
        }

        private VisualElement Row(VisualElement form, string labelKey, string id, string value, Func<LifeDraft, LifeDraft> plus, Func<LifeDraft, LifeDraft> minus)
        {
            var row = new VisualElement();
            row.AddToClassList("ch-step");
            var label = new Label(text.Get("life." + labelKey));
            label.AddToClassList("ch-step__name");
            var less = new Button(() => Edit(minus)) { name = "minus-" + id, text = "−" };
            less.AddToClassList("ch-step__button");
            var current = new Label(value);
            current.AddToClassList("ch-step__value");
            var more = new Button(() => Edit(plus)) { name = "plus-" + id, text = "+" };
            more.AddToClassList("ch-step__button");
            row.Add(label); row.Add(less); row.Add(current); row.Add(more);
            form.Add(row);
            return row;
        }

        private void Edit(Func<LifeDraft, LifeDraft>? change)
        {
            if (change != null) draft = change(draft);
            draft = LifeGame.Clamp(draft, game!.Phase == "Setup" ? 0 : game.SeatCapacity);
            RebuildForms();
        }

        private void RefreshEstimates()
        {
            int students = LifeGame.ExpectedStudents(draft), bill = LifeGame.WeeklyBill(draft);
            Q<Label>("setup-estimate").text = text.Get("life.est.setup", Args(
                ("opening", LifeGame.OpeningCost(draft)), ("students", students), ("income", students * draft.Fee), ("bill", bill)));
            int running = game!.SettingsStudents(draft);
            Q<Label>("settings-estimate").text = text.Get("life.est.settings", Args(("students", running), ("income", running * draft.Fee), ("bill", game.SettingsBill(draft))));
            Q<Button>("setup-open").SetEnabled(!game.DraftLocked(draft));
        }

        private void ApplyTheme()
        {
            if (game == null) return;
            int colour = game.Phase == "Setup" ? draft.Colour : game.Colour;
            if (colour == theme) return;
            VisualElement screen = Q<VisualElement>("root");
            if (theme >= 0) screen.RemoveFromClassList("theme-" + theme);
            theme = colour;
            screen.AddToClassList("theme-" + theme);
        }

        // Screen state

        public void Refresh()
        {
            if (game == null || root == null) return;
            Q<VisualElement>("setup").EnableInClassList("hidden", game.Phase != "Setup");
            ApplyTheme();
            if (game.Phase == "Setup")
            {
                HideOverlays();
                Q<Label>("setup-crest").text = Letter(text.Get("life.crest." + draft.Crest));
            }
            else
            {
                Q<Label>("title").text = text.Get("life.school." + game.SchoolName);
                Q<Label>("crest").text = Letter(text.Get("life.crest." + game.Crest));
            }
            Q<Label>("week").text = text.Get("life.day", Args(("day", game.Day), ("total", game.TotalDays)));
            Q<Label>("cash").text = Cash(game.Cash);
            Q<Label>("students").text = text.Get("ch.students", Args(("students", game.Students)));
            SetBar("Reputation", game.Reputation);
            SetBar("Parents", game.Parents);
            SetBar("Staff", game.Staff);
            SetBar("Academics", game.Academics);
            SetBar("Energy", game.Energy);
            SetBar("Family", game.Family);
            RefreshHeaderV3();

            if (shownLines < game.LineCount - ShownLines) shownLines = game.LineCount - ShownLines;
            while (shownLines < game.LineCount) AddLine(game.Line(shownLines++));
            while (log!.contentContainer.childCount > ShownLines * 2) log.contentContainer.RemoveAt(0);

            RefreshClock();
            if (game.Phase == "Setup") return;

            bool over = game.Ending != "InProgress", letter = !over && game.PendingLetter != null;
            bool month = !over && !letter && game.CardKind == "month";
            bool decide = !over && !letter && game.HasCard && !month;
            bool evening = !over && !letter && !decide && !month && game.Phase == "Evening";
            Show("ending", over);
            Show("letter", letter);
            Show("event", decide);
            Show("settings", evening && settingsOpen);
            Show("month", month);
            bool report = evening && !settingsOpen && game.ReportDay == game.Day && reportSeenDay != game.Day && game.CelebrationCount == 0;
            Show("report", report);
            if (month) FillMonth();
            if (report) FillReport();

            foreach (string id in TabIds)
            {
                Q<Button>("tab-" + id).EnableInClassList("ch-tab--on", tab == id);
                Q<VisualElement>("panel-" + id).EnableInClassList("hidden", tab != id);
            }

            Show("next", !evening);
            Q<Button>("next").SetEnabled(!over && !letter && !decide && !month && game.Phase == "School");
            Q<Button>("next").text = text.Get("life.clock.skip");
            Show("evening", evening);
            Show("actions", game.Phase == "School");
            Show("clock-controls", game.Phase == "School");
            RefreshActions();

            if (letter) FillLetter();
            if (decide) FillCard();
            if (evening) FillEvening();
            if (over) FillEnding();
            switch (tab)
            {
                case "people": RenderPeople(); break;
                case "build": RenderBuild(); break;
                case "train": RenderTrain(); break;
                case "office": RenderOffice(); break;
                case "home": RenderHome(); break;
            }
            RefreshChallenge();
            RefreshCelebrations();
        }

        private static string Letter(string word) => word.Length == 0 ? "" : word.Substring(0, 1);

        private void Show(string name, bool visible) => Q<VisualElement>(name).EnableInClassList("hidden", !visible);

        private void HideOverlays()
        {
            foreach (string name in new[] { "ending", "letter", "event", "settings", "report", "month", "celebrate", "challenge" }) Show(name, false);
        }

        /// <summary>True when the panel already shows these values; rebuilding would lose a press in progress.</summary>
        private bool Same(string panel, string signature)
        {
            if (shown.TryGetValue(panel, out string previous) && previous == signature) return true;
            shown[panel] = signature;
            return false;
        }

        private void RefreshActions()
        {
            bool school = game!.Phase == "School";
            foreach (string id in LifeGame.ActionIds)
            {
                string slot = id.StartsWith("broadcast_", StringComparison.Ordinal) ? "broadcast" : id;
                Q<Button>("action-" + id).SetEnabled(school && !game.ActionUsed(slot));
            }
        }

        private void RefreshClock()
        {
            int period = game!.Period, total = game.TotalPeriods;
            Q<Label>("clock-time").text = LifeGame.TimeText(game.Minute);
            Q<Label>("clock-period").text = game.Phase == "Evening" ? text.Get("life.clock.done")
                : period == 0 ? text.Get("life.clock.break")
                : text.Get("life.clock.period", Args(("period", period), ("total", total)));
            Q<Label>("clock-left").text = game.Phase == "Evening" ? "" : text.Get("life.clock.left", Args(("minutes", game.MinutesLeft)));
            Q<VisualElement>("clock-fill").style.width = Length.Percent(game.Phase == "Evening" ? 100f : game.SlotProgress * 100f);
            Q<Label>("day-end").text = LifeGame.TimeText(game.DayEndMinute);

            Label season = Q<Label>("season");
            season.EnableInClassList("hidden", game.Season == null);
            if (game.Season != null) season.text = text.Get($"life.season.{game.Season}.banner");

            VisualElement dots = Q<VisualElement>("clock-dots");
            if (dotCount != total)
            {
                dotCount = total;
                dots.Clear();
                for (int i = 0; i < total; i++)
                {
                    var dot = new VisualElement();
                    dot.AddToClassList("ch-dot");
                    dots.Add(dot);
                }
            }
            for (int i = 0; i < dots.childCount; i++)
            {
                bool done = game.Phase == "Evening" || (period > 0 ? i < period - 1 : i < 3);
                dots[i].EnableInClassList("ch-dot--done", done);
                dots[i].EnableInClassList("ch-dot--now", game.Phase == "School" && i == period - 1);
            }

            for (int i = 0; i < Paces.Length; i++) Q<Button>("speed-" + i).EnableInClassList("ch-speed--on", i == speedIndex);

            int slot = game.Phase == "Evening" ? -1 : period;
            if (slot != lastSlot)
            {
                bool ringing = lastSlot != -99;
                lastSlot = slot;
                if (ringing) RingBell();
            }
        }

        /// <summary>The bell is visual: the clock card flashes gold when a lesson changes.</summary>
        private void RingBell()
        {
            VisualElement clock = Q<VisualElement>("clock");
            clock.AddToClassList("ch-clock--bell");
            clock.schedule.Execute(() => clock.RemoveFromClassList("ch-clock--bell")).ExecuteLater(450);
        }

        private void FillLetter()
        {
            string key = game!.PendingLetter!;
            if (Same("letter", key + "|" + game.Streak + "|" + game.LetterBonus + "|" + game.AwayIncome)) return;
            Q<Label>("letter-tag").text = text.Get("life.letter.tag", Args(("streak", game.Streak)));
            Q<Label>("letter-body").text = text.Get(key);
            Q<Label>("letter-reward").text = text.Get("life.letter.reward", Args(("cash", game.LetterBonus)));
            Label away = Q<Label>("letter-away");
            away.EnableInClassList("hidden", game.AwayIncome == 0);
            away.text = text.Get("life.letter.away", Args(("cash", game.AwayIncome)));
            VisualElement choices = Q<VisualElement>("letter-choices");
            choices.Clear();
            IReadOnlyList<string> options = game.LetterChoices();
            if (options.Count == 0)
            {
                choices.Add(Choice("letter-collect", text.Get("life.letter.collect"), () => game.ReadLetter(null)));
                return;
            }
            foreach (string id in options)
            {
                string option = id;
                choices.Add(Choice("letter-" + id, text.Get($"{key}.opt.{id}"), () => game.ReadLetter(option)));
            }
        }

        private void FillCard()
        {
            if (Same("card", game!.CardKind + "|" + game.CardId + "|" + game.CardName + "|" + game.Cash)) return;
            Q<Label>("event-body").text = text.Get(game.CardBodyKey!, Args(("name", NameText(game.CardName))));
            VisualElement choices = Q<VisualElement>("choices");
            choices.Clear();
            foreach ((string id, string buttonKey) in game.CardChoices())
            {
                string choice = id;
                int cost = game.CardCost(id);
                Button button = Choice("choice-" + id, text.Get(buttonKey) + (cost > 0 ? " · " + Cash(cost) : ""), () => game.ResolveCard(choice));
                button.SetEnabled(cost <= game.Cash);
                choices.Add(button);
            }
        }

        private void FillEvening()
        {
            var signature = new System.Text.StringBuilder();
            foreach (string id in game!.Activities()) signature.Append(id).Append(game.ActivityEstimate(id)).Append(',');
            if (Same("evening", signature.ToString())) return;
            VisualElement choices = Q<VisualElement>("evening-choices");
            choices.Clear();
            foreach (string id in game.Activities())
            {
                string activity = id;
                choices.Add(Choice("act-" + id, text.Get("life.act." + id, Args(("cash", game.ActivityEstimate(id)))), () => game.Evening(activity)));
            }
        }

        private void FillEnding()
        {
            var args = Args(("week", game!.Week), ("students", game.Students));
            Q<Label>("ending-title").text = text.Get("ch.ending." + game.Ending);
            Q<Label>("ending-body").text = text.Get("ch.ending.body." + game.Ending, args);
            Q<Label>("ending-grade").text = text.Get("ch.grade", Args(("grade", game.Grade)));
            Q<Label>("ending-rank").text = text.Get("life.rank", Args(("rank", game.Rank)));
            FillEndingV3();
        }

        private void RenderPeople()
        {
            var roster = game!.Roster();
            var followed = game.Followed();
            var candidates = game.Candidates();
            string signature = string.Join(";", roster) + "#" + string.Join(";", followed) + "#" + string.Join(";", candidates) + "#" + game.InterviewAsked + game.InterviewReply + "#" + game.Phase;
            if (Same("people", signature)) return;
            ScrollView panel = Q<ScrollView>("panel-people");
            panel.Clear();

            if (candidates.Count == 0)
            {
                Press(panel, "people-hire", "ch-mini", text.Get("life.people.hire") + " · " + Cash(game.InterviewCost), () => game.StartInterview());
            }
            else
            {
                Text(panel, "ch-section", text.Get("life.interview.title"));
                if (game.InterviewReply != null) Text(Box(panel, "ch-item"), "ch-item__info", text.Get(game.InterviewReply));
                foreach (var c in candidates)
                {
                    int id = c.id;
                    VisualElement item = Box(panel, "ch-item");
                    ItemHead(item, NameText(c.name), text.Get("life.subject." + c.subject));
                    Text(item, "ch-item__info", text.Get("life.interview.band." + c.band));
                    Text(item, "ch-item__soft", TraitText(c.trait));
                    Text(item, "ch-item__soft", text.Get("life.people.pay") + ": " + Cash(c.pay));
                    if (!game.InterviewAsked)
                        foreach (string q in LifeGame.InterviewQuestions)
                        {
                            string question = q;
                            Press(item, $"ask-{id}-{q}", "ch-pick", text.Get("life.interview.ask." + q), () => game.AskCandidate(id, question));
                        }
                    VisualElement buttons = Box(item, "ch-item__buttons");
                    Press(buttons, "hire-" + id, "ch-mini", text.Get("life.interview.hire") + " · " + Cash(game.HiringCost), () => game.HireCandidate(id));
                }
                Press(panel, "interview-cancel", "ch-mini", text.Get("life.interview.cancel"), () => game.CancelInterview()).AddToClassList("ch-mini--quiet");
            }

            Text(panel, "ch-section", text.Get("life.people.title"));
            foreach (var t in roster)
            {
                int id = t.id;
                VisualElement item = Box(panel, "ch-item");
                ItemHead(item, NameText(t.name), text.Get("life.subject." + t.subject));
                if (t.role != null) Text(item, "ch-item__status", text.Get("life.role." + t.role));
                Text(item, "ch-item__info", text.Get("life.people.skill") + " " + t.skill + "   ·   " + text.Get("life.people.mood") + " " + t.mood);
                Text(item, "ch-item__soft", TraitText(t.trait));
                Text(item, "ch-item__soft", text.Get("life.people.pay") + ": " + Cash(t.pay));
                bool away = t.remaining > 0 || t.course != null;
                if (away) Text(item, "ch-item__status", text.Get("life.people.in_training") + " · " + Remaining(t.remaining));
                VisualElement buttons = Box(item, "ch-item__buttons");
                buttons.AddToClassList("ch-item__buttons--row");
                Press(buttons, "observe-" + id, "ch-mini", text.Get("life.people.observe"), () => game.Observe(id)).SetEnabled(!away);
                Button dismiss = Press(buttons, "dismiss-" + id, "ch-mini", text.Get("life.people.dismiss"), () => game.DismissTeacher(id));
                dismiss.AddToClassList("ch-mini--quiet");
                dismiss.SetEnabled(!away);
                bool evening = game.Phase == "Evening";
                Press(buttons, "totm-" + id, "ch-mini", text.Get("life.totm.button"), () => game.NameTeacherOfMonth(id)).SetEnabled(evening);
                if (t.role == null)
                {
                    buttons = Box(item, "ch-item__buttons");
                    buttons.AddToClassList("ch-item__buttons--row");
                    Press(buttons, "promote-head-" + id, "ch-mini", text.Get("life.promote.head.button"), () => game.PromoteTeacher(id, "head")).SetEnabled(evening && !away);
                    Press(buttons, "promote-deputy-" + id, "ch-mini", text.Get("life.promote.deputy.button"), () => game.PromoteTeacher(id, "deputy")).SetEnabled(evening && !away);
                }
            }

            Text(panel, "ch-section", text.Get("life.students.title"));
            foreach (var s in followed)
            {
                VisualElement item = Box(panel, "ch-item");
                ItemHead(item, NameText(s.name), "");
                Text(item, "ch-item__info", text.Get("life.students.marks") + " " + s.marks + "   ·   " + text.Get("life.students.conduct") + " " + s.conduct);
            }
        }

        private string TraitText(string? trait) => trait == null
            ? text.Get("life.people.trait.unknown")
            : text.Get("life.trait." + trait) + " · " + text.Get($"life.trait.{trait}.info");

        private void RenderBuild()
        {
            var upgrades = game!.Upgrades();
            string job = game.BuildingJob ?? "";
            int remaining = game.BuildingRemaining;
            if (Same("build", string.Join(";", upgrades) + "#" + job + remaining)) return;
            ScrollView panel = Q<ScrollView>("panel-build");
            panel.Clear();
            Text(panel, "ch-section", text.Get("life.build.title"));
            foreach (var u in upgrades)
            {
                string id = u.id;
                VisualElement item = Box(panel, "ch-item");
                item.EnableInClassList("ch-item--on", u.building);
                item.EnableInClassList("ch-item--done", u.done);
                ItemHead(item, text.Get("life.up." + id), "");
                Text(item, "ch-item__info", text.Get($"life.up.{id}.info"));
                Text(item, "ch-item__soft", text.Get("life.build.cost") + ": " + Cash(u.cost) + "   ·   " + text.Get("life.build.time") + ": " + Duration(u.minutes));
                if (u.done) Text(item, "ch-item__status", text.Get("life.build.done"));
                else if (u.building) Text(item, "ch-item__status", text.Get("life.build.building") + " · " + Remaining(remaining));
                else if (u.locked) Text(item, "ch-item__soft", text.Get("rule.LifeUpgradeLocked"));
                else if (job.Length > 0) Text(item, "ch-item__soft", text.Get("rule.LifeUpgradeBusy"));
                else
                {
                    VisualElement buttons = Box(item, "ch-item__buttons");
                    Press(buttons, "start-" + id, "ch-mini", text.Get("life.build.start"), () => game.StartUpgrade(id, NowMinutes));
                }
            }
        }

        private void RenderTrain()
        {
            var roster = game!.Roster();
            var courses = game.Courses();
            int slots = game.TrainingSlots, used = 0;
            int firstFree = -1;
            bool pickValid = false;
            foreach (var t in roster)
            {
                bool away = t.remaining > 0 || t.course != null;
                if (away) used++;
                else
                {
                    if (firstFree < 0) firstFree = t.id;
                    if (t.id == trainTeacher) pickValid = true;
                }
            }
            if (!pickValid) trainTeacher = firstFree;
            if (trainCourse.Length == 0 && courses.Count > 0) trainCourse = courses[0].id;

            string signature = string.Join(";", roster) + "#" + slots + "#" + trainTeacher + trainCourse;
            if (Same("train", signature)) return;
            ScrollView panel = Q<ScrollView>("panel-train");
            panel.Clear();
            Text(panel, "ch-section", text.Get("life.train.title"));
            if (slots == 0)
            {
                VisualElement need = Box(panel, "ch-item");
                need.name = "train-need";
                Text(need, "ch-item__info", text.Get("life.train.need_center"));
                Press(Box(need, "ch-item__buttons"), "train-go-build", "ch-mini", text.Get("life.tab.build"), () => { tab = "build"; Refresh(); });
                return;
            }

            Text(panel, "ch-item__soft", text.Get("life.train.slots") + ": " + used + " / " + slots);
            foreach (var t in roster)
            {
                if (t.remaining <= 0 && t.course == null) continue;
                VisualElement item = Box(panel, "ch-item");
                item.AddToClassList("ch-item--on");
                ItemHead(item, NameText(t.name), text.Get("life.course." + t.course));
                Text(item, "ch-item__status", text.Get("life.train.remaining") + ": " + Remaining(t.remaining));
            }

            if (trainTeacher < 0) return;
            Text(panel, "ch-section", text.Get("life.people.title"));
            VisualElement picks = Box(panel, "ch-chipbox");
            foreach (var t in roster)
            {
                if (t.remaining > 0 || t.course != null) continue;
                int id = t.id;
                Button pick = Press(picks, "pick-" + id, "ch-pick", NameText(t.name) + " · " + t.skill, () => { trainTeacher = id; Refresh(); });
                pick.EnableInClassList("ch-pick--on", id == trainTeacher);
            }

            Text(panel, "ch-section", text.Get("life.train.start"));
            foreach (var c in courses)
            {
                string id = c.id;
                var card = new Button(() => { trainCourse = id; Refresh(); }) { name = "course-" + id };
                card.AddToClassList("ch-item");
                card.EnableInClassList("ch-item--on", id == trainCourse);
                panel.Add(card);
                ItemHead(card, text.Get("life.course." + id), Duration(c.minutes));
                Text(card, "ch-item__info", text.Get($"life.course.{id}.info"));
                Text(card, "ch-item__soft", text.Get("life.build.cost") + ": " + Cash(c.cost));
            }
            Button start = Press(panel, "train-start", "ch-mini", text.Get("life.train.start"), () =>
            {
                game.StartTraining(trainTeacher, trainCourse, NowMinutes);
                Refresh();
            });
            start.SetEnabled(used < slots);
        }

        private void RenderOffice()
        {
            var achievements = LifeGame.AchievementIds;
            var earned = new System.Text.StringBuilder();
            foreach (string id in achievements) earned.Append(game!.Earned(id) ? '1' : '0');
            string signature = $"{game!.LoanWeeks}|{game.GoalId}|{game.GoalDone}|{earned}|{game.MemoryCount}|{game.Rank}";
            if (Same("office", signature)) return;
            ScrollView panel = Q<ScrollView>("panel-office");
            panel.Clear();

            Text(panel, "ch-rank", text.Get("life.rank", Args(("rank", game.Rank))));

            if (game.GoalId != null)
            {
                VisualElement goal = Box(panel, "ch-item");
                goal.EnableInClassList("ch-item--done", game.GoalDone);
                ItemHead(goal, text.Get("life.goal.title"), Cash(game.GoalReward));
                Text(goal, "ch-item__info", text.Get("life.goal." + game.GoalId));
                if (game.GoalDone) Text(goal, "ch-item__status", text.Get("life.goal.done"));
            }

            VisualElement loan = Box(panel, "ch-item");
            ItemHead(loan, text.Get("life.loan.button"), Cash(game.LoanAmount));
            if (game.LoanWeeks > 0) Text(loan, "ch-item__status", text.Get("rule.LifeLoanActive") + " · " + game.LoanWeeks + " · " + Cash(game.LoanPayment));
            else Press(Box(loan, "ch-item__buttons"), "loan-take", "ch-mini", text.Get("life.loan.button"), () => game.TakeLoan());

            Text(panel, "ch-section", text.Get("life.ach.title"));
            foreach (string id in achievements)
            {
                VisualElement item = Box(panel, "ch-item");
                item.EnableInClassList("ch-item--off", !game.Earned(id));
                item.EnableInClassList("ch-item--done", game.Earned(id));
                ItemHead(item, text.Get("life.ach." + id), "");
                Text(item, "ch-item__soft", text.Get($"life.ach.{id}.info"));
            }

            if (game.MemoryCount == 0) return;
            Text(panel, "ch-section", text.Get("life.memories.title"));
            foreach (LifeLine memory in game.Memories())
            {
                VisualElement item = Box(panel, "ch-item");
                ItemHead(item, text.Get("life.log.day", Args(("day", memory.Day))), "");
                Text(item, "ch-item__info", LineText(memory));
            }
        }

        private static Button Choice(string name, string label, Action action)
        {
            var button = new Button(action) { name = name, text = label };
            button.AddToClassList("ch-choice");
            return button;
        }

        private void SetBar(string stat, int value)
        {
            var (fill, label) = bars[stat];
            fill.style.width = Length.Percent(value);
            fill.EnableInClassList("ch-stat__fill--low", value < 25);
            fill.EnableInClassList("ch-stat__fill--warn", value >= 25 && value < 45);
            label.text = value.ToString();
        }

        private void ResetLog()
        {
            log!.Clear();
            shownLines = 0;
            lastLogDay = -1;
        }

        private void AddLine(LifeLine line)
        {
            if (line.Day != lastLogDay)
            {
                lastLogDay = line.Day;
                var heading = new Label(text.Get("life.log.day", Args(("day", line.Day))));
                heading.AddToClassList("ch-week");
                log!.Add(heading);
            }

            var card = new VisualElement();
            card.AddToClassList("ch-line");
            card.AddToClassList("ch-line--new");
            var body = new Label(LineText(line));
            body.AddToClassList("ch-line__text");
            card.Add(body);
            if (line.Changes.Count > 0)
            {
                var chips = new VisualElement();
                chips.AddToClassList("ch-chips");
                foreach (KeyValuePair<string, int> change in line.Changes)
                {
                    string value = (IsArabic ? "‎" : "") + (change.Value > 0 ? "+" + change.Value : change.Value.ToString());
                    var chip = new Label(text.Get("ch.delta", Args(("value", value), ("name", text.Get("ch.stat." + change.Key)))));
                    chip.AddToClassList("ch-chip");
                    chip.AddToClassList(change.Value > 0 ? "ch-chip--good" : "ch-chip--bad");
                    chips.Add(chip);
                }
                card.Add(chips);
            }
            log!.Add(card);
            card.schedule.Execute(() => card.RemoveFromClassList("ch-line--new")).ExecuteLater(20);
            card.schedule.Execute(() => log.ScrollTo(card)).ExecuteLater(60);
        }

        private void Reject(string code) => Toast(text.Get("rule." + code), false);

        private void Toast(string message, bool good)
        {
            Label toast = Q<Label>("toast");
            toast.text = message;
            toast.EnableInClassList("ch-toast--good", good);
            toast.AddToClassList("visible");
            toastTimer = ToastSeconds;
        }

        private void Update()
        {
            if (toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
                if (toastTimer <= 0f) Q<Label>("toast").RemoveFromClassList("visible");
            }

            tickTimer += Time.unscaledDeltaTime;
            if (tickTimer >= TickSeconds)
            {
                tickTimer = 0f;
                TickNow();
            }

            UpdateCelebrations();
            if (OverlayUp) return;
            if (game == null || game.Phase != "School" || game.HasCard || game.PendingLetter != null || game.Ending != "InProgress") return;
            clockCarry += Time.unscaledDeltaTime * Paces[speedIndex];
            if (clockCarry < 1f) return;
            int minutes = (int)clockCarry;
            clockCarry -= minutes;
            game.Advance(minutes);
        }
    }
}
