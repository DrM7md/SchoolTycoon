using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading;

namespace SchoolDirectorTycoon.Core
{

    public enum SaveSlot { Manual, Auto }

    /// <summary>Language-neutral reason a save file was rejected. Unity localizes by this code.</summary>
    public enum SaveErrorCode { FileTooLarge, UnsupportedVersion, InvalidState, InvalidLayout, InvalidReports }

    /// <summary>
    /// Builds and inspects save-validation errors. InvalidDataException is sealed, so instead of a subclass the
    /// error stays a plain InvalidDataException (existing handlers keep working) with the Arabic Message, and the
    /// language-neutral code and English text ride in Exception.Data, mirroring RuleException's Code/EnglishMessage.
    /// </summary>
    public static class SaveErrors
    {
        private const string CodeKey = "SchoolDirectorTycoon.SaveErrorCode";
        private const string EnglishKey = "SchoolDirectorTycoon.EnglishMessage";

        public static InvalidDataException Create(SaveErrorCode code, string message, string englishMessage)
        {
            var exception = new InvalidDataException(message);
            exception.Data[CodeKey] = code;
            exception.Data[EnglishKey] = englishMessage;
            return exception;
        }

        /// <summary>The code of a save-validation error, or null for any other exception.</summary>
        public static SaveErrorCode? GetSaveErrorCode(this Exception exception) =>
            exception.Data[CodeKey] is SaveErrorCode code ? code : (SaveErrorCode?)null;

        /// <summary>The English text of a save-validation error, or null for any other exception.</summary>
        public static string? GetEnglishMessage(this Exception exception) => exception.Data[EnglishKey] as string;
    }

    public sealed class SaveStore
    {
        private readonly string directory;

        public SaveStore(string directory) => this.directory = directory;

        private const int CurrentVersion = 2;
        private const int MaximumSaveBytes = 256 * 1024;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            IgnoreReadOnlyProperties = true,
            RespectRequiredConstructorParameters = true
        };
        private sealed record SaveFile(int Version, SchoolState State);

        private const int SwapAttempts = 3;
        private const int SwapRetryDelayMilliseconds = 50;

        public void Write(SaveSlot slot, SchoolState state)
        {
            Validate(state);
            Directory.CreateDirectory(directory);
            string path = PathFor(slot);
            string temporary = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                {
                    JsonSerializer.Serialize(stream, new SaveFile(CurrentVersion, state), JsonOptions);
                    stream.Flush(flushToDisk: true);
                }
                for (int attempt = 1; ; attempt++)
                {
                    try
                    {
                        Swap(temporary, path);
                        return;
                    }
                    // Sharing violations from antivirus or sync clients (OneDrive) are usually transient.
                    catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < SwapAttempts)
                    {
                        Thread.Sleep(SwapRetryDelayMilliseconds * attempt);
                    }
                }
            }
            finally
            {
                File.Delete(temporary);
            }
        }

        // File.Move(..., overwrite) is unavailable on netstandard2.1; Replace keeps the swap atomic and keeps
        // the previous save as <slot>.json.bak. No File.Exists pre-check: a missing target is detected by the
        // exception, and a target that appears between the two calls makes Move fail and the caller retry.
        private static void Swap(string temporary, string path)
        {
            try
            {
                File.Replace(temporary, path, BackupPathFor(path));
            }
            catch (FileNotFoundException) when (File.Exists(temporary))
            {
                File.Move(temporary, path);
            }
        }

        /// <summary>
        /// Reads a slot. If the main file is missing or fails validation, the <c>.bak</c> copy from the previous
        /// write is read and validated instead; if that also fails, the main file's error is rethrown.
        /// </summary>
        public SchoolState Read(SaveSlot slot)
        {
            string path = PathFor(slot);
            try
            {
                return ReadFile(path);
            }
            catch (Exception primary) when (primary is FileNotFoundException || primary is DirectoryNotFoundException
                || primary is InvalidDataException || primary is JsonException)
            {
                try
                {
                    return ReadFile(BackupPathFor(path));
                }
                catch (Exception backup) when (backup is IOException || backup is InvalidDataException || backup is JsonException)
                {
                    ExceptionDispatchInfo.Capture(primary).Throw();
                    throw;
                }
            }
        }

        private static string BackupPathFor(string path) => path + ".bak";

        private static SchoolState ReadFile(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (stream.Length > MaximumSaveBytes)
                throw SaveErrors.Create(SaveErrorCode.FileTooLarge, "حجم ملف الحفظ يتجاوز الحد المسموح.", "The save file exceeds the size limit.");
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(nameof(SaveFile.Version), out var version)
                || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int value)
                || value is not (1 or CurrentVersion))
                throw SaveErrors.Create(SaveErrorCode.UnsupportedVersion, "إصدار الحفظ غير مدعوم.", "This save version is not supported.");
            var state = document.RootElement.Deserialize<SaveFile>(JsonOptions)?.State;
            if (value == 1)
            {
                Validate(state, requireLayout: false);
                state = state! with { Buildings = CampusLayout.FromCounts(state.Campus) };
            }
            Validate(state);
            return state!;
        }

        private string PathFor(SaveSlot slot) => Path.Combine(directory, slot switch
        {
            SaveSlot.Manual => "manual.json",
            SaveSlot.Auto => "auto.json",
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        });

        private static void Validate(SchoolState? state, bool requireLayout = true)
        {
            if (state is null || state.Campus is not { } campus || state.Staff is not { } staff
                || state.Day is < 1 or > Simulation.CampaignDays + 1
                || state.Reports.IsDefault || state.Reports.Length != state.Day - 1
                || campus.Classrooms < 2 || !Units(campus.Classrooms) || !Units(campus.Labs)
                || !Units(campus.Libraries) || !Units(campus.Buses) || !Units(campus.Canteens)
                || !Units(staff.Teachers) || staff.AcademicDirectors is < 0 or > 1
                || staff.Deputies is < 0 or > 1 || staff.Accountants is < 0 or > 1
                || !Score(staff.Morale) || !Score(state.Quality) || !Score(state.Reputation)
                || state.Students < 0 || state.Students > campus.Capacity
                || state.Tuition is < Simulation.MinimumTuition or > Simulation.MaximumTuition
                || state.Cash is < -100_000 or > 2_000_000 || state.RandomState == 0
                || state.ResponseCost is not (0 or 180 or 240)
                || state.InvestmentToday is < 0 or > 500_000
                || (state.PendingIncident is { } incident
                    && (!Enum.IsDefined(typeof(Incident), incident) || state.Outcome != Outcome.InProgress || state.ResponseCost != 0)))
                throw SaveErrors.Create(SaveErrorCode.InvalidState, "بيانات الحفظ غير صالحة.", "The save data is invalid.");

            if (requireLayout && (state.Buildings.IsDefault
                || state.Buildings.Length > CampusLayout.Columns * CampusLayout.Rows
                || state.Buildings.Any(building => !Facilities.IsDefined(building.Facility) || !CampusLayout.IsBuildable(building.Tile))
                || state.Buildings.Select(building => building.Tile).Distinct().Count() != state.Buildings.Length
                || Facilities.All.Any(facility =>
                    state.Buildings.Count(building => building.Facility == facility) != CampusLayout.Count(campus, facility))))
                throw SaveErrors.Create(SaveErrorCode.InvalidLayout, "خريطة الحرم غير صالحة.", "The campus layout is invalid.");

            for (int i = 0; i < state.Reports.Length; i++)
            {
                var report = state.Reports[i];
                if (report is null || report.Day != i + 1 || !Score(report.Quality) || !Score(report.Reputation)
                    || report.ClosingCash is < -100_000 or > 2_000_000
                    || report.Tuition is < 0 or > 50_000 || report.Transport is < 0 or > 5_000
                    || report.Canteen is < 0 or > 5_000 || report.Payroll is < 0 or > 10_000
                    || report.Operations is < 0 or > 30_000 || report.ResponseCost is not (0 or 180 or 240)
                    || report.Investment is < 0 or > 500_000
                    || report.Arrivals is < 0 or > 14 || report.Departures is < 0 or > 720)
                    throw SaveErrors.Create(SaveErrorCode.InvalidReports, "تقارير الحفظ غير صالحة.", "The save reports are invalid.");
            }
        }

        private static bool Units(int value) => value is >= 0 and <= Simulation.MaximumUnits;
        private static bool Score(int value) => value is >= 0 and <= 100;
    }
}
