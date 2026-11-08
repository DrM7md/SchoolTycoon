using System.Collections.Generic;
using System.IO;
using SchoolDirectorTycoon.UI;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace SchoolDirectorTycoon.EditorTools
{
    /// <summary>Creates the Localization settings, the English locale and the "UI" String Table, and
    /// adds any missing English entries. Existing entries are never overwritten, so edits made in the
    /// Localization Tables window survive a re-run. English is the reference table; Arabic comes later.</summary>
    public static class LocalizationSeed
    {
        private const string Dir = "Assets/_Project/Localization";

        /// <summary>English seed text. Arguments are named Smart String placeholders.</summary>
        public static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
        {
            [HudKeys.Day] = "Day {day} / {total}",
            [HudKeys.Cash] = "Cash {cash:N0}",
            [HudKeys.Students] = "Students {students} / {capacity}",
            [HudKeys.Reputation] = "Reputation {value}",
            [HudKeys.Quality] = "Quality {value}",
            [HudKeys.Net] = "Yesterday {net:+#,0;-#,0;0}",
            [HudKeys.EndDay] = "End Day",
            [HudKeys.Pause] = "Pause",
            [HudKeys.Speed] = "{speed}×",
            [HudKeys.BuildHint] = "Choose a facility (1–5), then click a lot",
            [HudKeys.BuildSelected] = "Placing {name} — click a lot, right-click or Esc to cancel",
            [HudKeys.BuildItem] = "{key}  {name}\n{price:N0}",
            [HudKeys.Intervene] = "Intervene ({cost:N0})",
            [HudKeys.Ignore] = "Ignore",
            ["facility.Classroom"] = "Classroom",
            ["facility.Lab"] = "Lab",
            ["facility.Library"] = "Library",
            ["facility.Bus"] = "Bus",
            ["facility.Canteen"] = "Canteen",
            ["outcome.Won"] = "Campaign won! The school is thriving.",
            ["outcome.Lost"] = "Campaign lost. The school could not meet its goals.",
            ["incident.Absence"] = "A teacher is absent today. Pay for cover, or let classes suffer?",
            ["incident.Complaint"] = "A parent has filed a complaint. Respond now, or let it go?",
            ["rule.LotOnMainPath"] = "Choose a lot off the main path.",
            ["rule.LotOccupied"] = "That lot is already occupied.",
            ["rule.InsufficientCashToBuild"] = "Not enough cash to build this facility.",
            ["rule.TuitionOutOfRange"] = "Tuition is outside the allowed range.",
            ["rule.NoPendingIncident"] = "There is no incident awaiting a decision.",
            ["rule.InsufficientCashToIntervene"] = "Not enough cash to intervene.",
            ["rule.CampaignEnded"] = "The campaign has ended.",
            ["rule.IncidentPending"] = "Resolve the incident first.",
            ["rule.LimitReached"] = "You have reached the limit.",
            ["rule.RoleEmpty"] = "No employee holds this role.",
        };

        [MenuItem("SDT/Seed Localization Tables")]
        public static void Seed()
        {
            Directory.CreateDirectory(Dir);
            LocalizationSettings settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, Dir + "/LocalizationSettings.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            }

            Locale english = LocalizationEditorSettings.GetLocale(new LocaleIdentifier("en"));
            if (english == null)
            {
                english = Locale.CreateLocale(new LocaleIdentifier("en"));
                AssetDatabase.CreateAsset(english, Dir + "/English (en).asset");
                LocalizationEditorSettings.AddLocale(english);
            }

            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(LocalizedHudText.Table)
                ?? LocalizationEditorSettings.CreateStringTableCollection(LocalizedHudText.Table, Dir);
            var table = (StringTable)collection.GetTable(english.Identifier)
                ?? (StringTable)collection.AddNewTable(english.Identifier);

            foreach (KeyValuePair<string, string> pair in English)
            {
                if (table.GetEntry(pair.Key) != null) continue;
                StringTableEntry entry = table.AddEntry(pair.Key, pair.Value);
                entry.IsSmart = pair.Value.Contains("{");
            }
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
