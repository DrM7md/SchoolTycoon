using NUnit.Framework;
using SchoolDirectorTycoon.Bridge;
using SchoolDirectorTycoon.EditorTools;

namespace SchoolDirectorTycoon.Tests
{
    public sealed class ChronicleTests
    {
        [Test]
        public void EveryEventKeyHasEnglishText()
        {
            foreach (string key in ChronicleGame.AllEventKeys())
                Assert.IsTrue(ChronicleText.English.ContainsKey(key), key);
        }

        [Test]
        public void ArabicMatchesEnglishKeysAndPlaceholders()
        {
            foreach (var pair in ChronicleText.English)
            {
                Assert.IsTrue(ChronicleText.Arabic.TryGetValue(pair.Key, out string arabic), "missing Arabic: " + pair.Key);
                CollectionAssert.AreEquivalent(Names(pair.Value), Names(arabic), pair.Key);
            }
            foreach (string key in ChronicleText.Arabic.Keys)
                Assert.IsTrue(ChronicleText.English.ContainsKey(key) || key.StartsWith("rule."), "extra Arabic: " + key);
        }

        private static System.Collections.Generic.List<string> Names(string text)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"\{(\w+)\}")) names.Add(m.Groups[1].Value);
            return names;
        }

        [Test]
        public void EveryLifeKeyHasEnglishAndArabic()
        {
            foreach (string key in LifeGame.AllKeys())
            {
                Assert.IsTrue(ChronicleText.English.ContainsKey(key), key);
                Assert.IsTrue(ChronicleText.Arabic.ContainsKey(key), key);
            }
        }

        [Test]
        public void LifeGameRestoresFromItsSave()
        {
            var game = new LifeGame(5);
            LifeDraft draft = game.CurrentDraft();
            game.Open(draft);
            game.Greet(700);
            LifeGame copy = LifeGame.Deserialize(game.Serialize());
            Assert.AreEqual(game.Serialize(), copy.Serialize());
            Assert.AreEqual(game.PendingLetter, copy.PendingLetter);
        }

        [Test]
        public void ChroniclePanelCarriesArabicShapingData()
        {
            var panel = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>("Assets/_Project/UI/ChroniclePanelSettings.asset");
            Assert.IsNotNull(panel);
            Assert.IsNotNull(new UnityEditor.SerializedObject(panel).FindProperty("m_ICUDataAsset").objectReferenceValue);
        }

        [Test]
        public void GameSendsDecisionsAndReportsRejections()
        {
            var game = new ChronicleGame(11);
            string rejected = null;
            game.Rejected += code => rejected = code;
            game.Choose("nothing");
            Assert.AreEqual("ChronicleNoPendingEvent", rejected);
            for (int i = 0; i < 80 && game.PendingEvent == null; i++) game.NextWeek();
            Assert.IsNotNull(game.PendingEvent);
            Assert.GreaterOrEqual(game.PendingChoices().Count, 2);
            game.Choose(game.PendingChoices()[0]);
            Assert.IsNull(game.PendingEvent);
        }
    }
}
