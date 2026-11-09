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
    /// <summary>Portrait screens of the school life mode: setup, the running school clock, evenings, morning letters and the ending.
    /// Holds no rules and owns real time and the save file. No audio: every result is colour, motion or text.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class LifeController : MonoBehaviour
    {
        private static readonly string[] StatNames = { "Reputation", "Parents", "Staff", "Academics" };
        private static readonly int[] Speeds = { 0, 1, 2, 4 };
        private const float ToastSeconds = 2.6f;
        private const int ShownLines = 30;
        private const string LanguageKey = "sdt.language";
        private static readonly DateTime Epoch = new DateTime(2020, 1, 1);

        private readonly IHudText text = new LocalizedHudText();
        private readonly Dictionary<string, (VisualElement fill, Label value)> bars = new Dictionary<string, (VisualElement, Label)>();
        private LifeGame? game;
        private VisualElement? root;
        private ScrollView? log;
        private LifeDraft draft;
        private bool settingsOpen, testing;
        private int shownLines, lastLogDay = -1, lastSlot = -99, speedIndex = 2, dotCount = -1;
        private float toastTimer, clockCarry;

        public LifeGame? Game => game;
        public bool IsBound => game != null;
        public LifeDraft Draft => draft;

        public string SavePath => Path.Combine(Application.persistentDataPath, testing ? "life-test.save" : "life.save");
        private static int RealDay => (int)(DateTime.Now.Date - Epoch).TotalDays;

        private IEnumerator Start()
        {
            yield return LocalizationSettings.InitializationOperation;
            if (LocalizationSettings.SelectedLocale == null && LocalizationSettings.AvailableLocales.Locales.Count > 0)
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[0];
            yield return SelectLanguage(StartingLanguage());
            testing = Array.IndexOf(Environment.GetCommandLineArgs(), "-sdtChronicle") >= 0;
            root = GetComponent<UIDocument>().rootVisualElement;
            root.schedule.Execute(() => ApplySafeArea(root));
            Wire();
            Use(testing ? NewGame() : LoadOrNew());
            ApplyLanguage();
            if (!testing) game!.Greet(RealDay);
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
        private void OnApplicationFocus(bool focused) { if (focused && !testing) game?.Greet(RealDay); }

        /// <summary>Lets the automated run pretend a new calendar day has begun.</summary>
        public void GreetDay(int day) => game?.Greet(day);

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
            Q<Label>("title").text = text.Get("ch.title");
            Q<Button>("lang").text = text.Get("ch.lang");
            Q<Button>("lang-setup").text = text.Get("ch.lang");
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
            for (int i = 0; i < Speeds.Length; i++) Q<Button>("speed-" + i).text = text.Get("life.speed." + i);
            foreach (string stat in StatNames) Q<Label>("name-" + stat).text = text.Get("ch.stat." + stat);
            ResetLog();
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
            Q<Button>("lang").clicked += () => StartCoroutine(ToggleLanguage());
            Q<Button>("lang-setup").clicked += () => StartCoroutine(ToggleLanguage());
            Q<Button>("next").clicked += () => game?.Advance(100000);
            Q<Button>("restart").clicked += () => { Use(NewGame()); ApplyLanguage(); Save(); };
            Q<Button>("setup-open").clicked += OpenSchool;
            Q<Button>("evening-settings").clicked += () => { settingsOpen = true; draft = game!.CurrentDraft(); RebuildForms(); Refresh(); };
            Q<Button>("settings-cancel").clicked += () => { settingsOpen = false; Refresh(); };
            Q<Button>("settings-apply").clicked += () => { settingsOpen = false; game!.Adjust(draft); Refresh(); };
            for (int i = 0; i < Speeds.Length; i++)
            {
                int index = i;
                Q<Button>("speed-" + i).clicked += () => { speedIndex = index; Refresh(); };
            }

            VisualElement stats = Q<VisualElement>("stats");
            stats.Clear();
            foreach (string stat in StatNames)
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
        }

        private void Use(LifeGame next)
        {
            if (game != null) { game.Changed -= Refresh; game.Committed -= Save; game.Rejected -= Reject; }
            game = next;
            game.Changed += Refresh;
            game.Committed += Save;
            game.Rejected += Reject;
            draft = game.CurrentDraft();
            settingsOpen = false;
            lastSlot = -99;
            dotCount = -1;
        }

        private void OpenSchool()
        {
            game!.Open(draft);
            if (game.Phase != "Setup" && !testing) game.Greet(RealDay);
            else if (game.Phase != "Setup") game.Greet(1000);
        }

        private T Q<T>(string name) where T : VisualElement =>
            root!.Q<T>(name) ?? throw new InvalidOperationException($"Element '{name}' missing from Chronicle.uxml");

        private static Dictionary<string, object> Args(params (string name, object value)[] pairs)
        {
            var args = new Dictionary<string, object>(pairs.Length);
            foreach (var (name, value) in pairs) args[name] = value;
            return args;
        }

        // Setup and settings form

        private void RebuildForms()
        {
            BuildForm(Q<VisualElement>("setup-form"), true);
            BuildForm(Q<VisualElement>("settings-form"), false);
            RefreshEstimates();
        }

        private void BuildForm(VisualElement form, bool choosingBuilding)
        {
            form.Clear();
            if (choosingBuilding)
            {
                foreach ((string id, int capacity, int rent, bool _) in LifeGame.Buildings())
                {
                    string building = id;
                    var card = new Button(() => { draft.Building = building; Edit(0, null); }) { name = "b-" + id };
                    card.AddToClassList("ch-building");
                    card.EnableInClassList("ch-building--on", draft.Building == id);
                    var title = new Label(text.Get("life.building." + id));
                    title.AddToClassList("ch-building__name");
                    var info = new Label(text.Get($"life.building.{id}.info", Args(("capacity", capacity), ("rent", rent))));
                    info.AddToClassList("ch-building__info");
                    card.Add(title);
                    card.Add(info);
                    form.Add(card);
                }
            }

            Row(form, "seats", draft.Seats.ToString(), d => { d.Seats += 10; return d; }, d => { d.Seats -= 10; return d; });
            Row(form, "fee", text.Get("ch.cash", Args(("cash", draft.Fee))), d => { d.Fee += 10; return d; }, d => { d.Fee -= 10; return d; });
            Row(form, "teachers", draft.Teachers.ToString(), d => { d.Teachers++; return d; }, d => { d.Teachers--; return d; });
            Row(form, "admins", draft.Admins.ToString(), d => { d.Admins++; return d; }, d => { d.Admins--; return d; });
            Row(form, "start", LifeGame.TimeText(draft.Start), d => { d.Start += 15; return d; }, d => { d.Start -= 15; return d; });
            Row(form, "periods", draft.Periods.ToString(), d => { d.Periods++; return d; }, d => { d.Periods--; return d; });
        }

        private void Row(VisualElement form, string id, string value, Func<LifeDraft, LifeDraft> plus, Func<LifeDraft, LifeDraft> minus)
        {
            var row = new VisualElement();
            row.AddToClassList("ch-step");
            var label = new Label(text.Get("life.form." + id));
            label.AddToClassList("ch-step__name");
            var less = new Button(() => Edit(0, minus)) { name = "minus-" + id, text = "−" };
            less.AddToClassList("ch-step__button");
            var shown = new Label(value);
            shown.AddToClassList("ch-step__value");
            var more = new Button(() => Edit(0, plus)) { name = "plus-" + id, text = "+" };
            more.AddToClassList("ch-step__button");
            row.Add(label); row.Add(less); row.Add(shown); row.Add(more);
            form.Add(row);
        }

        private void Edit(int unused, Func<LifeDraft, LifeDraft>? change)
        {
            if (change != null) draft = change(draft);
            draft = LifeGame.Clamp(draft);
            RebuildForms();
        }

        private void RefreshEstimates()
        {
            int students = LifeGame.ExpectedStudents(draft), bill = LifeGame.WeeklyBill(draft);
            Q<Label>("setup-estimate").text = text.Get("life.est.setup", Args(
                ("opening", LifeGame.OpeningCost(draft)), ("students", students), ("income", students * draft.Fee), ("bill", bill)));
            Q<Label>("settings-estimate").text = text.Get("life.est.settings", Args(("students", students), ("income", students * draft.Fee), ("bill", bill)));
        }

        // Screen state

        public void Refresh()
        {
            if (game == null || root == null) return;
            Q<VisualElement>("setup").EnableInClassList("hidden", game.Phase != "Setup");
            if (game.Phase == "Setup") HideOverlays();
            Q<Label>("week").text = text.Get("life.day", Args(("day", game.Day), ("total", game.TotalDays)));
            Q<Label>("cash").text = text.Get("ch.cash", Args(("cash", game.Cash)));
            Q<Label>("students").text = text.Get("ch.students", Args(("students", game.Students)));
            SetBar("Reputation", game.Reputation);
            SetBar("Parents", game.Parents);
            SetBar("Staff", game.Staff);
            SetBar("Academics", game.Academics);

            if (shownLines < game.LineCount - ShownLines) shownLines = game.LineCount - ShownLines;
            while (shownLines < game.LineCount) AddLine(game.Line(shownLines++));
            while (log!.contentContainer.childCount > ShownLines * 2) log.contentContainer.RemoveAt(0);

            RefreshClock();
            if (game.Phase == "Setup") return;

            bool over = game.Ending != "InProgress", letter = !over && game.PendingLetter != null;
            bool decide = !over && !letter && game.PendingEvent != null;
            bool evening = !over && !letter && !decide && game.Phase == "Evening";
            Show("ending", over);
            Show("letter", letter);
            Show("event", decide);
            Show("evening", evening && !settingsOpen);
            Show("settings", evening && settingsOpen);
            Q<Button>("next").SetEnabled(!over && !letter && !decide && game.Phase == "School");
            Q<Button>("next").text = text.Get("life.clock.skip");

            if (letter) FillLetter();
            if (decide) FillEvent();
            if (evening && !settingsOpen) FillEvening();
            if (over) FillEnding();
        }

        private void Show(string name, bool visible) => Q<VisualElement>(name).EnableInClassList("hidden", !visible);

        private void HideOverlays()
        {
            foreach (string name in new[] { "ending", "letter", "event", "evening", "settings" }) Show(name, false);
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

            for (int i = 0; i < Speeds.Length; i++) Q<Button>("speed-" + i).EnableInClassList("ch-speed--on", i == speedIndex);

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
            Q<Label>("letter-tag").text = text.Get("life.letter.tag", Args(("streak", game!.Streak)));
            Q<Label>("letter-body").text = text.Get(game.PendingLetter!);
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
                choices.Add(Choice("letter-" + id, text.Get($"{game.PendingLetter}.opt.{id}"), () => game.ReadLetter(option)));
            }
        }

        private void FillEvent()
        {
            Q<Label>("event-body").text = text.Get("ev." + game!.PendingEvent);
            VisualElement choices = Q<VisualElement>("choices");
            choices.Clear();
            foreach (string id in game.PendingChoices())
            {
                string choice = id;
                choices.Add(Choice("choice-" + id, text.Get($"ev.{game.PendingEvent}.{id}"), () => game.Choose(choice)));
            }
        }

        private void FillEvening()
        {
            VisualElement choices = Q<VisualElement>("evening-choices");
            choices.Clear();
            foreach (string id in game!.Activities())
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

        private void AddLine(ChronicleLine line)
        {
            if (line.Week != lastLogDay)
            {
                lastLogDay = line.Week;
                var heading = new Label(text.Get("life.log.day", Args(("day", line.Week))));
                heading.AddToClassList("ch-week");
                log!.Add(heading);
            }

            var card = new VisualElement();
            card.AddToClassList("ch-line");
            card.AddToClassList("ch-line--new");
            var body = new Label(text.Get(line.Key));
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

        private void Reject(string code)
        {
            Label toast = Q<Label>("toast");
            toast.text = text.Get("rule." + code);
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

            if (game == null || game.Phase != "School" || game.PendingEvent != null || game.PendingLetter != null || game.Ending != "InProgress") return;
            clockCarry += Time.unscaledDeltaTime * Speeds[speedIndex];
            if (clockCarry < 1f) return;
            int minutes = (int)clockCarry;
            clockCarry -= minutes;
            game.Advance(minutes);
        }
    }
}
