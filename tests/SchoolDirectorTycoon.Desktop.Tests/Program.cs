using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SchoolDirectorTycoon.Core;
using SchoolDirectorTycoon.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.InitializeComponent();
        var directory = Directory.CreateTempSubdirectory("school-ui-");
        MainWindow? window = null;
        try
        {
            window = CreateWindow(directory);
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(1280, 920));
            content.Arrange(new Rect(0, 0, 1280, 920));
            content.UpdateLayout();

            var selector = (ComboBox)window.FindName("LanguageChoice");
            var dashboard = (TextBlock)window.FindName("Dashboard");
            var next = (Button)window.FindName("NextDay");
            var reports = (ListBox)window.FindName("Reports");
            var guide = (Expander)window.FindName("StarterGuide");
            var tip = (TextBlock)window.FindName("PlanningTip");
            Check(window.FlowDirection == FlowDirection.RightToLeft && dashboard.Text.Contains("اليوم")
                && Equals(next.Content, "إنهاء اليوم") && Equals(guide.Header, "دليل البداية"), "Arabic defaults");
            guide.IsExpanded = true;

            selector.SelectedIndex = 1;
            Check(window.FlowDirection == FlowDirection.LeftToRight && dashboard.Text.Contains("Day 1")
                && Equals(next.Content, "End day") && Equals(guide.Header, "Getting started")
                && tip.Text.Contains("Open seats 8") && guide.IsExpanded, "English labels, guide, tip and direction");
            guide.IsExpanded = false;
            var toolbar = (Panel)window.FindName("FacilityButtons");
            var world = (CampusMap)window.FindName("World");
            double mapScale = Math.Min(world.ActualWidth / 960, world.ActualHeight / 512);
            var center = new Point((world.ActualWidth - 960 * mapScale) / 2 + (446 + (8 - 4) * 34) * mapScale,
                (world.ActualHeight - 512 * mapScale) / 2 + (53 + (8 + 4) * 17) * mapScale);
            var picked = typeof(CampusMap).GetMethod("TileAt", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(world, [center]);
            Check(picked is CampusTile { Column: 8, Row: 4 }, "Projected map coordinates select the correct lot");
            var hint = (TextBlock)window.FindName("MapHint");
            ((Button)toolbar.Children[2]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(hint.Text.Contains("Choose a lot"), "Build tool prompts for a lot");
            var interact = typeof(MainWindow).GetMethod("InteractWithTile", BindingFlags.Instance | BindingFlags.NonPublic)!;
            interact.Invoke(window, [new CampusTile(4, CampusLayout.MainPathRow)]);
            Check(hint.Text.Contains("main path"), "Cannot build on walkway");
            var libraryTile = new CampusTile(8, 4);
            interact.Invoke(window, [libraryTile]);
            Check(hint.Text.Contains("Construction complete"), "Map construction commits the selected site");
            if (args.Length == 1)
                Capture(content, args[0]);

            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(dashboard.Text.Contains("Day 2") && File.Exists(Path.Combine(directory.FullName, "auto.json")),
                "Day advance and autosave");
            Check(new SaveStore(directory.FullName).Read(SaveSlot.Auto).Buildings.Contains(
                new CampusBuilding(Facility.Library, libraryTile)), "Chosen map site persisted");
            Check(reports.Items.Count == 1 && reports.Items[0].ToString()!.Contains("Tuition"), "English report");

            selector.SelectedIndex = 0;
            Check(window.FlowDirection == FlowDirection.RightToLeft && dashboard.Text.Contains("2/30")
                && reports.Items[0].ToString()!.Contains("الرسوم"), "Arabic report and preserved campaign");
            selector.SelectedIndex = 1;
            window.Close();
            window = CreateWindow(directory);
            Check(window.FlowDirection == FlowDirection.LeftToRight, "Preferred language restored on restart");
            window.Close();
            File.WriteAllText(Path.Combine(directory.FullName, "language.txt"), "unknown");
            window = CreateWindow(directory);
            Check(window.FlowDirection == FlowDirection.RightToLeft, "Invalid language falls back to Arabic");
            Console.WriteLine("PASS WPF campus construction, bilingual interface, persistent language, reports, and autosave");
        }
        finally
        {
            window?.Close();
            foreach (var file in directory.GetFiles())
                file.Delete();
            directory.Delete();
        }
    }

    private static void Capture(FrameworkElement content, string path)
    {
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1280, 920, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static MainWindow CreateWindow(DirectoryInfo directory) => (MainWindow)Activator.CreateInstance(
        typeof(MainWindow), BindingFlags.Instance | BindingFlags.NonPublic, null, [directory.FullName], null)!;

    private static void Check(bool result, string scenario)
    {
        if (!result)
            throw new InvalidOperationException($"Desktop check failed: {scenario}");
    }
}
