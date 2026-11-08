using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.Immutable;

namespace SchoolDirectorTycoon.Core
{

    public static class Simulation
    {
        public const int CampaignDays = 30;
        public const int MinimumTuition = 20;
        public const int MaximumTuition = 60;
        public const int MaximumUnits = 30;
        public const int StudentsPerTeacher = 16;

        public static SchoolState NewSchool(uint seed) => new(
            1, 14_000, 40, 34, 52, 58, new(2, 0, 0, 0, 0), new(3, 0, 0, 0, 70),
            seed == 0 ? 1 : seed, null, 0, 0, ImmutableArray<DayReport>.Empty, CampusLayout.FromCounts(new(2, 0, 0, 0, 0)));

        public static int Price(Facility facility) => facility switch
        {
            Facility.Classroom => 4_500,
            Facility.Lab => 3_000,
            Facility.Library => 2_500,
            Facility.Bus => 2_200,
            Facility.Canteen => 1_800,
            _ => throw new ArgumentOutOfRangeException(nameof(facility))
        };

        public static int Salary(Role role) => role switch
        {
            Role.Teacher => 170,
            Role.AcademicDirector => 220,
            Role.Deputy => 150,
            Role.Accountant => 145,
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };

        public static SchoolState Build(SchoolState state, Facility facility)
        {
            RequireActive(state);
            var tile = CampusLayout.Lots()
                .Where(lot => !state.Buildings.Any(building => building.Tile == lot))
                .DefaultIfEmpty(new CampusTile(-1, -1))
                .First();
            return Build(state, facility, tile);
        }

        public static SchoolState Build(SchoolState state, Facility facility, CampusTile tile)
        {
            RequireActive(state);
            if (!CampusLayout.IsBuildable(tile))
                throw new RuleException(RuleCode.LotOnMainPath, "اختر قطعة أرض خارج الممر.", "Choose a lot off the main path.");
            if (state.Buildings.Any(building => building.Tile == tile))
                throw new RuleException(RuleCode.LotOccupied, "قطعة الأرض مشغولة.", "That lot is already occupied.");
            int price = Price(facility);
            if (state.Cash < price)
                throw new RuleException(RuleCode.InsufficientCashToBuild, "الرصيد لا يكفي لبناء هذا المرفق.", "Insufficient cash to build this facility.");
            Campus campus = state.Campus;
            campus = facility switch
            {
                Facility.Classroom => campus with { Classrooms = AddUnit(campus.Classrooms) },
                Facility.Lab => campus with { Labs = AddUnit(campus.Labs) },
                Facility.Library => campus with { Libraries = AddUnit(campus.Libraries) },
                Facility.Bus => campus with { Buses = AddUnit(campus.Buses) },
                Facility.Canteen => campus with { Canteens = AddUnit(campus.Canteens) },
                _ => throw new ArgumentOutOfRangeException(nameof(facility))
            };
            return state with
            {
                Campus = campus,
                Cash = state.Cash - price,
                InvestmentToday = state.InvestmentToday + price,
                Buildings = state.Buildings.Add(new CampusBuilding(facility, tile))
            };
        }

        public static SchoolState ChangeStaff(SchoolState state, Role role, bool hire)
        {
            RequireActive(state);
            Staff staff = state.Staff;
            return state with
            {
                Staff = role switch
                {
                    Role.Teacher => staff with { Teachers = ChangeCount(staff.Teachers, hire) },
                    Role.AcademicDirector => staff with { AcademicDirectors = ChangeCount(staff.AcademicDirectors, hire, 1) },
                    Role.Deputy => staff with { Deputies = ChangeCount(staff.Deputies, hire, 1) },
                    Role.Accountant => staff with { Accountants = ChangeCount(staff.Accountants, hire, 1) },
                    _ => throw new ArgumentOutOfRangeException(nameof(role))
                }
            };
        }

        public static SchoolState SetTuition(SchoolState state, int tuition)
        {
            RequireActive(state);
            if (tuition is < MinimumTuition or > MaximumTuition)
                throw new RuleException(RuleCode.TuitionOutOfRange, $"اختر رسومًا بين {MinimumTuition} و{MaximumTuition}.", $"Choose tuition between {MinimumTuition} and {MaximumTuition}.");
            return state with { Tuition = tuition };
        }

        public static int IncidentCost(Incident incident) => incident == Incident.Absence ? 240 : 180;

        public static SchoolState ResolveIncident(SchoolState state, bool intervene)
        {
            if (state.Outcome != Outcome.InProgress || state.PendingIncident is not { } incident)
                throw new RuleException(RuleCode.NoPendingIncident, "لا يوجد حدث ينتظر قرارًا.", "There is no incident awaiting a decision.");

            int cost = intervene ? IncidentCost(incident) : 0;
            if (state.Cash < cost)
                throw new RuleException(RuleCode.InsufficientCashToIntervene, "الرصيد لا يكفي للتدخل.", "Insufficient cash to intervene.");
            int penalty = state.Staff.Deputies > 0 ? 2 : 5;
            return state with
            {
                PendingIncident = null,
                Cash = state.Cash - cost,
                ResponseCost = cost,
                Reputation = Score(state.Reputation + (intervene ? 2 : -penalty)),
                Staff = state.Staff with { Morale = Score(state.Staff.Morale + (intervene ? 2 : -4)) }
            };
        }

        public static SchoolState AdvanceDay(SchoolState state)
        {
            RequireActive(state);
            Campus campus = state.Campus;
            Staff staff = state.Staff;
            int workload = Math.Max(0, state.Students - staff.Teachers * StudentsPerTeacher);
            int morale = Score(staff.Morale + (workload > 0 ? -3 : 1) + staff.Deputies);
            int quality = Score(58 + Math.Min(campus.Labs, 3) * 5 + Math.Min(campus.Libraries, 3) * 4
                + staff.AcademicDirectors * 8 + (morale - 60) / 3 - workload);
            int reputation = Score(state.Reputation + (quality >= 75 ? 2 : quality >= 60 ? 1 : quality < 45 ? -2 : 0));
            int departures = Math.Min(state.Students, Math.Max(0, (55 - quality) / 10) + Math.Max(0, state.Tuition - 48) / 4);
            int demand = Math.Clamp(2 + (reputation - 50) / 8 + (quality - 60) / 10 - (state.Tuition - 34) / 3, 0, 14);
            int arrivals = Math.Min(campus.Capacity - state.Students + departures, demand);
            int students = state.Students - departures + arrivals;

            int tuition = students * state.Tuition;
            int transport = Math.Min(students, campus.Buses * 16) * 8;
            int canteen = Math.Min(students, campus.Canteens * 40) * 5;
            int payroll = staff.Teachers * Salary(Role.Teacher) + staff.AcademicDirectors * Salary(Role.AcademicDirector)
                + staff.Deputies * Salary(Role.Deputy) + staff.Accountants * Salary(Role.Accountant);
            int operations = 290 + campus.Classrooms * 100 + campus.Labs * 90 + campus.Libraries * 60
                + campus.Buses * 75 + campus.Canteens * 65 + students * 3;
            if (staff.Accountants > 0)
                operations = operations * 85 / 100;

            int cash = state.Cash + tuition + transport + canteen - payroll - operations;
            var report = new DayReport(state.Day, tuition, transport, canteen, payroll, operations,
                state.ResponseCost, state.InvestmentToday, arrivals, departures, cash, quality, reputation);
            var next = state with
            {
                Day = state.Day + 1,
                Cash = cash,
                Students = students,
                Reputation = reputation,
                Quality = quality,
                Staff = staff with { Morale = morale },
                ResponseCost = 0,
                InvestmentToday = 0,
                Reports = state.Reports.Add(report),
                RandomState = NextRandom(state.RandomState)
            };
            if (next.Outcome == Outcome.InProgress && next.RandomState % 100 < 24)
                next = next with { PendingIncident = IncidentFor(next.RandomState) };
            return next;
        }

        // Explicit mapping so reordering the Incident enum cannot change seeded outcomes.
        private static Incident IncidentFor(uint randomState) => (randomState % 2) switch
        {
            0 => Incident.Absence,
            _ => Incident.Complaint
        };

        private static void RequireActive(SchoolState state)
        {
            if (state.Outcome != Outcome.InProgress)
                throw new RuleException(RuleCode.CampaignEnded, "انتهت الحملة.", "The campaign has ended.");
            if (state.PendingIncident.HasValue)
                throw new RuleException(RuleCode.IncidentPending, "اتخذ قرارًا بشأن الحدث أولًا.", "Resolve the incident first.");
        }

        private static int AddUnit(int count, int limit = MaximumUnits)
        {
            if (count >= limit)
                throw new RuleException(RuleCode.LimitReached, "بلغت الحد المتاح.", "You have reached the limit.");
            return count + 1;
        }

        private static int ChangeCount(int count, bool hire, int limit = MaximumUnits)
        {
            if (hire)
                return AddUnit(count, limit);
            if (count == 0)
                throw new RuleException(RuleCode.RoleEmpty, "لا يوجد موظف في هذا الدور.", "No employee holds this role.");
            return count - 1;
        }

        private static int Score(int value) => Math.Clamp(value, 0, 100);

        private static uint NextRandom(uint value)
        {
            value ^= value << 13;
            value ^= value >> 17;
            return value ^ (value << 5);
        }
    }
}
