#nullable enable
using System.Collections;
using System.Text;
using SchoolDirectorTycoon.Bridge;
using SchoolDirectorTycoon.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace SchoolDirectorTycoon.UI
{
    /// <summary>Development builds only (<c>-sdtPlayTest &lt;report.txt&gt;</c>): plays through the shipped input
    /// actions and HUD using virtual mouse and keyboard devices, and writes PASS/FAIL per step.</summary>
    public static class DevPlayTest
    {
        public static IEnumerator Run(CampusBootstrap bootstrap, HudController hud, UIDocument document, string file)
        {
            for (int i = 0; i < 300 && (bootstrap.Session == null || !hud.IsBound); i++) yield return null;
            var report = new StringBuilder();
            int failures = 0;
            void Check(string step, bool ok, string detail = "")
            {
                if (!ok) failures++;
                report.Append(ok ? "PASS " : "FAIL ").Append(step).Append(detail.Length > 0 ? " (" + detail + ")" : "").Append('\n');
            }

            GameSession session = bootstrap.Session!;
            PlacementController placement = bootstrap.Placement!;
            Camera cam = Camera.main;
            VisualElement root = document.rootVisualElement;

            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach (InputDevice device in InputSystem.devices.ToArray())
                if (device is Mouse || device is Keyboard) InputSystem.DisableDevice(device);
            var mouse = InputSystem.AddDevice<Mouse>("PlayTestMouse");
            var keyboard = InputSystem.AddDevice<Keyboard>("PlayTestKeyboard");
            mouse.MakeCurrent();
            keyboard.MakeCurrent();
            var pointer = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
            IEnumerator Move(Vector2 to)
            {
                pointer = to;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = to });
                yield return Frames(4);
            }
            IEnumerator Click(Vector2 at)
            {
                yield return Move(at);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = at }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, true));
                yield return Frames(4);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = at });
                yield return Frames(4);
            }
            IEnumerator Press(Key key, int hold = 3)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                yield return Frames(hold);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Frames(4);
            }
            Vector2 ButtonScreen(string name)
            {
                Rect b = root.Q<VisualElement>(name).worldBound;
                return new Vector2(b.center.x * Screen.width / root.worldBound.width, Screen.height * (1f - b.center.y / root.worldBound.height));
            }
            Vector2 ElementScreen(VisualElement e)
            {
                Rect b = e.worldBound;
                return new Vector2(b.center.x * Screen.width / root.worldBound.width, Screen.height * (1f - b.center.y / root.worldBound.height));
            }
            int Built() { int n = 0; foreach (PlacedBuilding _ in CampusModel.Buildings(session)) n++; return n; }
            bool FreeLot(out Vector2 screen)
            {
                for (int row = 0; row < CampusModel.Rows; row++)
                for (int column = 0; column < CampusModel.Columns; column++)
                {
                    if (!CampusModel.IsBuildable(column, row) || CampusModel.IsOccupied(session, column, row)) continue;
                    Vector3 p = cam.WorldToScreenPoint(GridMetrics.CellCenter(column, row));
                    if (p.x > Screen.width * 0.25f && p.x < Screen.width * 0.75f && p.y > Screen.height * 0.3f && p.y < Screen.height * 0.8f)
                    {
                        screen = p;
                        return true;
                    }
                }
                screen = default;
                return false;
            }

            yield return Frames(30);
            Check("free lot visible", FreeLot(out Vector2 lot));

            yield return Press(Key.Digit5);
            Check("key 5 selects canteen", placement.Selected == FacilityKind.Canteen);
            int before = Built();
            yield return Click(lot);
            Check("click builds on lot", Built() == before + 1, $"{before}->{Built()}");
            yield return Click(lot);
            Check("click on occupied lot is rejected", Built() == before + 1 && placement.LastRejection != null, placement.LastRejection ?? "no rejection");

            int count = Built();
            yield return Click(ButtonScreen("speeds") );
            Check("click over HUD does not build", Built() == count);
            yield return Press(Key.Escape);
            Check("Esc clears selection", placement.Selected == null);

            VisualElement speeds = root.Q<VisualElement>("speeds");
            yield return Click(ElementScreen(speeds[1]));
            Check("HUD 1x button", bootstrap.Clock != null && bootstrap.Clock.Speed == 1, "speed=" + bootstrap.Clock?.Speed);
            yield return Click(ElementScreen(speeds[0]));
            Check("HUD pause button", bootstrap.Clock != null && bootstrap.Clock.Speed == 0, "speed=" + bootstrap.Clock?.Speed);

            yield return Click(ElementScreen(root.Q<VisualElement>("facilities")[3]));
            Check("HUD build bar selects bus", placement.Selected == FacilityKind.Bus);
            yield return Press(Key.Escape);

            Vector3 camPos = cam.transform.position;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer, scroll = new Vector2(0f, 120f) });
            yield return Frames(40);
            Check("wheel zooms", Vector3.Distance(camPos, cam.transform.position) > 0.5f);
            Quaternion camRot = cam.transform.rotation;
            yield return Press(Key.Q, 40);
            yield return Frames(30);
            Check("Q rotates", Quaternion.Angle(camRot, cam.transform.rotation) > 1f);
            camPos = cam.transform.position;
            yield return Press(Key.W, 40);
            yield return Frames(30);
            Check("W pans", Vector3.Distance(camPos, cam.transform.position) > 0.5f);

            int day = HudSnapshot.From(session).Day;
            bool sawIncident = HudSnapshot.From(session).Incident != null;
            if (!sawIncident)
            {
                yield return Press(Key.N);
                Check("N ends the day", HudSnapshot.From(session).Day == day + 1 || HudSnapshot.From(session).Incident != null, "day " + day + "->" + HudSnapshot.From(session).Day);
            }
            for (int i = 0; i < 12 && HudSnapshot.From(session).Incident == null; i++)
            {
                day = HudSnapshot.From(session).Day;
                yield return Click(ButtonScreen("end-day"));
                if (i == 0) Check("HUD End Day button", HudSnapshot.From(session).Day == day + 1 || HudSnapshot.From(session).Incident != null, "day " + day + "->" + HudSnapshot.From(session).Day);
                if (HudSnapshot.From(session).Outcome != "InProgress") break;
            }
            if (HudSnapshot.From(session).Incident != null)
            {
                yield return Click(ButtonScreen("ignore"));
                Check("HUD ignore resolves incident", HudSnapshot.From(session).Incident == null);
            }
            else report.Append("SKIP incident: none appeared\n");

            report.Append("failures=").Append(failures).Append('\n');
            System.IO.File.WriteAllText(file, report.ToString());
            Application.Quit(failures == 0 ? 0 : 3);
        }
    }
}
