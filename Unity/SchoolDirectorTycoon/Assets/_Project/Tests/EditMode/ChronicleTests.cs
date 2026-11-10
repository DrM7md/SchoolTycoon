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

        private static string CareerFile() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sdt-career-" + System.Guid.NewGuid().ToString("N") + ".txt");

        [Test]
        public void CareerFileRoundTripsAndToleratesDamage()
        {
            string path = CareerFile();
            var missing = new LifeGame(5);
            missing.UseCareer(path);
            Assert.AreEqual(1, missing.CareerLevel);
            Assert.IsFalse(System.IO.File.Exists(path));

            var career = new SchoolDirectorTycoon.Core.CareerState { Xp = 900, Level = 4 };
            System.IO.File.WriteAllText(path, SchoolDirectorTycoon.Core.CareerSave.Write(career));
            var game = new LifeGame(5);
            game.UseCareer(path);
            Assert.AreEqual(4, game.CareerLevel);
            Assert.AreEqual(900, game.CareerXp);
            Assert.AreEqual(SchoolDirectorTycoon.Core.Career.StartingCash(4), game.StartingCash);
            Assert.IsTrue(game.SaveCareer());

            System.IO.File.WriteAllText(path, "not a career");
            var bad = new LifeGame(5);
            bad.UseCareer(path);
            Assert.AreEqual(1, bad.CareerLevel);
            Assert.IsTrue(System.IO.File.Exists(path + ".bad"));
            System.IO.File.Delete(path);
            System.IO.File.Delete(path + ".bad");
        }

        [Test]
        public void FinishedYearIsBankedOnceAndShowsLevelUp()
        {
            string path = CareerFile();
            var game = new LifeGame(9);
            game.UseCareer(path);
            game.Open(game.CurrentDraft());
            game.Greet(700);
            for (int i = 0; i < 40000 && game.Ending == "InProgress"; i++)
            {
                if (game.PendingLetter != null) { var options = game.LetterChoices(); game.ReadLetter(options.Count > 0 ? options[0] : null); }
                else if (game.HasCard)
                {
                    foreach (var choice in game.CardChoices()) if (game.CardCost(choice.id) <= game.Cash) { game.ResolveCard(choice.id); break; }
                }
                else if (game.Phase == "School") game.Advance(100000);
                else game.Evening("home");
            }
            Assert.AreNotEqual("InProgress", game.Ending);
            int xp = game.CareerXp;
            Assert.Greater(xp, 0);
            Assert.AreEqual(game.XpGained, xp);
            bool levelUp = false;
            for (int i = 0; i < 40 && game.CelebrationCount > 0; i++)
            {
                if (game.TopCelebration!.Value.kind == "level") levelUp = true;
                game.AckCelebration();
            }
            Assert.AreEqual(game.CareerLevel > 1, levelUp);
            var reload = LifeGame.Deserialize(game.Serialize());
            reload.UseCareer(path);
            Assert.AreEqual(xp, reload.CareerXp, "the same run must not pay twice");
            System.IO.File.Delete(path);
        }

        [Test]
        public void VillageForcesVillaAndHonoursFeeCap()
        {
            var game = new LifeGame(5);
            LifeDraft d = game.CurrentDraft();
            d.Scenario = "village";
            d.Fee = 480;
            d.Building = "campus";
            d.Seats = 180;
            LifeDraft c = LifeGame.Clamp(d);
            Assert.AreEqual("villa", c.Building);
            Assert.LessOrEqual(c.Fee, 200);
            Assert.LessOrEqual(c.Seats, 40);
        }

        [Test]
        public void EveryV3CardAndOverlayKeyHasBothLanguages()
        {
            foreach (string kind in new[] { "seed", "scandal", "absent", "results", "home", "arc", "month" })
                Assert.IsTrue(System.Linq.Enumerable.Any(LifeGame.AllKeys(), k => k.StartsWith("life." + kind) || (kind == "month" && k == "life.month.body")), kind);
            foreach (string key in LifeGame.AllKeys())
                if (key.StartsWith("life.") || key.StartsWith("rule."))
                {
                    Assert.IsTrue(ChronicleText.English.ContainsKey(key), "en: " + key);
                    Assert.IsTrue(ChronicleText.Arabic.ContainsKey(key), "ar: " + key);
                }
        }

        [Test]
        public void EveryLiteralKeyInTheUiExists()
        {
            string[] files = { "Assets/_Project/Scripts/Runtime/UI/LifeController.cs", "Assets/_Project/Scripts/Runtime/UI/LifeController.Life3.cs" };
            foreach (string file in files)
            {
                string source = System.IO.File.ReadAllText(file);
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(source, "\"((?:life|rule|ch)\\.[a-z0-9_.]+)\""))
                {
                    string key = m.Groups[1].Value;
                    if (key.EndsWith(".") || key == "life.save" || key == "life.career") continue;
                    Assert.IsTrue(ChronicleText.English.ContainsKey(key), "en: " + key);
                    Assert.IsTrue(ChronicleText.Arabic.ContainsKey(key), "ar: " + key);
                }
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
        public void LifeDraftKeepsItsTimesAndIdentityWithinLimits()
        {
            var game = new LifeGame(5);
            LifeDraft draft = game.CurrentDraft();
            Assert.AreEqual(45, draft.PeriodMinutes);
            Assert.AreEqual(20, draft.BreakMinutes);
            draft.PeriodMinutes = 90; draft.BreakMinutes = 1; draft.Colour = -1; draft.SchoolName = 8; draft.Crest = 6;
            LifeDraft clamped = LifeGame.Clamp(draft);
            Assert.AreEqual(60, clamped.PeriodMinutes);
            Assert.AreEqual(10, clamped.BreakMinutes);
            Assert.AreEqual(LifeGame.Colours - 1, clamped.Colour);
            Assert.AreEqual(0, clamped.SchoolName);
            Assert.AreEqual(0, clamped.Crest);
        }

        [Test]
        public void LifeGameExposesThePeopleAndProjectsAfterOpening()
        {
            var game = new LifeGame(5);
            LifeDraft draft = game.CurrentDraft();
            draft.Colour = 3;
            game.Open(draft);
            Assert.AreEqual(3, game.Colour);
            Assert.AreEqual(draft.Teachers, game.Roster().Count);
            Assert.IsNull(game.Roster()[0].trait);
            Assert.AreEqual(5, game.Followed().Count);
            Assert.IsNotEmpty(game.Upgrades());
            Assert.IsNotEmpty(game.Courses());
            Assert.AreEqual(0, game.TrainingSlots);
            Assert.IsFalse(game.HasCard);
        }

        [Test]
        public void LifeGameRefusesEveningCommandsDuringSchool()
        {
            var game = new LifeGame(5);
            game.Open(game.CurrentDraft());
            string rejected = null;
            game.Rejected += code => rejected = code;
            game.TakeLoan();
            Assert.AreEqual("LifeWrongPhase", rejected);
        }

        [Test]
        public void LifeGameFinishesAProjectOnTheRealTimeClock()
        {
            var game = new LifeGame(5);
            game.Open(game.CurrentDraft());
            game.Advance(100000);
            for (int i = 0; i < 20 && game.Phase != "Evening"; i++)
            {
                if (game.HasCard) game.ResolveCard(game.CardChoices()[0].id);
                game.Advance(100000);
            }
            Assert.AreEqual("Evening", game.Phase);
            game.StartUpgrade("garden", 5000);
            Assert.AreEqual("garden", game.BuildingJob);
            Assert.AreEqual(60, game.BuildingRemaining);
            game.Tick(5059);
            Assert.AreEqual("garden", game.BuildingJob);
            game.Tick(5060);
            Assert.IsNull(game.BuildingJob);
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
