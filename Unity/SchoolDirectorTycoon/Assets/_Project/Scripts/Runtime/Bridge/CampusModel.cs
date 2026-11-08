#nullable enable
using System.Collections.Generic;
using SchoolDirectorTycoon.Core;

namespace SchoolDirectorTycoon.Bridge
{
    /// <summary>Mirror of <see cref="Facility"/> so presentation assemblies never reference Core.
    /// Values must match; a test enforces it.</summary>
    public enum FacilityKind { Classroom, Lab, Library, Bus, Canteen }

    public readonly struct PlacedBuilding
    {
        public PlacedBuilding(FacilityKind kind, int column, int row) { Kind = kind; Column = column; Row = row; }
        public FacilityKind Kind { get; }
        public int Column { get; }
        public int Row { get; }
    }

    /// <summary>Read-only campus queries and campus commands for the World layer. Holds no rules:
    /// every answer comes from <see cref="CampusLayout"/> or the current <see cref="SchoolState"/>.</summary>
    public static class CampusModel
    {
        public const int Columns = CampusLayout.Columns;
        public const int Rows = CampusLayout.Rows;
        public const int MainPathRow = CampusLayout.MainPathRow;
        public const int FacilityCount = 5;

        public static bool IsBuildable(int column, int row) =>
            CampusLayout.IsBuildable(new CampusTile(column, row));

        public static bool IsOccupied(GameSession session, int column, int row)
        {
            var tile = new CampusTile(column, row);
            foreach (CampusBuilding building in session.State.Buildings)
                if (building.Tile == tile) return true;
            return false;
        }

        public static List<PlacedBuilding> Buildings(GameSession session)
        {
            var result = new List<PlacedBuilding>(session.State.Buildings.Length);
            foreach (CampusBuilding building in session.State.Buildings)
                result.Add(new PlacedBuilding((FacilityKind)building.Facility, building.Tile.Column, building.Tile.Row));
            return result;
        }

        /// <returns>True when Core accepted the build; otherwise <see cref="GameSession.CommandRejected"/> fired.</returns>
        public static bool Build(GameSession session, FacilityKind kind, int column, int row) =>
            session.Dispatch(state => Simulation.Build(state, (Facility)kind, new CampusTile(column, row)));

        public static bool EndDay(GameSession session) => session.Dispatch(Simulation.AdvanceDay);

        public static bool ResolveIncident(GameSession session, bool intervene) =>
            session.Dispatch(state => Simulation.ResolveIncident(state, intervene));

        /// <summary>Cost of intervening in the pending incident, or 0 when none is pending.</summary>
        public static int PendingIncidentCost(GameSession session) =>
            session.State.PendingIncident is Incident incident ? Simulation.IncidentCost(incident) : 0;

        /// <summary>Calls <paramref name="onChanged"/> after each applied command, without exposing Core
        /// types to the caller. Dispose the result to unsubscribe.</summary>
        public static System.IDisposable OnChanged(GameSession session, System.Action onChanged)
        {
            System.Action<SchoolState, SchoolState> handler = (_, __) => onChanged();
            session.StateChanged += handler;
            return new Subscription(() => session.StateChanged -= handler);
        }

        /// <summary>Same as <see cref="OnChanged"/> for rejections; the rule code is passed as its name
        /// (a localization key), so callers need no Core reference.</summary>
        public static System.IDisposable OnRejected(GameSession session, System.Action<string> onRejected)
        {
            System.Action<RuleCode> handler = code => onRejected(code.ToString());
            session.CommandRejected += handler;
            return new Subscription(() => session.CommandRejected -= handler);
        }

        private sealed class Subscription : System.IDisposable
        {
            private System.Action? dispose;
            public Subscription(System.Action dispose) { this.dispose = dispose; }
            public void Dispose() { dispose?.Invoke(); dispose = null; }
        }
    }
}
