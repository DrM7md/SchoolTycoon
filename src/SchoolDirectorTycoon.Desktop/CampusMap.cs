using System.Collections.Immutable;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SchoolDirectorTycoon.Core;

namespace SchoolDirectorTycoon.Desktop;

public sealed class CampusMap : FrameworkElement
{
    private const double HalfWidth = 34;
    private const double HalfHeight = 17;
    private const double MapWidth = 960;
    private const double MapHeight = 512;
    private static readonly Brush[] Grass = [BrushFrom("#4E7659"), BrushFrom("#557D5E"), BrushFrom("#51795B"), BrushFrom("#597F60")];
    private static readonly Brush[] Roofs = [BrushFrom("#5B9FB2"), BrushFrom("#8DA8C0"), BrushFrom("#B87963"), BrushFrom("#E9B964"), BrushFrom("#80A995")];
    private static readonly Brush[] Jackets = [BrushFrom("#FFD179"), BrushFrom("#72CAE1"), BrushFrom("#EF947B"), BrushFrom("#F0E6D1")];
    private static readonly Brush Background = CreateBackdrop();
    private static readonly Pen ContourLine = Line("#346052", 1);
    private static readonly Pen MapFrame = Line("#527568", 1);
    private static readonly Brush Haze = BrushFrom("#106DBF9E");
    private static readonly Brush WarmHighlight = BrushFrom("#59F5DBA4");
    private static readonly Brush Concrete = BrushFrom("#B0BAA1");
    private static readonly Brush Earth = BrushFrom("#263D33");
    private static readonly Brush PathSurface = BrushFrom("#ADB29A");
    private static readonly Brush WallLight = BrushFrom("#E1D6BC");
    private static readonly Brush WallDark = BrushFrom("#85958B");
    private static readonly Brush WindowGlass = BrushFrom("#345B66");
    private static readonly Brush Door = BrushFrom("#28514F");
    private static readonly Brush Shadow = BrushFrom("#4B0A2220");
    private static readonly Brush Skin = BrushFrom("#E7B995");
    private static readonly Brush TreeDark = BrushFrom("#244E3D");
    private static readonly Brush TreeLight = BrushFrom("#65936E");
    private static readonly Brush Bark = BrushFrom("#5A4835");
    private static readonly Brush HoverGood = BrushFrom("#92F0C3");
    private static readonly Brush HoverBad = BrushFrom("#E89078");
    private static readonly Pen GridLine = Line("#3B6651", 0.75);
    private static readonly Pen RoadMark = Line("#5D6E61", 1);
    private static readonly Pen RoofEdge = Line("#D7E0CC", 1);
    private static readonly Pen HoverOutline = Line("#F1D58F", 2);
    private readonly DispatcherTimer animationTimer = new() { Interval = TimeSpan.FromMilliseconds(65) };
    private readonly Stopwatch animationClock = Stopwatch.StartNew();
    private ImmutableArray<CampusBuilding> buildings = [];
    private readonly DrawingGroup terrain;
    private DrawingGroup structures = new();
    private CampusTile? hovered;
    private CampusTile? selected;
    private int studentCount;
    private Facility? blueprint;
    private double zoom = 1;
    private Vector pan;
    private Point? dragStart;

    public CampusMap()
    {
        Focusable = true;
        terrain = DrawTerrain();
        animationTimer.Tick += (_, _) => InvalidateVisual();
        IsVisibleChanged += (_, _) => UpdateAnimation();
        Loaded += (_, _) => UpdateAnimation();
        Unloaded += (_, _) => animationTimer.Stop();
    }

    internal event Action<CampusTile>? TileActivated;

    internal void Update(SchoolState school, Facility? facility)
    {
        if (buildings != school.Buildings)
        {
            buildings = school.Buildings;
            structures = DrawStructures(buildings);
        }
        studentCount = school.Students;
        blueprint = facility;
        InvalidateVisual();
    }

    private void UpdateAnimation()
    {
        if (IsLoaded && IsVisible && SystemParameters.ClientAreaAnimation)
            animationTimer.Start();
        else
            animationTimer.Stop();
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Background, null, new Rect(RenderSize));
        var (scale, offset) = Viewport();
        if (scale <= 0)
            return;
        dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        dc.PushTransform(new TranslateTransform(offset.X, offset.Y));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawDrawing(terrain);
        var cursor = hovered ?? (IsKeyboardFocused ? selected : null);
        if (cursor is { } tile)
        {
            bool vacant = CampusLayout.IsBuildable(tile) && !buildings.Any(b => b.Tile == tile);
            DrawTile(dc, tile, blueprint.HasValue ? vacant ? HoverGood : HoverBad : null, HoverOutline, 0.96);
        }
        DrawPeople(dc);
        dc.DrawDrawing(structures);
        if (cursor is { } lot && blueprint is { } facility && CampusLayout.IsBuildable(lot)
            && !buildings.Any(building => building.Tile == lot))
        {
            dc.PushOpacity(0.65);
            DrawBuilding(dc, Project(lot.Column, lot.Row), facility);
            dc.Pop();
        }
        dc.Pop();
        dc.Pop();
        dc.Pop();
        if (ActualWidth > 2 && ActualHeight > 2)
            dc.DrawRoundedRectangle(null, MapFrame, new Rect(1, 1, ActualWidth - 2, ActualHeight - 2), 8, 8);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Point position = e.GetPosition(this);
        if (dragStart is { } previous)
        {
            pan += position - previous;
            ClampPan();
            dragStart = position;
            InvalidateVisual();
            return;
        }
        CampusTile? next = TileAt(position);
        if (hovered != next)
        {
            hovered = next;
            InvalidateVisual();
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            return;
        ChangeZoom(e.Delta > 0 ? 1.2 : 1 / 1.2);
        e.Handled = true;
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        if (CaptureMouse())
        {
            dragStart = e.GetPosition(this);
            hovered = null;
            e.Handled = true;
        }
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        if (dragStart.HasValue)
        {
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        dragStart = null;
    }

    internal void ChangeZoom(double multiplier)
    {
        zoom = Math.Clamp(zoom * multiplier, 1, 2.4);
        ClampPan();
        hovered = null;
        InvalidateVisual();
    }

    internal void ResetCamera()
    {
        zoom = 1;
        pan = default;
        hovered = null;
        InvalidateVisual();
    }

    private void ClampPan()
    {
        double maxX = ActualWidth * (zoom - 1) / 2;
        double maxY = ActualHeight * (zoom - 1) / 2;
        pan = new Vector(Math.Clamp(pan.X, -maxX, maxX), Math.Clamp(pan.Y, -maxY, maxY));
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        ClampPan();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = null;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        if (TileAt(e.GetPosition(this)) is { } tile)
        {
            selected = tile;
            TileActivated?.Invoke(tile);
            e.Handled = true;
        }
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        selected ??= new CampusTile(CampusLayout.Columns / 2, CampusLayout.MainPathRow);
        InvalidateVisual();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (selected is not { } tile)
            return;

        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
                TileActivated?.Invoke(tile);
                break;
            case Key.Right: selected = new CampusTile(Math.Min(CampusLayout.Columns - 1, tile.Column + 1), tile.Row); break;
            case Key.Left: selected = new CampusTile(Math.Max(0, tile.Column - 1), tile.Row); break;
            case Key.Up: selected = new CampusTile(tile.Column, Math.Max(0, tile.Row - 1)); break;
            case Key.Down: selected = new CampusTile(tile.Column, Math.Min(CampusLayout.Rows - 1, tile.Row + 1)); break;
            default: return;
        }
        hovered = null;
        e.Handled = true;
        InvalidateVisual();
    }

    internal CampusTile? TileAt(Point screenPoint)
    {
        var (scale, offset) = Viewport();
        if (scale <= 0)
            return null;
        Point origin = Project(0, 0);
        double x = ((screenPoint.X - offset.X) / scale - origin.X) / HalfWidth;
        double y = ((screenPoint.Y - offset.Y) / scale - origin.Y) / HalfHeight;
        int column = (int)Math.Floor((y + x) / 2 + 0.5);
        int row = (int)Math.Floor((y - x) / 2 + 0.5);
        return column >= 0 && column < CampusLayout.Columns && row >= 0 && row < CampusLayout.Rows
            ? new CampusTile(column, row) : null;
    }

    private (double Scale, Point Offset) Viewport()
    {
        double scale = Math.Min(ActualWidth / MapWidth, ActualHeight / MapHeight) * zoom;
        return (scale, new Point((ActualWidth - MapWidth * scale) / 2 + pan.X, (ActualHeight - MapHeight * scale) / 2 + pan.Y));
    }

    private static Point Project(double column, double row) =>
        new(446 + (column - row) * HalfWidth, 53 + (column + row) * HalfHeight);

    private static DrawingGroup DrawTerrain()
    {
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            dc.DrawEllipse(Haze, null, new Point(485, 252), 445, 235);
            for (int ring = 0; ring < 4; ring++)
                dc.DrawEllipse(null, ContourLine, new Point(479, 266), 457 + ring * 15, 209 + ring * 19);
            dc.DrawEllipse(Shadow, null, new Point(480, 306), 449, 187);
            Point south = Project(CampusLayout.Columns - 1, CampusLayout.Rows - 1);
            Point west = Project(0, CampusLayout.Rows - 1);
            Point east = Project(CampusLayout.Columns - 1, 0);
            south.Offset(0, HalfHeight);
            west.Offset(-HalfWidth, 0);
            east.Offset(HalfWidth, 0);
            Polygon(dc, Earth, null, west, south, east, Offset(east, 0, 14), Offset(south, 0, 14), Offset(west, 0, 14));

            for (int row = 0; row < CampusLayout.Rows; row++)
            {
                for (int column = 0; column < CampusLayout.Columns; column++)
                {
                    bool road = row == CampusLayout.MainPathRow;
                    Point p = Project(column, row);
                    DrawTile(dc, new CampusTile(column, row),
                        road ? PathSurface : Grass[(column * 7 + row * 11 + column * row) % Grass.Length],
                        road ? RoadMark : GridLine, 1);
                    if (road)
                    {
                        dc.DrawLine(LineForRoad, Offset(p, -9, -4.5), Offset(p, 9, 4.5));
                        if (column % 3 == 0)
                            dc.DrawEllipse(WarmHighlight, null, Offset(p, 12, -7), 4, 2);
                        continue;
                    }
                    for (int detail = 0; detail < 4; detail++)
                    {
                        int hash = column * 3011 + row * 679 + detail * 127;
                        dc.DrawEllipse(TreeLight, null, Offset(p, hash % 26 - 13, hash / 7 % 14 - 7), 1.5, 0.65);
                    }
                }
            }
        }
        drawing.Freeze();
        return drawing;
    }

    private static readonly Pen LineForRoad = Line("#D8CA99", 2);

    private static DrawingGroup DrawStructures(ImmutableArray<CampusBuilding> layout)
    {
        var drawing = new DrawingGroup();
        var occupied = layout.ToDictionary(building => building.Tile, building => building.Facility);
        using (var dc = drawing.Open())
        {
            for (int depth = 0; depth < CampusLayout.Columns + CampusLayout.Rows - 1; depth++)
            {
                for (int row = 0; row < CampusLayout.Rows; row++)
                {
                    int column = depth - row;
                    if (column is < 0 or >= CampusLayout.Columns)
                        continue;
                    var tile = new CampusTile(column, row);
                    Point point = Project(column, row);
                    if (occupied.TryGetValue(tile, out Facility facility))
                        DrawBuilding(dc, point, facility);
                    else if (row != CampusLayout.MainPathRow && (column * 7 + row * 3) % 9 == 0)
                        DrawTree(dc, point);
                }
            }
        }
        drawing.Freeze();
        return drawing;
    }

    private static void DrawBuilding(DrawingContext dc, Point p, Facility facility)
    {
        double height = facility switch { Facility.Lab => 28, Facility.Library => 31, Facility.Bus => 13, _ => 23 };
        double width = facility == Facility.Bus ? 20 : 24;
        double depth = width / 2;
        var left = Offset(p, -width, 0);
        var right = Offset(p, width, 0);
        var front = Offset(p, 0, depth);
        var back = Offset(p, 0, -depth);
        var a = Offset(left, 0, -height);
        var b = Offset(front, 0, -height);
        var c = Offset(right, 0, -height);
        var d = Offset(back, 0, -height);

        dc.DrawEllipse(Shadow, null, Offset(p, 7, 6), width + 3, depth + 2);
        Polygon(dc, Concrete, null, Offset(p, -28, 1), Offset(p, 0, -13), Offset(p, 28, 1), Offset(p, 0, 15));
        Polygon(dc, WallDark, null, a, b, front, left);
        Polygon(dc, WallLight, null, b, c, right, front);
        Polygon(dc, Roofs[(int)facility], RoofEdge, a, d, c, b);

        dc.DrawLine(RoofEdge, Offset(a, 1, 2), Offset(b, -1, 2));
        for (int index = 1; index <= 3; index++)
        {
            double t = index / 4.0;
            Point window = new(b.X + (c.X - b.X) * t, b.Y + (c.Y - b.Y) * t + 7);
            dc.DrawLine(RoofEdge, Offset(window, -2, -1), Offset(window, 3, -3));
            Polygon(dc, index == 2 && facility != Facility.Bus ? Door : WindowGlass, null,
                Offset(window, -2.5, 1), Offset(window, 2.5, -1.5),
                Offset(window, 2.5, 4), Offset(window, -2.5, 6.5));
        }

        Point fixture = Offset(p, -3, -height - 2);
        if (facility is Facility.Lab or Facility.Classroom)
        {
            Polygon(dc, WindowGlass, RoofEdge, Offset(fixture, -10, 0), Offset(fixture, 0, -5),
                Offset(fixture, 10, 0), Offset(fixture, 0, 5));
        }
        else if (facility == Facility.Library)
        {
            dc.DrawEllipse(WindowGlass, RoofEdge, fixture, 8, 4);
        }
        else if (facility == Facility.Canteen)
        {
            for (int stripe = 0; stripe < 3; stripe++)
                dc.DrawEllipse(stripe == 1 ? WallLight : Roofs[(int)Facility.Canteen],
                    RoofEdge, Offset(fixture, stripe * 5 - 3, 0), 4, 3);
        }
    }

    private static void DrawTree(DrawingContext dc, Point p)
    {
        dc.DrawEllipse(Shadow, null, Offset(p, 5, 2), 11, 5);
        dc.DrawRectangle(Bark, null, new Rect(p.X - 1.5, p.Y - 9, 3, 11));
        dc.DrawEllipse(TreeDark, null, Offset(p, 0, -10), 9, 10);
        dc.DrawEllipse(TreeLight, null, Offset(p, -2, -14), 6, 7);
    }

    private void DrawPeople(DrawingContext dc)
    {
        int visible = Math.Min(studentCount, 26);
        double time = SystemParameters.ClientAreaAnimation ? animationClock.Elapsed.TotalSeconds : 0;
        for (int i = 0; i < visible; i++)
        {
            double position = (i * 1.73 + time * (0.34 + (i % 4) * 0.07)) % 25.5;
            double column = position <= 12.75 ? position : 25.5 - position;
            Point p = Project(column, CampusLayout.MainPathRow + (i % 2 == 0 ? 0.19 : -0.19));
            double step = Math.Sin(time * 9 + i) * 0.9;
            dc.DrawEllipse(Shadow, null, Offset(p, 1, 1), 3.6, 1.5);
            dc.DrawEllipse(Jackets[i % Jackets.Length], null, Offset(p, 0, -3 + step), 2.4, 3.5);
            dc.DrawEllipse(Skin, null, Offset(p, 0, -7 + step), 1.7, 1.8);
        }
    }

    private static void DrawTile(DrawingContext dc, CampusTile tile, Brush? fill, Pen? pen, double size)
    {
        Point p = Project(tile.Column, tile.Row);
        Polygon(dc, fill, pen, Offset(p, 0, -HalfHeight * size), Offset(p, HalfWidth * size, 0),
            Offset(p, 0, HalfHeight * size), Offset(p, -HalfWidth * size, 0));
    }

    private static void Polygon(DrawingContext dc, Brush? fill, Pen? pen, Point first, params Point[] rest)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(first, fill is not null, true);
            context.PolyLineTo(rest, true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(fill, pen, geometry);
    }

    private static Point Offset(Point p, double x, double y) => new(p.X + x, p.Y + y);

    private static Brush CreateBackdrop()
    {
        var brush = new RadialGradientBrush(
            (Color)ColorConverter.ConvertFromString("#285044"),
            (Color)ColorConverter.ConvertFromString("#0C2425"));
        brush.GradientOrigin = new Point(0.47, 0.38);
        brush.RadiusX = 0.64;
        brush.RadiusY = 0.67;
        brush.Freeze();
        return brush;
    }

    private static Brush BrushFrom(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private static Pen Line(string color, double thickness)
    {
        var pen = new Pen(BrushFrom(color), thickness);
        pen.Freeze();
        return pen;
    }
}
