#nullable enable
using System.Collections.Generic;
using UnityEngine.Localization.Settings;

namespace SchoolDirectorTycoon.UI
{
    /// <summary>All HUD text comes from the "UI" String Table; arguments are named (Smart Strings),
    /// never concatenated, so word order can change per language.</summary>
    public interface IHudText
    {
        string Get(string key, IDictionary<string, object>? args = null);
    }

    public sealed class LocalizedHudText : IHudText
    {
        public const string Table = "UI";

        public string Get(string key, IDictionary<string, object>? args = null)
        {
            string value = args == null
                ? LocalizationSettings.StringDatabase.GetLocalizedString(Table, key)
                : LocalizationSettings.StringDatabase.GetLocalizedString(Table, key, new object[] { args });
            // A missing entry shows the key itself, so gaps are visible instead of blank.
            return string.IsNullOrEmpty(value) ? key : value;
        }
    }

    /// <summary>Every key the HUD uses. The editor seeds the English table from <c>LocalizationSeed</c>;
    /// a test checks the two lists match.</summary>
    public static class HudKeys
    {
        public const string Day = "hud.day";
        public const string Cash = "hud.cash";
        public const string Students = "hud.students";
        public const string Reputation = "hud.reputation";
        public const string Quality = "hud.quality";
        public const string Net = "hud.net";
        public const string EndDay = "hud.endDay";
        public const string Pause = "hud.pause";
        public const string Speed = "hud.speed";
        public const string BuildHint = "build.hint";
        public const string BuildSelected = "build.selected";
        public const string BuildItem = "build.item";
        public const string Intervene = "incident.intervene";
        public const string Ignore = "incident.ignore";

        public static string Facility(string kind) => "facility." + kind;
        public static string Outcome(string outcome) => "outcome." + outcome;
        public static string Incident(string incident) => "incident." + incident;
        public static string Rule(string code) => "rule." + code;
    }
}
