#nullable enable
using System.Collections;
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
    /// the real buttons with a virtual mouse, saves screenshots and quits.</summary>
    public sealed class LifeDevTest : MonoBehaviour
    {
        private IEnumerator Start()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, "-sdtChronicle");
            if (at < 0 || at + 2 >= args.Length || !Debug.isDebugBuild) yield break;
            string reportFile = args[at + 1], shot = args[at + 2];

            var controller = GetComponent<LifeController>();
            for (int i = 0; i < 600 && !controller.IsBound; i++) yield return null;
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;

            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach (InputDevice device in InputSystem.devices.ToArray())
                if (device is Mouse || device is Keyboard) InputSystem.DisableDevice(device);
            var mouse = InputSystem.AddDevice<Mouse>("TestMouse");
            mouse.MakeCurrent();

            var report = new StringBuilder();
            int failures = 0;
            void Check(string step, bool ok)
            {
                if (!ok) failures++;
                report.Append(ok ? "PASS " : "FAIL ").Append(step).Append('\n');
            }
            IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
            IEnumerator Click(VisualElement element)
            {
                Rect b = element.worldBound;
                var point = new Vector2(b.center.x * Screen.width / root.worldBound.width, Screen.height * (1f - b.center.y / root.worldBound.height));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                yield return Frames(3);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(InputMouseButton.Left, true));
                yield return Frames(3);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                yield return Frames(4);
            }
            IEnumerator Shot(string suffix)
            {
                yield return Frames(40);
                ScreenCapture.CaptureScreenshot(shot.Replace(".png", suffix + ".png"));
                yield return Frames(6);
            }
            bool Visible(string name) => !root.Q<VisualElement>(name).ClassListContains("hidden");

            yield return Frames(30);
            LifeGame game = controller.Game!;
            VisualElement screen = root.Q<VisualElement>("root");

            Check("new game starts at setup", game.Phase == "Setup" && Visible("setup"));
            bool wasRtl = screen.ClassListContains("rtl");
            yield return Click(root.Q<Button>("lang-setup"));
            yield return Frames(20);
            Check("language button flips direction", screen.ClassListContains("rtl") != wasRtl);
            yield return Click(root.Q<Button>("lang-setup"));
            yield return Frames(20);
            Check("language flips back", screen.ClassListContains("rtl") == wasRtl);

            yield return Click(root.Q<Button>("b-academy"));
            yield return Frames(6);
            Check("building card selects", controller.Draft.Building == "academy");
            yield return Click(root.Q<Button>("b-campus"));
            yield return Frames(6);
            int teachers = controller.Draft.Teachers;
            yield return Click(root.Q<VisualElement>("setup-form").Q<Button>("plus-teachers"));
            yield return Frames(6);
            Check("teacher stepper adds one", controller.Draft.Teachers == teachers + 1);
            yield return Shot("-setup");
            yield return Click(root.Q<Button>("setup-open"));
            yield return Frames(10);
            Check("opening the school starts the clock", game.Phase == "School" && game.Cash < game.StartingCash && game.Students > 0);
            Check("first morning letter waits", game.PendingLetter != null && Visible("letter"));
            yield return Shot("-letter");
            int startMinute = game.Minute;
            yield return Frames(30);
            Check("clock waits while the letter is open", game.Minute == startMinute);
            yield return Click(root.Q<VisualElement>("letter-choices")[0]);
            yield return Frames(10);
            Check("letter pays and closes", game.PendingLetter == null && !Visible("letter"));

            float wait = Time.unscaledTime;
            while (game.Minute < startMinute + 8 && Time.unscaledTime - wait < 15f && game.PendingEvent == null) yield return null;
            Check("clock runs in real time", game.Minute > startMinute);
            yield return Shot("-clock");

            bool eventShot = false, eveningShot = false, settingsTried = false;
            int decided = 0, nights = 0, nextGreeting = 1001;
            for (int i = 0; i < 2400 && game.Ending == "InProgress"; i++)
            {
                if (game.PendingLetter != null)
                {
                    yield return Frames(4);
                    yield return Click(root.Q<VisualElement>("letter-choices")[0]);
                }
                else if (game.PendingEvent != null)
                {
                    yield return Frames(3);
                    if (!eventShot) { eventShot = true; yield return Shot("-event"); }
                    VisualElement choices = root.Q<VisualElement>("choices");
                    yield return Click(choices[decided++ % choices.childCount]);
                    Check("event resolves " + decided, game.PendingEvent == null);
                }
                else if (game.Phase == "School") yield return Click(root.Q<Button>("next"));
                else
                {
                    yield return Frames(3);
                    if (!eveningShot) { eveningShot = true; yield return Shot("-evening"); }
                    if (!settingsTried)
                    {
                        settingsTried = true;
                        int before = game.CurrentDraft().Admins;
                        yield return Click(root.Q<Button>("evening-settings"));
                        yield return Frames(8);
                        yield return Shot("-settings");
                        yield return Click(root.Q<VisualElement>("settings-form").Q<Button>("plus-admins"));
                        yield return Frames(8);
                        yield return Click(root.Q<Button>("settings-apply"));
                        yield return Frames(8);
                        Check("settings hire an administrator", game.CurrentDraft().Admins == before + 1);
                    }
                    int day = game.Day;
                    VisualElement options = root.Q<VisualElement>("evening-choices");
                    yield return Click(options[nights++ % options.childCount]);
                    Check("evening moves on " + day, game.Day != day || game.Ending != "InProgress");
                    if (nights % 10 == 0) controller.GreetDay(nextGreeting++);
                }
            }
            Check("year reached an ending", game.Ending != "InProgress");
            Check("ending card visible", Visible("ending"));
            Check("save file matches the game", System.IO.File.Exists(controller.SavePath) && LifeGame.Deserialize(System.IO.File.ReadAllText(controller.SavePath)).Serialize() == game.Serialize());
            yield return Shot("-end");
            yield return Click(root.Q<Button>("restart"));
            yield return Frames(10);
            Check("restart returns to setup", controller.Game != game && controller.Game!.Phase == "Setup" && Visible("setup"));

            report.Append("ending=").Append(game.Ending).Append(" day=").Append(game.Day).Append(" decisions=").Append(decided).Append(" nights=").Append(nights).Append('\n');
            report.Append("failures=").Append(failures).Append('\n');
            System.IO.File.WriteAllText(reportFile, report.ToString());
            Application.Quit(failures == 0 ? 0 : 3);
        }
    }
}
