#nullable enable
using SchoolDirectorTycoon.Core;

namespace SchoolDirectorTycoon.Bridge
{
    /// <summary>Plain numbers and localization keys for the HUD, read from the current state.
    /// Keys are enum names (e.g. "Won", "Absence") so the UI looks them up in String Tables.</summary>
    public readonly struct HudSnapshot
    {
        public int Day { get; }
        public int CampaignDays { get; }
        public int Cash { get; }
        public int Students { get; }
        public int Capacity { get; }
        public int Reputation { get; }
        public int Quality { get; }
        public int Tuition { get; }
        /// <summary>"InProgress", "Won" or "Lost".</summary>
        public string Outcome { get; }
        /// <summary>Pending incident name, or null.</summary>
        public string? Incident { get; }
        /// <summary>Net result of the last finished day, or null before the first report.</summary>
        public int? LastNet { get; }

        private HudSnapshot(SchoolState s)
        {
            Day = s.Day;
            CampaignDays = Simulation.CampaignDays;
            Cash = s.Cash;
            Students = s.Students;
            Capacity = s.Campus.Capacity;
            Reputation = s.Reputation;
            Quality = s.Quality;
            Tuition = s.Tuition;
            Outcome = s.Outcome.ToString();
            Incident = s.PendingIncident?.ToString();
            LastNet = s.Reports.IsDefaultOrEmpty ? (int?)null : s.Reports[s.Reports.Length - 1].Net;
        }

        public static HudSnapshot From(GameSession session) => new HudSnapshot(session.State);

        public static int Price(FacilityKind kind) => Simulation.Price((Facility)kind);
    }
}
