using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.Immutable;

namespace SchoolDirectorTycoon.Core
{

    // init (not get-only) so SaveStore serializes these with IgnoreReadOnlyProperties, matching save schema 2.
    public readonly struct CampusTile : IEquatable<CampusTile>
    {
        public CampusTile(int column, int row) { Column = column; Row = row; }
        public int Column { get; init; }
        public int Row { get; init; }
        public bool Equals(CampusTile other) => Column == other.Column && Row == other.Row;
        public override bool Equals(object? obj) => obj is CampusTile other && Equals(other);
        public override int GetHashCode() => unchecked(Column * 397 ^ Row);
        public override string ToString() => $"CampusTile {{ Column = {Column}, Row = {Row} }}";
        public static bool operator ==(CampusTile left, CampusTile right) => left.Equals(right);
        public static bool operator !=(CampusTile left, CampusTile right) => !left.Equals(right);
    }

    public readonly struct CampusBuilding : IEquatable<CampusBuilding>
    {
        public CampusBuilding(Facility facility, CampusTile tile) { Facility = facility; Tile = tile; }
        public Facility Facility { get; init; }
        public CampusTile Tile { get; init; }
        public bool Equals(CampusBuilding other) => Facility == other.Facility && Tile == other.Tile;
        public override bool Equals(object? obj) => obj is CampusBuilding other && Equals(other);
        public override int GetHashCode() => unchecked((int)Facility * 397 ^ Tile.GetHashCode());
        public override string ToString() => $"CampusBuilding {{ Facility = {Facility}, Tile = {Tile} }}";
        public static bool operator ==(CampusBuilding left, CampusBuilding right) => left.Equals(right);
        public static bool operator !=(CampusBuilding left, CampusBuilding right) => !left.Equals(right);
    }

    public static class Facilities
    {
        public static readonly IReadOnlyList<Facility> All = (Facility[])Enum.GetValues(typeof(Facility));
        public static bool IsDefined(Facility facility) => Enum.IsDefined(typeof(Facility), facility);
    }

    public static class CampusLayout
    {
        public const int Columns = 14;
        public const int Rows = 12;
        public const int MainPathRow = 5;

        public static bool IsBuildable(CampusTile tile) =>
            tile.Column is >= 0 and < Columns && tile.Row is >= 0 and < Rows && tile.Row != MainPathRow;

        public static IEnumerable<CampusTile> Lots()
        {
            for (int distance = 1; distance < Rows; distance++)
            {
                foreach (int row in new[] { MainPathRow - distance, MainPathRow + distance })
                {
                    for (int offset = 0; offset < Columns; offset++)
                    {
                        int column = Columns / 2 + (offset % 2 == 0 ? offset / 2 : -(offset + 1) / 2);
                        var tile = new CampusTile(column, row);
                        if (IsBuildable(tile))
                            yield return tile;
                    }
                }
            }
        }

        public static ImmutableArray<CampusBuilding> FromCounts(Campus campus)
        {
            var facilities = Facilities.All.SelectMany(facility => Enumerable.Repeat(facility, Count(campus, facility)));
            return facilities.Zip(Lots(), (facility, tile) => new CampusBuilding(facility, tile)).ToImmutableArray();
        }

        public static int Count(Campus campus, Facility facility) => facility switch
        {
            Facility.Classroom => campus.Classrooms,
            Facility.Lab => campus.Labs,
            Facility.Library => campus.Libraries,
            Facility.Bus => campus.Buses,
            Facility.Canteen => campus.Canteens,
            _ => throw new ArgumentOutOfRangeException(nameof(facility))
        };
    }
}
