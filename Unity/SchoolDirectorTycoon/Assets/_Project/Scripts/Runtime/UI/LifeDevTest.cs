#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.Text;
using SchoolDirectorTycoon.Bridge;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using InputMouseButton = UnityEngine.InputSystem.LowLevel.MouseButton;

namespace SchoolDirectorTycoon.UI
{
    /// <summary>Development builds only: <c>-sdtChronicle &lt;report.txt&gt; &lt;shot.png&gt;</c> plays a whole year through
    /// the real buttons with a virtual mouse, saves screenshots and quits. A watchdog writes the report if anything sticks.</summary>
    public sealed class LifeDevTest : MonoBehaviour
    {
        private const float TimeLimitSeconds = 1000f;

        private readonly StringBuilder report = new StringBuilder();
        private int failures;
        private bool finished;
        private string reportFile = "", shot = "";
        private LifeController controller = null!;
        private VisualElement root = null!;
        private Mouse mouse = null!;

        private IEnumerator Start()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, "-sdtChronicle");
            if (at < 0 || at + 2 >= args.Length || !Debug.isDebugBuild) yield break;
            reportFile = args[at + 1];
            shot = args[at + 2];

            controller = GetComponent<LifeController>();
            for (int i = 0; i < 600 && !controller.IsBound; i++) yield return null;
            root = GetComponent<UIDocument>().rootVisualElement;

            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach (InputDevice device in InputSystem.devices.ToArray())
                if (device is Mouse || device is Keyboard) InputSystem.DisableDevice(device);
            mouse = InputSystem.AddDevice<Mouse>("TestMouse");
            mouse.MakeCurrent();

            Coroutine run = StartCoroutine(Run());
            while (!finished && Time.realtimeSinceStartup < TimeLimitSeconds) yield return null;
            if (finished) yield break;
            StopCoroutine(run);
            Check("run finished within the time limit", false);
            Finish("timeout");
        }

        private void Finish(string summary)
        {
            finished = true;
            report.Append(summary).Append('\n').Append("failures=").Append(failures).Append('\n');
            System.IO.File.WriteAllText(reportFile, report.ToString());
            Application.Quit(failures == 0 ? 0 : 3);
        }

        private void Check(string step, bool ok)
        {
            if (!ok) failures++;
            report.Append(ok ? "PASS " : "FAIL ").Append(step).Append('\n');
        }

        private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        private IEnumerator Click(VisualElement? element, string label = "")
        {
            if (element == null)
            {
                Check("element exists " + label, false);
                yield break;
            }
            ScrollView? scroller = element.GetFirstAncestorOfType<ScrollView>();
            if (scroller != null)
            {
                scroller.ScrollTo(element);
                yield return Frames(4);
            }
            Rect b = element.worldBound;
            var point = new Vector2(b.center.x * Screen.width / root.worldBound.width, Screen.height * (1f - b.center.y / root.worldBound.height));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return Frames(3);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(InputMouseButton.Left, true));
            yield return Frames(3);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return Frames(4);
        }

        private IEnumerator Shot(string suffix)
        {
            yield return Frames(40);
            ScreenCapture.CaptureScreenshot(shot.Replace(".png", suffix + ".png"));
            yield return Frames(6);
        }

        private bool Visible(string name) => !root.Q<VisualElement>(name).ClassListContains("hidden");
        private bool ToastUp => root.Q<Label>("toast").ClassListContains("visible");

        private IEnumerator Tab(string id)
        {
            yield return Click(root.Q<Button>("tab-" + id), "tab " + id);
            yield return Frames(6);
            Check("tab opens " + id, controller.CurrentTab == id && Visible("panel-" + id));
        }

        private readonly Dictionary<string, int> cards = new Dictionary<string, int>();
        private int decided;

        private static readonly string[] CorruptChoices = { "change", "cover", "silence", "easy_paper", "expel", "round_up" };
        private readonly HashSet<string> seenOverlays = new HashSet<string>();
        private int disabledAnswers;

        private IEnumerator Once(string key, string suffix)
        {
            if (seenOverlays.Add(key)) yield return Shot(suffix);
        }

        /// <summary>Dismisses every celebration, report, letter and card, through the real buttons, until the school is free.</summary>
        private IEnumerator ClearDesk(LifeGame game, bool shots)
        {
            for (int guard = 0; guard < 80; guard++)
            {
                yield return Frames(3);
                if (game.CelebrationCount > 0 && Visible("celebrate")) { yield return Celebrate(game, shots); continue; }
                if (Visible("report"))
                {
                    if (shots && seenOverlays.Add("report"))
                    {
                        Check("report shows cash and tomorrow", root.Q<Label>("report-cash").text.Length > 0 && root.Q<Label>("report-tease").text.Length > 0);
                        yield return Shot("-report");
                    }
                    yield return Click(root.Q<Button>("report-ok"));
                    continue;
                }
                if (game.Ending != "InProgress") yield break;
                if (game.PendingLetter != null)
                {
                    yield return Click(root.Q<VisualElement>("letter-choices")[0]);
                    continue;
                }
                if (!game.HasCard) yield break;
                string kind = game.CardKind!, id = game.CardId!;
                if (kind == "month")
                {
                    Check("month certificate is shown", Visible("month") && root.Q<VisualElement>("month-rows").childCount == 6);
                    if (shots)
                    {
                        if (cards.ContainsKey("month") && seenOverlays.Add("month2")) yield return Shot("-month2");
                        else if (!cards.ContainsKey("month")) yield return Shot("-month");
                    }
                    yield return Click(root.Q<Button>("month-ok"));
                    cards["month"] = (cards.TryGetValue("month", out int months) ? months : 0) + 1;
                    continue;
                }
                if (shots && !cards.ContainsKey(kind)) yield return Shot("-card-" + kind);
                VisualElement choices = root.Q<VisualElement>("choices");
                if (!cards.ContainsKey(kind)) Check("card has text and an answer " + kind, root.Q<Label>("event-body").text.Length > 0 && choices.childCount >= 1);
                var enabled = new List<Button>();
                foreach (VisualElement child in choices.Children()) if (child is Button button && button.enabledSelf) enabled.Add(button); else disabledAnswers++;
                Button? pick = null;
                // Corrupt answers until the scandal has been seen, then the bot stops damaging its own school.
                if (!cards.ContainsKey("scandal")) foreach (string corrupt in CorruptChoices) foreach (Button b in enabled) if (b.name == "choice-" + corrupt) pick = b;
                if (pick == null && game.Cash < 25000) foreach (Button b in enabled) if (game.CardCost(b.name.Substring("choice-".Length)) == 0) { pick = b; break; }
                if (pick == null && enabled.Count > 0) pick = enabled[decided++ % enabled.Count];
                Check("every card has an affordable answer", pick != null);
                if (pick == null) yield break;
                yield return Click(pick);
                if (!game.HasCard || game.CardId != id || game.CardKind != kind || game.Ending != "InProgress") cards[kind] = (cards.TryGetValue(kind, out int count) ? count : 0) + 1;
            }
        }

        private IEnumerator Celebrate(LifeGame game, bool shots)
        {
            var c = game.TopCelebration!.Value;
            int before = game.CelebrationCount;
            bool first = shots && seenOverlays.Add("cele-" + c.kind);
            if (c.kind == "gift")
            {
                Check("gift starts wrapped", Visible("cele-gift") && !Visible("cele-ok"));
                if (first) yield return Shot("-cele-gift-wrapped");
                yield return Click(root.Q<Button>("cele-gift"));
                yield return Frames(4);
                Check("gift opens", !Visible("cele-gift") && Visible("cele-ok"));
            }
            if (first)
            {
                yield return new WaitForSecondsRealtime(1.5f);
                string value = root.Q<Label>("cele-value").text;
                if (c.kind == "gift" || c.kind == "challenge" || c.kind == "level")
                    Check("count-up reaches the value " + c.kind, value.Replace("\u200E", "").Replace(",", "").Contains(c.value.ToString()));
                yield return Shot("-cele-" + c.kind);
            }
            yield return Click(root.Q<Button>("cele-ok"));
            yield return Frames(3);
            Check("celebration leaves the queue " + c.kind, game.CelebrationCount == before - 1);
        }

        private IEnumerator VisitSheets(LifeGame game)
        {
            yield return Tab("home");
            Check("director tab has content", root.Q<ScrollView>("panel-home").contentContainer.childCount > 2);
            yield return Shot("-home");
            yield return Click(root.Q<Button>("home-parents"));
            yield return Frames(8);
            Check("parents chat has bubbles", root.Q<ScrollView>("sheet-body").contentContainer.childCount > 0);
            yield return Shot("-chat-parents");
            yield return Click(root.Q<Button>("sheet-close"));
            yield return Frames(4);
            yield return Click(root.Q<Button>("home-staff"));
            yield return Frames(8);
            yield return Shot("-chat-staff");
            yield return Click(root.Q<Button>("sheet-close"));
            yield return Frames(4);
            yield return Click(root.Q<Button>("home-album"));
            yield return Frames(8);
            Check("album lists achievements and titles", root.Q<ScrollView>("sheet-body").contentContainer.childCount >= 2 + LifeGame.AchievementIds.Count + LifeGame.TitleIds.Count);
            yield return Shot("-album");
            yield return Click(root.Q<Button>("sheet-close"));
            yield return Frames(4);
            yield return Click(root.Q<Button>("home-hall"));
            yield return Frames(8);
            yield return Shot("-hall");
            yield return Click(root.Q<Button>("sheet-close"));
            yield return Frames(4);

            // Timetable: two subjects are refused, three distinct are kept.
            var morning = new List<string>(game.Morning);
            yield return Click(root.Q<Button>("home-timetable"));
            yield return Frames(8);
            Check("timetable sheet opens", controller.OpenSheetId == "timetable");
            yield return Click(root.Q<Button>("tt-" + morning[0]));
            yield return Frames(6);
            yield return Click(root.Q<Button>("tt-save"));
            yield return Frames(6);
            Check("two subjects are refused with a toast", ToastUp && controller.OpenSheetId == "timetable");
            yield return Shot("-timetable");
            string added = "computing";
            foreach (string subject in LifeGame.Subjects) if (!morning.Contains(subject)) { added = subject; break; }
            yield return Click(root.Q<Button>("tt-" + added));
            yield return Frames(6);
            yield return Click(root.Q<Button>("tt-save"));
            yield return Frames(6);
            Check("timetable saved", controller.OpenSheetId == null && new List<string>(game.Morning).Contains(added));

            // Promotions and teacher of the month
            yield return Tab("people");
            var roster = game.Roster();
            yield return Click(root.Q<Button>("totm-" + roster[0].id));
            yield return Frames(6);
            int mood = game.Roster()[0].mood;
            yield return Click(root.Q<Button>("totm-" + roster[1].id));
            yield return Frames(6);
            Check("second teacher of the month is refused", ToastUp && game.Roster()[0].mood == mood);
            yield return Shot("-people-promo");
            yield return Tab("school");
        }

        private bool promoted;

        private IEnumerator TryPromotion(LifeGame game)
        {
            foreach (var t in game.Roster())
            {
                if (t.skill < 70 || t.role != null || t.remaining > 0 || t.course != null) continue;
                yield return Tab("people");
                yield return Click(root.Q<Button>("promote-head-" + t.id));
                yield return Frames(8);
                var after = game.Roster();
                foreach (var x in after) if (x.id == t.id) Check("promotion gives the role", x.role == "head");
                promoted = true;
                yield return ClearDesk(game, true);
                yield return Shot("-people-role");
                yield return Tab("school");
                yield break;
            }
        }

        private IEnumerator Run()
        {
            yield return Frames(30);
            LifeGame game = controller.Game!;
            VisualElement screen = root.Q<VisualElement>("root");

            Check("new game starts at setup", game.Phase == "Setup" && Visible("setup"));
            bool wasRtl = screen.ClassListContains("rtl");
            yield return Click(root.Q<Button>("app-settings-setup"));
            yield return Frames(20);
            Check("gear opens app settings", Visible("sheet") && controller.OpenSheetId == "app");
            yield return Shot("-app-settings");
            yield return Click(root.Q<Button>(wasRtl ? "lang-en" : "lang-ar"));
            yield return Frames(20);
            Check("language choice flips direction", screen.ClassListContains("rtl") != wasRtl);
            yield return Click(root.Q<Button>(wasRtl ? "lang-ar" : "lang-en"));
            yield return Frames(20);
            Check("language flips back", screen.ClassListContains("rtl") == wasRtl);
            yield return Click(root.Q<Button>("sheet-close"));
            yield return Frames(10);
            Check("app settings closes", !Visible("sheet"));

            Check("level one has no quick start yet", root.Q<Button>("quick-start") == null);
            Check("academy is locked at level one", root.Q<Button>("b-academy") != null && !root.Q<Button>("b-academy").enabledSelf);
            Check("village is locked at level one", root.Q<Button>("s-village") != null && !root.Q<Button>("s-village").enabledSelf);
            yield return Click(root.Q<Button>("b-academy"));
            yield return Frames(6);
            Check("locked building is not selected", controller.Draft.Building != "academy");
            yield return Shot("-setup-locked");
            yield return Click(root.Q<Button>("b-villa"));
            yield return Frames(6);
            Check("building card selects", controller.Draft.Building == "villa");
            yield return Click(root.Q<Button>("b-campus"));
            yield return Frames(6);
            // The villa clamped the seats to 90; the year needs the full campus again.
            for (int i = 0; i < 9; i++)
            {
                yield return Click(root.Q<VisualElement>("setup-form").Q<Button>("plus-seats"));
                yield return Frames(4);
            }
            yield return Frames(6);
            int teachers = controller.Draft.Teachers;
            yield return Click(root.Q<VisualElement>("setup-form").Q<Button>("plus-teachers"));
            yield return Frames(6);
            Check("teacher stepper adds one", controller.Draft.Teachers == teachers + 1);

            int school = controller.Draft.SchoolName, crest = controller.Draft.Crest, periodMinutes = controller.Draft.PeriodMinutes, breakMinutes = controller.Draft.BreakMinutes;
            string philosophy = controller.Draft.Philosophy;
            yield return Click(root.Q<Button>("plus-name"));
            yield return Frames(6);
            yield return Click(root.Q<Button>("plus-crest"));
            yield return Frames(6);
            yield return Click(root.Q<Button>("plus-colour"));
            yield return Frames(6);
            yield return Click(root.Q<Button>("plus-philosophy"));
            yield return Frames(6);
            Check("identity steppers cycle", controller.Draft.SchoolName == school + 1 && controller.Draft.Crest == crest + 1 && controller.Draft.Colour == 1 && controller.Draft.Philosophy != philosophy);
            Check("colour switches the theme class", screen.ClassListContains("theme-1"));
            yield return Click(root.Q<VisualElement>("setup-form").Q<Button>("minus-period-minutes"));
            yield return Frames(6);
            yield return Click(root.Q<VisualElement>("setup-form").Q<Button>("plus-break-minutes"));
            yield return Frames(6);
            Check("minute steppers change", controller.Draft.PeriodMinutes == periodMinutes - 5 && controller.Draft.BreakMinutes == breakMinutes + 5);
            yield return Click(root.Q<VisualElement>("setup-form").Q<Button>("plus-period-minutes"));
            yield return Frames(6);
            yield return Shot("-setup");
            yield return Click(root.Q<Button>("setup-open"));
            yield return Frames(10);
            Check("opening the school starts the clock", game.Phase == "School" && game.Cash < game.StartingCash && game.Students > 0);
            Check("school name replaces the title", root.Q<Label>("title").text.Length > 0 && game.SchoolName == school + 1);
            Check("first morning letter waits", game.PendingLetter != null && Visible("letter"));
            yield return Shot("-letter");
            int startMinute = game.Minute;
            yield return Frames(30);
            Check("clock waits while the letter is open", game.Minute == startMinute);
            yield return Click(root.Q<VisualElement>("letter-choices")[0]);
            yield return Frames(10);
            Check("letter pays and closes", game.PendingLetter == null && !Visible("letter"));

            // A first-day card covers the clock, so the player answers it before touching the pace.
            for (int guard = 0; game.HasCard && guard < 5; guard++)
            {
                yield return Click(root.Q<VisualElement>("choices")[0]);
                yield return Frames(10);
            }

            // Clock: pace, pause and the fractional carry
            float wait = Time.unscaledTime;
            yield return Click(root.Q<Button>("speed-1"));
            Check("pace is saved", PlayerPrefs.GetInt("sdt.life.pace", -1) == 1);
            while (game.Minute == startMinute && Time.unscaledTime - wait < 15f && !game.HasCard) yield return null;
            Check("slow pace carries fractions of a minute", game.Minute > startMinute);
            yield return Click(root.Q<Button>("speed-4"));
            int fast = game.Minute;
            wait = Time.unscaledTime;
            while (game.Minute < fast + 4 && Time.unscaledTime - wait < 8f && !game.HasCard) yield return null;
            Check("fastest pace runs", game.Minute >= fast + 4 || game.HasCard);
            yield return Click(root.Q<Button>("speed-0"));
            yield return Frames(3);
            int paused = game.Minute;
            yield return Frames(60);
            Check("pause stops the clock", game.Minute == paused);
            yield return ClearDesk(game, false);

            // School tab
            yield return Tab("school");
            int end = game.DayEndMinute;
            yield return Click(root.Q<Button>("extend-plus"));
            yield return Frames(4);
            Check("extend adds fifteen", game.DayExtra == 15 && game.DayEndMinute == end + 15);
            yield return Click(root.Q<Button>("extend-minus"));
            yield return Frames(4);
            Check("shorten removes fifteen", game.DayExtra == 0 && game.DayEndMinute == end);
            yield return Click(root.Q<Button>("extend-plus"));
            yield return Frames(4);
            yield return Click(root.Q<Button>("action-gate"));
            yield return Frames(6);
            Check("gate action is used", game.ActionUsed("gate") && !root.Q<Button>("action-gate").enabledSelf);
            yield return Click(root.Q<Button>("action-broadcast_study"));
            yield return Frames(6);
            Check("one broadcast a day", game.ActionUsed("broadcast") && !root.Q<Button>("action-broadcast_kindness").enabledSelf);
            yield return Shot("-school");

            // People during the day
            yield return Tab("people");
            var roster = game.Roster();
            Check("roster is listed", roster.Count == game.CurrentDraft().Teachers && root.Q<Button>("observe-" + roster[0].id) != null);
            Check("trait starts unknown", roster[0].trait == null);
            yield return Click(root.Q<Button>("observe-" + roster[0].id));
            yield return Frames(6);
            Check("observe reveals the trait", game.Roster()[0].trait != null && game.ActionUsed("observe"));
            yield return Click(root.Q<Button>("observe-" + roster[1].id));
            yield return Frames(6);
            Check("second observe is refused with a toast", ToastUp && game.Roster()[1].trait == null);
            int count = game.Roster().Count;
            yield return Click(root.Q<Button>("dismiss-" + roster[0].id));
            yield return Frames(6);
            Check("dismissal waits for the evening", game.Roster().Count == count && ToastUp);
            yield return Shot("-people");

            // Run to the evening, leaving tabs usable there
            yield return Tab("build");
            yield return Tab("train");
            Check("training needs a centre", root.Q<VisualElement>("train-need") != null);
            yield return Shot("-train-need");
            yield return Tab("office");
            yield return Tab("home");
            yield return Tab("school");
            for (int guard = 0; guard < 8 && game.Phase == "School" && game.Ending == "InProgress"; guard++)
            {
                yield return ClearDesk(game, true);
                if (game.Phase == "School") yield return Click(root.Q<Button>("next"));
                yield return Frames(4);
            }
            yield return ClearDesk(game, true);
            Check("evening card waits on the school tab", game.Phase == "Evening" && Visible("evening"));
            yield return Shot("-evening");

            // Build with a fake clock
            yield return Tab("build");
            Check("the evening keeps the tabs available", Visible("panel-build") && !Visible("panel-school"));
            int cash = game.Cash;
            yield return Click(root.Q<Button>("start-garden"));
            yield return Frames(6);
            Check("garden starts", game.BuildingJob == "garden" && game.Cash < cash);
            yield return Shot("-build");
            controller.SetTestNow(controller.TestNow + 120);
            yield return Frames(6);
            Check("garden finishes on the real-time clock", game.BuildingJob == null);
            Check("finish shows a toast", ToastUp);
            yield return ClearDesk(game, true);
            yield return Click(root.Q<Button>("start-training_center"));
            yield return Frames(6);
            Check("training centre starts", game.BuildingJob == "training_center");
            controller.SetTestNow(controller.TestNow + 400);
            yield return Frames(6);
            Check("training centre finishes", game.BuildingJob == null && game.TrainingSlots > 0);
            yield return ClearDesk(game, true);

            // Training
            yield return Tab("train");
            yield return Frames(6);
            int free = game.Roster()[0].id;
            int mood = game.Roster()[0].mood;
            yield return Click(root.Q<Button>("pick-" + free));
            yield return Frames(6);
            yield return Click(root.Q<Button>("course-care"));
            yield return Frames(6);
            yield return Click(root.Q<Button>("train-start"));
            yield return Frames(8);
            Check("teacher starts training", game.Roster()[0].remaining > 0);
            yield return Shot("-train");
            controller.SetTestNow(controller.TestNow + 200);
            yield return Frames(6);
            var trained = game.Roster()[0];
            Check("course finishes and helps", trained.remaining == 0 && trained.course == null && trained.mood > mood && game.CoursesFinished == 1);
            yield return ClearDesk(game, true);

            // Interview
            yield return Tab("people");
            int staff = game.Roster().Count;
            yield return Click(root.Q<Button>("people-hire"));
            yield return Frames(8);
            var candidates = game.Candidates();
            Check("three candidates arrive", candidates.Count == 3);
            if (candidates.Count == 3)
            {
                yield return Click(root.Q<Button>($"ask-{candidates[0].id}-experience"));
                yield return Frames(8);
                Check("one answer is given", game.InterviewAsked && game.InterviewReply != null && game.Candidates()[0].trait != null);
                yield return Shot("-interview");
                yield return Click(root.Q<Button>("hire-" + candidates[1].id));
                yield return Frames(8);
                Check("hire joins the roster", game.Roster().Count == staff + 1 && game.Candidates().Count == 0 && game.CurrentDraft().Teachers == staff + 1);
            }

            // Office
            yield return Tab("office");
            int beforeLoan = game.Cash;
            yield return Click(root.Q<Button>("loan-take"));
            yield return Frames(8);
            Check("loan is taken", game.LoanWeeks > 0 && game.Cash == beforeLoan + game.LoanAmount);
            yield return Shot("-office");

            // Settings
            yield return Tab("school");
            int admins = game.CurrentDraft().Admins;
            yield return Click(root.Q<Button>("evening-settings"));
            yield return Frames(8);
            Check("settings has no teacher stepper", root.Q<VisualElement>("settings-form").Q<Button>("plus-teachers") == null);
            yield return Shot("-settings");
            yield return Click(root.Q<VisualElement>("settings-form").Q<Button>("plus-admins"));
            yield return Frames(8);
            // A higher fee and evening lessons pay for the projects above, so the run can reach graduation.
            for (int i = 0; i < 4; i++)
            {
                yield return Click(root.Q<VisualElement>("settings-form").Q<Button>("plus-fee"));
                yield return Frames(6);
            }
            yield return Click(root.Q<Button>("settings-apply"));
            yield return Frames(8);
            Check("settings hire an administrator", game.CurrentDraft().Admins == admins + 1);
            Check("settings raise the fee", game.CurrentDraft().Fee == 280);

            int nights = 0, nextGreeting = 1001;
            bool visited = false;
            for (int i = 0; i < 2400 && game.Ending == "InProgress"; i++)
            {
                if (game.PendingLetter != null || game.HasCard || Visible("report") || Visible("celebrate")) yield return ClearDesk(game, true);
                else if (game.Phase == "School") yield return Click(root.Q<Button>("next"));
                else
                {
                    yield return Frames(3);
                    if (nights == 40 && !visited) { visited = true; yield return VisitSheets(game); yield return ClearDesk(game, true); continue; }
                    if (!promoted && nights > 3) { yield return TryPromotion(game); if (promoted) continue; }
                    int day = game.Day;
                    bool rest = game.Energy <= 10 || (nights % 3 == 0 && game.Cash > 40000);
                    yield return Click(root.Q<Button>(rest ? "act-home" : "act-tutoring"));
                    nights++;
                    bool moved = game.Day != day || game.Ending != "InProgress" || game.HasCard;
                    if (!moved) Check("evening moves on " + day, false);
                    if (nights % 10 == 0) controller.GreetDay(nextGreeting++);
                }
            }
            yield return ClearDesk(game, true);
            if (game.Ending == "YearCompleted") Check("graduation speech was given", cards.ContainsKey("speech"));
            Check("year reached an ending", game.Ending != "InProgress");
            Check("ending card visible", Visible("ending"));
            Check("ending shows the rank", root.Q<Label>("ending-rank").text.Length > 0);
            Check("ending shows the title", root.Q<Label>("ending-title-name").text.Length > 0);
            Check("ending shows the conscience", root.Q<Label>("ending-conscience").text.Length > 0);
            Check("ending shows epilogues", root.Q<VisualElement>("ending-epilogues").childCount == game.Followed().Count);
            Check("ending shows XP", root.Q<Label>("ending-xp").text.Length > 0);
            Check("career banked the XP once", game.CareerXp == game.XpGained && game.XpGained > 0);
            Check("career file was written", System.IO.File.Exists(controller.CareerPath));
            Check("all new overlays were cleared", !Visible("celebrate") && !Visible("report"));
            Check("save file matches the game", System.IO.File.Exists(controller.SavePath) && LifeGame.Deserialize(System.IO.File.ReadAllText(controller.SavePath)).Serialize() == game.Serialize());
            yield return Shot("-end");
            ScrollView endScroll = root.Q<ScrollView>("ending-scroll");
            endScroll.ScrollTo(root.Q<Label>("ending-xp"));
            yield return Shot("-end-bottom");
            int level = game.CareerLevel;
            yield return Click(root.Q<Button>("restart"));
            yield return Frames(10);
            Check("restart returns to setup", controller.Game != game && controller.Game!.Phase == "Setup" && Visible("setup"));
            LifeGame next = controller.Game!;
            Check("career carries over", next.CareerXp == game.CareerXp && next.CareerLevel == level);
            Check("quick start appears", root.Q<Button>("quick-start") != null);
            yield return Shot("-setup-career");
            yield return Click(root.Q<Button>("setup-album"));
            yield return Frames(8);
            yield return Shot("-album-end");
            yield return Click(root.Q<Button>("sheet-close"));
            yield return Frames(4);
            yield return Click(root.Q<Button>("setup-hall"));
            yield return Frames(8);
            Check("hall of fame lists the year", next.HallOfFame().Count == 1 && root.Q<ScrollView>("sheet-body").contentContainer.childCount == 1);
            yield return Shot("-hall-end");
            yield return Click(root.Q<Button>("sheet-close"));
            yield return Frames(4);
            if (level >= 2)
            {
                yield return Click(root.Q<Button>("s-village"));
                yield return Frames(8);
                Check("village selects the villa and caps the fee", controller.Draft.Scenario == "village" && controller.Draft.Building == "villa" && controller.Draft.Fee <= 200);
                yield return Shot("-setup-village");
                yield return Click(root.Q<Button>("s-standard"));
                yield return Frames(8);
            }
            else Check("skipped: village needs level 2 (career did not reach it)", true);
            yield return Click(root.Q<Button>("quick-start"));
            yield return Frames(10);
            Check("quick start opens the same school", next.Phase == "School" && next.SchoolName == game.SchoolName);

            var kinds = new StringBuilder();
            foreach (KeyValuePair<string, int> pair in cards) kinds.Append(pair.Key).Append(':').Append(pair.Value).Append(' ');
            PlayerPrefs.DeleteKey("sdt.life.pace");
            var overlays = new StringBuilder();
            foreach (string seen in seenOverlays) overlays.Append(seen).Append(' ');
            Finish("ending=" + game.Ending + " day=" + game.Day + " rank=" + game.Rank + " level=" + game.CareerLevel + " xp=" + game.CareerXp + " promoted=" + promoted + " disabledAnswers=" + disabledAnswers + " cards=" + kinds + "nights=" + nights + " overlays=" + overlays);
        }
    }
}
