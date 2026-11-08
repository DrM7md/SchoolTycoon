using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SchoolDirectorTycoon.Bridge;
using SchoolDirectorTycoon.Core;
using SchoolDirectorTycoon.EditorTools;
using SchoolDirectorTycoon.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolDirectorTycoon.Tests
{
    public sealed class HudTests
    {
        private sealed class KeyText : IHudText
        {
            public string Get(string key, IDictionary<string, object> args = null) =>
                args == null ? key : key + "|" + string.Join(",", args.Select(a => a.Key + "=" + a.Value));
        }

        private sealed class SilentLogger : IBridgeLogger
        {
            public void LogException(Exception error) { }
        }

        [Test]
        public void EnglishSeedCoversEveryKeyTheHudCanAskFor()
        {
            var needed = new List<string>
            {
                HudKeys.Day, HudKeys.Cash, HudKeys.Students, HudKeys.Reputation, HudKeys.Quality, HudKeys.Net,
                HudKeys.EndDay, HudKeys.Pause, HudKeys.Speed, HudKeys.BuildHint, HudKeys.BuildSelected,
                HudKeys.BuildItem, HudKeys.Intervene, HudKeys.Ignore,
                HudKeys.Outcome(nameof(Outcome.Won)), HudKeys.Outcome(nameof(Outcome.Lost)),
            };
            needed.AddRange(Enum.GetNames(typeof(RuleCode)).Select(HudKeys.Rule));
            needed.AddRange(Enum.GetNames(typeof(Facility)).Select(HudKeys.Facility));
            needed.AddRange(Enum.GetNames(typeof(Incident)).Select(HudKeys.Incident));

            var missing = needed.Where(k => !LocalizationSeed.English.ContainsKey(k)).ToList();
            Assert.IsEmpty(missing, "Missing English entries: " + string.Join(", ", missing));
        }

        [Test]
        public void HudShowsStateAndSendsDecisionsThroughBridge()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Project/UI/Hud.uxml");
            Assert.IsNotNull(tree, "Hud.uxml");
            VisualElement root = tree.CloneTree();
            var session = GameSession.New(3, new SilentLogger());
            var go = new GameObject("HUD");
            try
            {
                var hud = go.AddComponent<HudController>();
                hud.UseText(new KeyText());
                hud.Bind(session, null, null, root);

                Assert.AreEqual("hud.day|day=1,total=30", root.Q<Label>("day").text);
                StringAssert.StartsWith("hud.cash|cash=", root.Q<Label>("cash").text);
                Assert.IsTrue(root.Q("outcome").ClassListContains("hidden"));
                Assert.AreEqual(5, root.Q("facilities").childCount);

                // A rejected build shows its RuleCode text in the toast (visual feedback, no audio).
                CampusModel.Build(session, FacilityKind.Lab, 0, CampusModel.MainPathRow);
                Assert.AreEqual("rule.LotOnMainPath", root.Q<Label>("toast").text);
                Assert.IsTrue(root.Q("toast").ClassListContains("visible"));

                // Ending a day through the bridge refreshes the HUD.
                if (session.State.PendingIncident == null) CampusModel.EndDay(session);
                Assert.AreEqual($"hud.day|day={session.State.Day},total=30", root.Q<Label>("day").text);
                Assert.IsFalse(root.Q("net").ClassListContains("hidden"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
