using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SchoolDirectorTycoon.Core;

namespace SchoolDirectorTycoon.Desktop;

public partial class MainWindow : Window
{
    private static readonly int[] Speeds = [0, 1, 2, 4, 8, 16];
    private readonly DispatcherTimer timer = new();
    private readonly SaveStore saves;
    private readonly LanguagePreference languagePreference;
    private SchoolState school = CreateSchool();
    private bool english;
    private bool initialized;
    private Facility? blueprint;

    public MainWindow() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SchoolDirectorTycoon")) { }

    internal MainWindow(string saveDirectory)
    {
        saves = new SaveStore(saveDirectory);
        languagePreference = new LanguagePreference(saveDirectory);
        InitializeComponent();
        timer.Tick += Advance;
        World.TileActivated += InteractWithTile;
        Closed += (_, _) => timer.Stop();
        LanguageChoice.SelectedIndex = languagePreference.ReadEnglish() ? 1 : 0;
        Pause();
        TuitionInput.Text = school.Tuition.ToString(CultureInfo.InvariantCulture);
        initialized = true;
    }

    private static SchoolState CreateSchool() => Simulation.NewSchool((uint)RandomNumberGenerator.GetInt32(1, int.MaxValue));
    private string T(string ar, string en) => english ? en : ar;

    private void ChangeLanguage(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageChoice.SelectedIndex < 0)
            return;
        english = LanguageChoice.SelectedIndex == 1;
        FlowDirection = english ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(english ? "en" : "ar");
        foreach (var (key, translations) in InterfaceText.All)
            Resources[key] = T(translations.Ar, translations.En);
        FacilityButtons.Children.Clear();
        StaffButtons.Children.Clear();
        foreach (Facility facility in Enum.GetValues<Facility>())
            AddButton(FacilityButtons, $"{FacilityName(facility)} · {Simulation.Price(facility):N0}",
                () => SelectBlueprint(facility));
        foreach (Role role in Enum.GetValues<Role>())
        {
            AddButton(StaffButtons, T($"تعيين {RoleName(role)} · {Simulation.Salary(role)}/يوم",
                $"Hire {RoleName(role)} · {Simulation.Salary(role)}/day"),
                () => Change(() => Simulation.ChangeStaff(school, role, true)));
            AddButton(StaffButtons, T($"إنهاء عقد {RoleName(role)}", $"Dismiss {RoleName(role)}"),
                () => Change(() => Simulation.ChangeStaff(school, role, false)));
        }
        Status.Text = "";
        Render();
        if (initialized && !languagePreference.TryWrite(english))
            Status.Text = T("تغيّرت اللغة لهذه الجلسة. تعذّر حفظ التفضيل.", "Language changed for this session. The preference could not be saved.");
    }

    private void SelectBlueprint(Facility facility)
    {
        Pause();
        blueprint = facility;
        RenderMap();
        World.Focus();
    }

    private void CancelBuild(object sender, RoutedEventArgs e)
    {
        blueprint = null;
        RenderMap();
    }

    private void ZoomIn(object sender, RoutedEventArgs e) => World.ChangeZoom(1.25);
    private void ZoomOut(object sender, RoutedEventArgs e) => World.ChangeZoom(0.8);
    private void ResetCamera(object sender, RoutedEventArgs e) => World.ResetCamera();

    private void MapKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !blueprint.HasValue)
            return;
        CancelBuild(sender, e);
        e.Handled = true;
    }

    private void InteractWithTile(CampusTile tile)
    {
        if (blueprint is { } facility)
        {
            if (Change(() => Simulation.Build(school, facility, tile)))
            {
                blueprint = null;
                MapHint.Text = T($"تم البناء في القطعة {tile.Column + 1}، {tile.Row + 1}.",
                    $"Construction complete at lot {tile.Column + 1}, {tile.Row + 1}.");
                CancelBuildButton.Visibility = Visibility.Collapsed;
                World.Update(school, null);
            }
            else
            {
                MapHint.Text = Status.Text;
            }
            return;
        }

        foreach (CampusBuilding building in school.Buildings)
        {
            if (building.Tile == tile)
            {
                MapHint.Text = FacilityLabel(building.Facility);
                return;
            }
        }
        MapHint.Text = CampusLayout.IsBuildable(tile)
            ? T("أرض متاحة. اختر مرفقًا من الشريط للبناء هنا.", "Vacant lot. Choose a facility from the toolbar to build here.")
            : T("الممر الرئيسي للطلاب.", "Main student walkway.");
    }

    private void RenderMap()
    {
        CancelBuildButton.Visibility = blueprint.HasValue ? Visibility.Visible : Visibility.Collapsed;
        MapHint.Text = blueprint is { } facility
            ? T($"اختر موقع {FacilityLabel(facility)} على الخريطة. يمكنك استخدام الأسهم ثم Enter.",
                $"Choose a lot for the {FacilityLabel(facility)}. You can also use the arrow keys and Enter.")
            : T("اختر مرفقًا ثم ضعْه على الخريطة، أو اختر مبنى للتعرّف عليه.",
                "Choose a facility and place it on the map, or select a building to inspect it.");
        World.Update(school, blueprint);
    }

    private void TuitionKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        ApplyTuition(sender, e);
    }

    private void Advance(object? sender, EventArgs e)
    {
        if (!Change(() => Simulation.AdvanceDay(school)))
        {
            Pause();
            return;
        }
        Save(SaveSlot.Auto, announce: false);
    }

    private bool Change(Func<SchoolState> command)
    {
        try
        {
            school = command();
            Status.Text = "";
            Render();
            return true;
        }
        catch (RuleException ex)
        {
            Status.Text = T(ex.Message, ex.EnglishMessage);
            return false;
        }
    }

    private void ApplyTuition(object sender, RoutedEventArgs e)
    {
        string digits = string.Concat(TuitionInput.Text.Select(c =>
        {
            int value = CharUnicodeInfo.GetDecimalDigitValue(c);
            return value < 0 ? c : (char)('0' + value);
        }));
        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int tuition))
        {
            Status.Text = T("أدخل قيمة رقمية للرسوم.", "Enter a numeric tuition amount.");
            return;
        }
        Change(() => Simulation.SetTuition(school, tuition));
    }

    private void Intervene(object sender, RoutedEventArgs e) => Change(() => Simulation.ResolveIncident(school, true));
    private void IgnoreIncident(object sender, RoutedEventArgs e) => Change(() => Simulation.ResolveIncident(school, false));

    private void NewGame(object sender, RoutedEventArgs e)
    {
        Pause();
        if (MessageBox.Show(this,
            T("هل تبدأ حملة جديدة؟ سيُستبدل الحفظ التلقائي.", "Start a new campaign? This replaces the autosave."),
            T("حملة جديدة", "New campaign"), MessageBoxButton.YesNo, MessageBoxImage.Question,
            MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        ReplaceSchool(CreateSchool());
        Save(SaveSlot.Auto, announce: false);
    }

    private void SaveManual(object sender, RoutedEventArgs e) => Save(SaveSlot.Manual);
    private void LoadManual(object sender, RoutedEventArgs e) => Load(SaveSlot.Manual);
    private void LoadAuto(object sender, RoutedEventArgs e) => Load(SaveSlot.Auto);

    private void Save(SaveSlot slot, bool announce = true)
    {
        try
        {
            saves.Write(slot, school);
            if (announce)
                Status.Text = T("تم حفظ الحملة.", "Campaign saved.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status.Text = T("تعذّر كتابة ملف الحفظ.", "Could not write the save file.");
        }
    }

    private void Load(SaveSlot slot)
    {
        try
        {
            ReplaceSchool(saves.Read(slot));
            Status.Text = T("تم تحميل الحملة.", "Campaign loaded.");
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            Status.Text = T("خانة الحفظ فارغة.", "This save slot is empty.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            Status.Text = T("تعذّر تحميل ملف الحفظ. تحقق من صلاحيته وإصداره.",
                "Could not load the save file. Check its format and version.");
        }
    }

    private void ReplaceSchool(SchoolState replacement)
    {
        Pause();
        blueprint = null;
        World.ResetCamera();
        school = replacement;
        TuitionInput.Text = school.Tuition.ToString(CultureInfo.InvariantCulture);
        Status.Text = "";
        Render();
    }

    private void SpeedChanged(object sender, SelectionChangedEventArgs e)
    {
        timer.Stop();
        if (Speed.SelectedIndex <= 0 || school.Outcome != Outcome.InProgress || school.PendingIncident.HasValue)
            return;
        timer.Interval = TimeSpan.FromSeconds(8.0 / Speeds[Speed.SelectedIndex]);
        timer.Start();
    }

    private void Pause() => Speed.SelectedIndex = 0;

    private void Render()
    {
        int day = Math.Min(school.Day, Simulation.CampaignDays);
        Dashboard.Text = T(
            $"اليوم {day}/30   •   الرصيد {school.Cash:N0}   •   الطلاب {school.Students}/{school.Campus.Capacity}   •   السمعة {school.Reputation}/100",
            $"Day {day}/30   •   Cash {school.Cash:N0}   •   Students {school.Students}/{school.Campus.Capacity}   •   Reputation {school.Reputation}/100");
        OutcomeText.Text = school.Outcome switch
        {
            Outcome.Won => T("نجحت الحملة. حققت جميع الأهداف.", "Campaign won. You met every objective."),
            Outcome.Lost when school.Cash < 0 => T("انتهت الحملة بسبب العجز المالي.", "Campaign lost. The school ran out of cash."),
            Outcome.Lost => T("انتهت الحملة قبل تحقيق أهداف التسجيل والسمعة.", "Campaign lost. Enrollment or reputation fell short."),
            _ => T($"الجودة {school.Quality}/100   •   رضا الطاقم {school.Staff.Morale}/100   •   الرسوم {school.Tuition} لكل طالب يوميًا",
                $"Quality {school.Quality}/100   •   Staff morale {school.Staff.Morale}/100   •   Tuition {school.Tuition} per student per day")
        };

        int seats = school.Campus.Capacity - school.Students;
        int coverage = school.Staff.Teachers * Simulation.StudentsPerTeacher;
        PlanningTip.Visibility = school.Outcome == Outcome.InProgress ? Visibility.Visible : Visibility.Collapsed;
        PlanningTip.Text = T(
            $"المقاعد الشاغرة {seats} · تغطية المعلمين {coverage} طالبًا",
            $"Open seats {seats} · Teacher coverage {coverage} students");
        if (school.Students > coverage)
            PlanningTip.Text += T(" · الطاقم فوق طاقته وقد تتراجع الجودة.", " · Staff are over capacity and quality may fall.");
        else if (seats == 0)
            PlanningTip.Text += T(" · المدرسة ممتلئة. يتيح فصل إضافي مقاعد جديدة.", " · The school is full. Another classroom adds seats.");

        Incident? incident = school.PendingIncident;
        bool canManage = school.Outcome == Outcome.InProgress && !incident.HasValue;
        if (!canManage)
        {
            Pause();
            blueprint = null;
        }
        FacilityButtons.IsEnabled = canManage;
        RenderMap();
        NextDay.IsEnabled = Speed.IsEnabled = Management.IsEnabled = canManage;
        IncidentCard.Visibility = incident.HasValue ? Visibility.Visible : Visibility.Collapsed;
        if (incident is { } pending)
        {
            IncidentText.Text = pending == Incident.Absence
                ? T("تغيّب معلم. يمكنك تمويل معلم بديل أو قبول أثر الغياب.", "A teacher is absent. Pay for cover or absorb the impact.")
                : T("وصلت شكوى من ولي أمر. يمكنك تمويل معالجة المشكلة أو قبول أثرها.", "A parent filed a complaint. Pay to address it or absorb the impact.");
            InterveneButton.Content = T($"تدخّل مقابل {Simulation.IncidentCost(pending)}", $"Intervene for {Simulation.IncidentCost(pending)}");
        }

        Campus campus = school.Campus;
        CampusSummary.Text = T(
            $"الفصول {campus.Classrooms} · المختبرات {campus.Labs} · المكتبات {campus.Libraries} · الحافلات {campus.Buses} · المقاصف {campus.Canteens}",
            $"Classrooms {campus.Classrooms} · Labs {campus.Labs} · Libraries {campus.Libraries} · Buses {campus.Buses} · Canteens {campus.Canteens}");
        Staff staff = school.Staff;
        StaffSummary.Text = T(
            $"المعلمون {staff.Teachers} · المديرون الأكاديميون {staff.AcademicDirectors} · الوكلاء {staff.Deputies} · المحاسبون {staff.Accountants}",
            $"Teachers {staff.Teachers} · Academic directors {staff.AcademicDirectors} · Deputies {staff.Deputies} · Accountants {staff.Accountants}");
        FinanceSummary.Text = T(
            $"صافي آخر 7 أيام {Net(7):N0}   •   صافي الحملة {Net(30):N0}   •   استثمار اليوم {school.InvestmentToday:N0}",
            $"Last 7 days net {Net(7):N0}   •   Campaign net {Net(30):N0}   •   Today's investment {school.InvestmentToday:N0}");
        Reports.ItemsSource = school.Reports.Reverse().Select(FormatReport).ToArray();
    }

    private string FormatReport(DayReport report) => T(
        $"يوم {report.Day} · الرسوم {report.Tuition:N0} · النقل {report.Transport:N0} · المقصف {report.Canteen:N0} · الأجور {report.Payroll:N0} · التشغيل {report.Operations:N0} · الحدث {report.ResponseCost:N0} · الاستثمار {report.Investment:N0} · الصافي {report.Net:N0} · الرصيد {report.ClosingCash:N0} · التسجيل +{report.Arrivals} / -{report.Departures}",
        $"Day {report.Day} · Tuition {report.Tuition:N0} · Transport {report.Transport:N0} · Canteen {report.Canteen:N0} · Payroll {report.Payroll:N0} · Operations {report.Operations:N0} · Incident {report.ResponseCost:N0} · Investment {report.Investment:N0} · Net {report.Net:N0} · Cash {report.ClosingCash:N0} · Enrollment +{report.Arrivals} / -{report.Departures}");

    private int Net(int days) => school.Reports.TakeLast(days).Sum(report => report.Net);

    private string FacilityName(Facility facility) => facility switch
    {
        Facility.Classroom => T("بناء فصل", "Build classroom"),
        Facility.Lab => T("بناء مختبر", "Build lab"),
        Facility.Library => T("بناء مكتبة", "Build library"),
        Facility.Bus => T("شراء حافلة", "Buy bus"),
        Facility.Canteen => T("بناء مقصف", "Build canteen"),
        _ => throw new ArgumentOutOfRangeException(nameof(facility))
    };

    private string FacilityLabel(Facility facility) => facility switch
    {
        Facility.Classroom => T("فصل دراسي", "classroom"),
        Facility.Lab => T("مختبر", "lab"),
        Facility.Library => T("مكتبة", "library"),
        Facility.Bus => T("حافلة", "bus"),
        Facility.Canteen => T("مقصف", "canteen"),
        _ => throw new ArgumentOutOfRangeException(nameof(facility))
    };

    private string RoleName(Role role) => role switch
    {
        Role.Teacher => T("معلم", "teacher"),
        Role.AcademicDirector => T("مدير أكاديمي", "academic director"),
        Role.Deputy => T("وكيل", "deputy"),
        Role.Accountant => T("محاسب", "accountant"),
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static void AddButton(Panel panel, string label, Action command)
    {
        var button = new Button { Content = label };
        button.Click += (_, _) => command();
        panel.Children.Add(button);
    }
}
