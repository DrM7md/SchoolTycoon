using System;
namespace SchoolDirectorTycoon.Core
{

    public enum Facility { Classroom, Lab, Library, Bus, Canteen }
    public enum Role { Teacher, AcademicDirector, Deputy, Accountant }
    public enum Incident { Absence, Complaint }
    public enum Outcome { InProgress, Won, Lost }

    /// <summary>Language-neutral reason a command was rejected. Unity localizes by this code.</summary>
    public enum RuleCode
    {
        LotOnMainPath, LotOccupied, InsufficientCashToBuild, TuitionOutOfRange, NoPendingIncident,
        InsufficientCashToIntervene, CampaignEnded, IncidentPending, LimitReached, RoleEmpty,
        ChronicleEventPending, ChronicleNoPendingEvent, ChronicleUnknownChoice,
        LifeNotEnoughCash, LifeInvalidPlan, LifeWrongPhase, LifeUnknownActivity, LifeNoLetter, LifeLetterWaiting
    }

    public sealed record Campus(int Classrooms, int Labs, int Libraries, int Buses, int Canteens)
    {
        public int Capacity => Classrooms * 24;
    }

    public sealed record Staff(int Teachers, int AcademicDirectors, int Deputies, int Accountants, int Morale);

    public sealed record DayReport(
        int Day, int Tuition, int Transport, int Canteen, int Payroll, int Operations, int ResponseCost, int Investment,
        int Arrivals, int Departures, int ClosingCash, int Quality, int Reputation)
    {
        public int Income => Tuition + Transport + Canteen;
        public int Expenses => Payroll + Operations + ResponseCost + Investment;
        public int Net => Income - Expenses;
    }

    public sealed record SchoolState(
        int Day, int Cash, int Students, int Tuition, int Reputation, int Quality,
        Campus Campus, Staff Staff, uint RandomState, Incident? PendingIncident,
        int ResponseCost, int InvestmentToday,
        System.Collections.Immutable.ImmutableArray<DayReport> Reports,
        System.Collections.Immutable.ImmutableArray<CampusBuilding> Buildings = default)
    {
        public Outcome Outcome => Cash < 0 ? Outcome.Lost
            : Day <= Simulation.CampaignDays ? Outcome.InProgress
            : Students >= 65 && Reputation >= 70 ? Outcome.Won : Outcome.Lost;
    }

    public sealed class RuleException : InvalidOperationException
    {
        public RuleException(RuleCode code, string message, string englishMessage) : base(message)
        {
            Code = code;
            EnglishMessage = englishMessage;
        }

        public RuleCode Code { get; }
        public string EnglishMessage { get; }
    }
}
