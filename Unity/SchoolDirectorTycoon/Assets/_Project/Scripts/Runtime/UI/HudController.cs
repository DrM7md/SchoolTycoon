#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using SchoolDirectorTycoon.Bridge;
using SchoolDirectorTycoon.World;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace SchoolDirectorTycoon.UI
{
    /// <summary>Presents the session: stats, time controls, incident decision, outcome, build bar and
    /// rejection toasts. Reads state through Bridge and sends decisions back through it; holds no rules.
    /// No audio: every result is shown (colour, toast, highlight).</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudController : MonoBehaviour
    {
        private const float ToastSeconds = 2.8f;

        [SerializeField] private CampusBootstrap? bootstrap;
        [SerializeField] private CampusPalette? palette;

        private IHudText text = new LocalizedHudText();
        private GameSession? session;
        private PlacementController? placement;
        private DayClock? clock;
        private IDisposable? changed, rejected;
        private VisualElement? root;
        private Label? day, cash, students, reputation, quality, net, toast, outcome, incidentText, buildHint;
        private VisualElement? incident;
        private Button? endDay, intervene, ignore;
        private readonly List<Button> speedButtons = new List<Button>();
        private readonly List<Button> facilityButtons = new List<Button>();
        private float toastTimer;

        public void UseText(IHudText source) => text = source;

        /// <summary>True once the HUD is listening to the session.</summary>
        public bool IsBound => session != null;

        private IEnumerator Start()
        {
            // Wait for the Localization system so the first frame never shows raw keys.
            yield return LocalizationSettings.InitializationOperation;
            if (LocalizationSettings.SelectedLocale == null && LocalizationSettings.AvailableLocales.Locales.Count > 0)
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[0];

            if (bootstrap == null || bootstrap.Session == null)
            {
                Debug.LogError("HudController: CampusBootstrap has no session.", this);
                yield break;
            }
            Bind(bootstrap.Session, bootstrap.Placement, bootstrap.Clock, GetComponent<UIDocument>().rootVisualElement);
            LocalizationSettings.SelectedLocaleChanged += _ => Refresh();
        }

        /// <summary>Builds the HUD on <paramref name="rootElement"/>; public so tests can drive it without a scene.</summary>
        public void Bind(GameSession newSession, PlacementController? newPlacement, DayClock? newClock, VisualElement rootElement)
        {
            changed?.Dispose();
            rejected?.Dispose();
            session = newSession;
            placement = newPlacement;
            clock = newClock;
            root = rootElement;

            day = Q<Label>("day"); cash = Q<Label>("cash"); students = Q<Label>("students");
            reputation = Q<Label>("reputation"); quality = Q<Label>("quality"); net = Q<Label>("net");
            toast = Q<Label>("toast"); outcome = Q<Label>("outcome"); incidentText = Q<Label>("incident-text");
            buildHint = Q<Label>("build-hint"); incident = Q<VisualElement>("incident");
            endDay = Q<Button>("end-day"); intervene = Q<Button>("intervene"); ignore = Q<Button>("ignore");

            endDay.clicked += () => CampusModel.EndDay(session);
            intervene.clicked += () => CampusModel.ResolveIncident(session, intervene: true);
            ignore.clicked += () => CampusModel.ResolveIncident(session, intervene: false);

            VisualElement speeds = Q<VisualElement>("speeds");
            speeds.Clear();
            speedButtons.Clear();
            foreach (int speed in DayClock.Speeds)
            {
                int s = speed;
                var button = new Button(() => { clock?.SetSpeed(s); Refresh(); }) { userData = s };
                button.AddToClassList("button");
                speeds.Add(button);
                speedButtons.Add(button);
            }

            VisualElement facilities = Q<VisualElement>("facilities");
            facilities.Clear();
            facilityButtons.Clear();
            foreach (FacilityKind kind in Enum.GetValues(typeof(FacilityKind)))
            {
                FacilityKind k = kind;
                var button = new Button(() => placement?.Select(placement.Selected == k ? (FacilityKind?)null : k)) { userData = k };
                button.AddToClassList("button");
                button.AddToClassList("facility");
                var swatch = new VisualElement();
                swatch.AddToClassList("facility__swatch");
                if (palette != null && palette.StyleFor(k).body != null)
                {
                    Color c = palette.StyleFor(k).body.GetColor("_BaseColor");
                    swatch.style.backgroundColor = c;
                    button.style.borderBottomColor = c;
                }
                var label = new Label { name = "label" };
                label.AddToClassList("facility__label");
                button.Add(swatch);
                button.Add(label);
                facilities.Add(button);
                facilityButtons.Add(button);
            }

            if (placement != null)
            {
                placement.SelectionChanged += Refresh;
                placement.IsPointerOverUi = IsOverUi;
            }
            changed = CampusModel.OnChanged(session, Refresh);
            rejected = CampusModel.OnRejected(session, ShowRejection);
            Refresh();
        }

        private T Q<T>(string name) where T : VisualElement =>
            root!.Q<T>(name) ?? throw new InvalidOperationException($"HUD element '{name}' missing from Hud.uxml");

        private void OnDestroy()
        {
            changed?.Dispose();
            rejected?.Dispose();
            if (placement != null) placement.SelectionChanged -= Refresh;
        }

        private bool IsOverUi(Vector2 screen)
        {
            if (root?.panel == null) return false;
            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            VisualElement? picked = root.panel.Pick(panelPoint);
            return picked != null && picked != root;
        }

        public void Refresh()
        {
            if (session == null || root == null) return;
            HudSnapshot s = HudSnapshot.From(session);

            day!.text = text.Get(HudKeys.Day, Args(("day", Math.Min(s.Day, s.CampaignDays)), ("total", s.CampaignDays)));
            cash!.text = text.Get(HudKeys.Cash, Args(("cash", s.Cash)));
            cash.EnableInClassList("negative", s.Cash < 0);
            students!.text = text.Get(HudKeys.Students, Args(("students", s.Students), ("capacity", s.Capacity)));
            reputation!.text = text.Get(HudKeys.Reputation, Args(("value", s.Reputation)));
            quality!.text = text.Get(HudKeys.Quality, Args(("value", s.Quality)));

            net!.EnableInClassList("hidden", s.LastNet == null);
            if (s.LastNet is int lastNet)
            {
                net.text = text.Get(HudKeys.Net, Args(("net", lastNet)));
                net.EnableInClassList("positive", lastNet >= 0);
                net.EnableInClassList("negative", lastNet < 0);
            }

            bool playing = s.Outcome == "InProgress";
            bool waiting = s.Incident != null;
            endDay!.text = text.Get(HudKeys.EndDay);
            endDay.SetEnabled(playing && !waiting);
            foreach (Button b in speedButtons)
            {
                int speed = (int)b.userData;
                b.text = speed == 0 ? text.Get(HudKeys.Pause) : text.Get(HudKeys.Speed, Args(("speed", speed)));
                b.EnableInClassList("is-active", clock != null && clock.Speed == speed);
                b.SetEnabled(clock != null && playing);
            }

            incident!.EnableInClassList("hidden", !waiting || !playing);
            if (waiting)
            {
                incidentText!.text = text.Get(HudKeys.Incident(s.Incident!));
                intervene!.text = text.Get(HudKeys.Intervene, Args(("cost", CampusModel.PendingIncidentCost(session))));
                ignore!.text = text.Get(HudKeys.Ignore);
            }

            outcome!.EnableInClassList("hidden", playing);
            outcome.EnableInClassList("won", s.Outcome == "Won");
            outcome.EnableInClassList("lost", s.Outcome == "Lost");
            if (!playing) outcome.text = text.Get(HudKeys.Outcome(s.Outcome));

            FacilityKind? selected = placement?.Selected;
            buildHint!.text = selected is FacilityKind sel
                ? text.Get(HudKeys.BuildSelected, Args(("name", text.Get(HudKeys.Facility(sel.ToString())))))
                : text.Get(HudKeys.BuildHint);
            for (int i = 0; i < facilityButtons.Count; i++)
            {
                Button b = facilityButtons[i];
                var kind = (FacilityKind)b.userData;
                b.Q<Label>("label").text = text.Get(HudKeys.BuildItem, Args(
                    ("key", i + 1), ("name", text.Get(HudKeys.Facility(kind.ToString()))), ("price", HudSnapshot.Price(kind))));
                b.EnableInClassList("is-active", selected == kind);
                b.SetEnabled(playing);
            }
        }

        private void ShowRejection(string code)
        {
            if (toast == null) return;
            toast.text = text.Get(HudKeys.Rule(code));
            toast.AddToClassList("visible");
            toastTimer = ToastSeconds;
        }

        private void Update()
        {
            if (toastTimer <= 0f) return;
            toastTimer -= Time.unscaledDeltaTime;
            if (toastTimer <= 0f) toast?.RemoveFromClassList("visible");
        }

        private static Dictionary<string, object> Args(params (string name, object value)[] pairs)
        {
            var args = new Dictionary<string, object>(pairs.Length);
            foreach (var (name, value) in pairs) args[name] = value;
            return args;
        }
    }
}
