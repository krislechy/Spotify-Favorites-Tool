using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace PrettyCats;

public sealed class PrettyCatsLayer : FrameworkElement
{
    private const int ActorCount = 3;
    private const double Pixel = 2.4;
    private const double GroundY = 1.0;
    private static readonly Random Random = new();

    private readonly DispatcherTimer _timer;
    private readonly CatActor[] _cats;
    private int _tick;

    public PrettyCatsLayer()
    {
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
        ClipToBounds = false;

        _cats = Enumerable.Range(0, ActorCount)
            .Select(index => CatActor.Create(index))
            .ToArray();

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(90)
        };
        _timer.Tick += Timer_Tick;

        Loaded += (_, _) => Start();
        Unloaded += (_, _) => Stop();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                Start();
                return;
            }

            Stop();
        };
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        foreach (var cat in _cats.OrderBy(cat => cat.Y))
        {
            DrawPortal(drawingContext, cat);
            if (cat.IsVisible)
            {
                DrawCat(drawingContext, cat);
            }
        }
    }

    private void Start()
    {
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private void Stop()
    {
        if (_timer.IsEnabled)
        {
            _timer.Stop();
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _tick++;
        var width = Math.Max(220, ActualWidth);
        foreach (var cat in _cats)
        {
            cat.Update(width, _tick);
        }

        InvalidateVisual();
    }

    private static void DrawCat(DrawingContext dc, CatActor cat)
    {
        var frame = (cat.Frame / 3) % 2;
        var y = cat.Y + cat.JumpOffset;
        var x = cat.X;
        var direction = cat.Direction;
        var body = cat.BodyBrush;
        var dark = cat.DarkBrush;
        var light = cat.LightBrush;
        var eye = Brushes.Black;
        var sleep = cat.State == CatState.Sleeping;
        var grooming = cat.State == CatState.Grooming;

        PixelRect(dc, dark, x, y + 9 * Pixel, 16, 1, direction);
        PixelRect(dc, body, x + 1 * Pixel * direction, y + 5 * Pixel, 13, 5, direction);
        PixelRect(dc, body, x + 4 * Pixel * direction, y + 4 * Pixel, 8, 1, direction);
        PixelRect(dc, light, x + 3 * Pixel * direction, y + 8 * Pixel, 8, 1, direction);
        PixelRect(dc, dark, x + 2 * Pixel * direction, y + 5 * Pixel, 2, 1, direction);
        PixelRect(dc, dark, x + 8 * Pixel * direction, y + 5 * Pixel, 2, 1, direction);

        PixelRect(dc, body, x + 11 * Pixel * direction, y + 3 * Pixel, 7, 6, direction);
        PixelRect(dc, body, x + 12 * Pixel * direction, y + 1 * Pixel, 2, 3, direction);
        PixelRect(dc, body, x + 16 * Pixel * direction, y + 1 * Pixel, 2, 3, direction);
        PixelRect(dc, light, x + 13 * Pixel * direction, y + 6 * Pixel, 3, 1, direction);
        PixelRect(dc, dark, x + 17 * Pixel * direction, y + 5 * Pixel, 1, 1, direction);

        if (sleep)
        {
            PixelRect(dc, dark, x + 13 * Pixel * direction, y + 5 * Pixel, 4, 1, direction);
            DrawSleepMarks(dc, x + 20 * Pixel * direction, y - 4 * Pixel, direction, cat.Frame);
        }
        else
        {
            PixelRect(dc, eye, x + 13 * Pixel * direction, y + 5 * Pixel, 1, 1, direction);
            PixelRect(dc, eye, x + 16 * Pixel * direction, y + 5 * Pixel, 1, 1, direction);
        }

        var tailLift = cat.State == CatState.Walking && frame == 0 ? -1 : 0;
        PixelRect(dc, body, x - 2 * Pixel * direction, y + (6 + tailLift) * Pixel, 4, 1, direction);
        PixelRect(dc, body, x - 4 * Pixel * direction, y + (5 + tailLift) * Pixel, 2, 2, direction);
        PixelRect(dc, dark, x - 5 * Pixel * direction, y + (4 + tailLift) * Pixel, 1, 1, direction);

        if (grooming)
        {
            PixelRect(dc, light, x + 9 * Pixel * direction, y + 5 * Pixel, 3, 4, direction);
            PixelRect(dc, Brushes.HotPink, x + 13 * Pixel * direction, y + 8 * Pixel, 1, 1, direction);
        }
        else
        {
            DrawLegs(dc, body, x, y, direction, frame);
        }
    }

    private static void DrawLegs(DrawingContext dc, Brush brush, double x, double y, int direction, int frame)
    {
        if (frame == 0)
        {
            PixelRect(dc, brush, x + 3 * Pixel * direction, y + 10 * Pixel, 2, 2, direction);
            PixelRect(dc, brush, x + 10 * Pixel * direction, y + 10 * Pixel, 2, 2, direction);
            PixelRect(dc, brush, x + 14 * Pixel * direction, y + 9 * Pixel, 2, 2, direction);
            return;
        }

        PixelRect(dc, brush, x + 2 * Pixel * direction, y + 10 * Pixel, 2, 2, direction);
        PixelRect(dc, brush, x + 8 * Pixel * direction, y + 10 * Pixel, 2, 2, direction);
        PixelRect(dc, brush, x + 15 * Pixel * direction, y + 9 * Pixel, 2, 2, direction);
    }

    private static void DrawPortal(DrawingContext dc, CatActor cat)
    {
        if (cat.PortalOpacity <= 0)
        {
            return;
        }

        dc.PushOpacity(cat.PortalOpacity);
        var center = new Point(cat.PortalX, GroundY + 15);
        var phase = cat.Frame % 4;
        var outer = phase % 2 == 0 ? Brushes.MediumPurple : Brushes.DeepSkyBlue;
        var inner = phase % 2 == 0 ? Brushes.DeepSkyBlue : Brushes.MediumPurple;

        PixelRect(dc, outer, center.X - 6 * Pixel, center.Y - 9 * Pixel, 2, 15, 1);
        PixelRect(dc, outer, center.X + 4 * Pixel, center.Y - 9 * Pixel, 2, 15, 1);
        PixelRect(dc, inner, center.X - 4 * Pixel, center.Y - 10 * Pixel, 8, 2, 1);
        PixelRect(dc, inner, center.X - 4 * Pixel, center.Y + 5 * Pixel, 8, 2, 1);
        PixelRect(dc, Brushes.Black, center.X - 4 * Pixel, center.Y - 7 * Pixel, 8, 11, 1);
        dc.Pop();
    }

    private static void DrawSleepMarks(DrawingContext dc, double x, double y, int direction, int frame)
    {
        var offset = frame % 8;
        PixelRect(dc, Brushes.LightCyan, x, y - offset, 3, 1, direction);
        PixelRect(dc, Brushes.LightCyan, x + 2 * Pixel * direction, y + 2 * Pixel - offset, 2, 1, direction);
    }

    private static void PixelRect(DrawingContext dc, Brush brush, double x, double y, int width, int height, int direction)
    {
        var left = direction >= 0 ? x : x - width * Pixel;
        dc.DrawRectangle(brush, null, new Rect(Math.Round(left), Math.Round(y), width * Pixel, height * Pixel));
    }

    private sealed class CatActor
    {
        private int _stateTicks;
        private int _hiddenTicks;
        private double _targetX;

        public required int Index { get; init; }
        public required Brush BodyBrush { get; init; }
        public required Brush DarkBrush { get; init; }
        public required Brush LightBrush { get; init; }
        public CatState State { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public int Direction { get; private set; } = 1;
        public int Frame { get; private set; }
        public double PortalX { get; private set; }
        public double PortalOpacity { get; private set; }
        public double JumpOffset { get; private set; }
        public bool IsVisible => State != CatState.Hidden;

        public static CatActor Create(int index)
        {
            var palette = index switch
            {
                0 => (Body: Color.FromRgb(238, 166, 84), Dark: Color.FromRgb(128, 76, 42), Light: Color.FromRgb(255, 211, 145)),
                1 => (Body: Color.FromRgb(88, 98, 114), Dark: Color.FromRgb(42, 47, 56), Light: Color.FromRgb(177, 188, 202)),
                _ => (Body: Color.FromRgb(234, 235, 218), Dark: Color.FromRgb(112, 121, 111), Light: Color.FromRgb(255, 248, 220))
            };

            return new CatActor
            {
                Index = index,
                X = 42 + index * 96,
                Y = GroundY + index % 2,
                BodyBrush = CreateBrush(palette.Body),
                DarkBrush = CreateBrush(palette.Dark),
                LightBrush = CreateBrush(palette.Light),
                State = CatState.Walking,
                _stateTicks = 35 + index * 16
            }.WithTarget(120 + index * 80);
        }

        private CatActor WithTarget(double targetX)
        {
            _targetX = targetX;
            return this;
        }

        public void Update(double width, int tick)
        {
            Frame++;
            _stateTicks--;

            switch (State)
            {
                case CatState.Walking:
                    UpdateWalking(width);
                    break;
                case CatState.Grooming:
                case CatState.Sleeping:
                    JumpOffset = 0;
                    PortalOpacity = Math.Max(0, PortalOpacity - 0.06);
                    break;
                case CatState.EnteringPortal:
                    UpdateEnteringPortal();
                    break;
                case CatState.Hidden:
                    UpdateHidden(width);
                    break;
                case CatState.ExitingPortal:
                    UpdateExitingPortal();
                    break;
            }

            if (_stateTicks <= 0)
            {
                ChooseNextState(width, tick);
            }
        }

        private void UpdateWalking(double width)
        {
            var distance = _targetX - X;
            Direction = distance >= 0 ? 1 : -1;
            X += Math.Sign(distance) * 1.4;
            JumpOffset = Math.Sin(Frame * 0.8) * 0.8;
            PortalOpacity = Math.Max(0, PortalOpacity - 0.04);

            if (Math.Abs(distance) < 4)
            {
                _targetX = RandomX(width);
            }
        }

        private void UpdateEnteringPortal()
        {
            PortalOpacity = Math.Min(1, PortalOpacity + 0.10);
            X += (PortalX - X) * 0.20;
            JumpOffset = -Math.Sin(Math.Min(1, PortalOpacity) * Math.PI) * 9;
        }

        private void UpdateHidden(double width)
        {
            _hiddenTicks--;
            PortalOpacity = Math.Max(0, PortalOpacity - 0.08);
            if (_hiddenTicks <= 0)
            {
                PortalX = RandomX(width);
                X = PortalX;
                Direction = Random.Next(2) == 0 ? -1 : 1;
                State = CatState.ExitingPortal;
                _stateTicks = 18;
                PortalOpacity = 0.1;
                JumpOffset = -10;
            }
        }

        private void UpdateExitingPortal()
        {
            PortalOpacity = Math.Min(1, PortalOpacity + 0.08);
            JumpOffset += (0 - JumpOffset) * 0.22;
        }

        private void ChooseNextState(double width, int tick)
        {
            if (State == CatState.EnteringPortal)
            {
                State = CatState.Hidden;
                _hiddenTicks = 24 + Random.Next(42);
                _stateTicks = _hiddenTicks;
                return;
            }

            if (State == CatState.ExitingPortal)
            {
                State = CatState.Walking;
                _targetX = RandomX(width);
                _stateTicks = 45 + Random.Next(60);
                return;
            }

            var roll = Random.NextDouble();
            if (roll < 0.18)
            {
                State = CatState.Sleeping;
                _stateTicks = 24 + Random.Next(38);
                return;
            }

            if (roll < 0.38)
            {
                State = CatState.Grooming;
                _stateTicks = 20 + Random.Next(30);
                return;
            }

            if (tick > 40 && roll > 0.78)
            {
                State = CatState.EnteringPortal;
                PortalX = Math.Clamp(X + Direction * 20, 34, width - 28);
                _stateTicks = 20;
                PortalOpacity = 0;
                return;
            }

            State = CatState.Walking;
            _targetX = RandomX(width);
            _stateTicks = 45 + Random.Next(72);
        }

        private static Brush CreateBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static double RandomX(double width)
        {
            return 34 + Random.NextDouble() * Math.Max(80, width - 76);
        }
    }

    private enum CatState
    {
        Walking,
        Grooming,
        Sleeping,
        EnteringPortal,
        Hidden,
        ExitingPortal
    }
}
