using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Mz1500SoundPlayer
{
    /// <summary>
    /// YM2151のADSRエンベロープを折れ線グラフで描画し、
    /// 5つのハンドルをドラッグしてAR/D1R/D2R/RR/D1Lを操作できるカスタムコントロール。
    /// </summary>
    public class EnvelopeVisualizer : Control
    {
        // --- StyledProperties (TwoWay default) ---
        public static readonly StyledProperty<int> ArProperty =
            AvaloniaProperty.Register<EnvelopeVisualizer, int>(nameof(Ar), 31, defaultBindingMode: BindingMode.TwoWay);
        public static readonly StyledProperty<int> D1rProperty =
            AvaloniaProperty.Register<EnvelopeVisualizer, int>(nameof(D1r), 0, defaultBindingMode: BindingMode.TwoWay);
        public static readonly StyledProperty<int> D2rProperty =
            AvaloniaProperty.Register<EnvelopeVisualizer, int>(nameof(D2r), 0, defaultBindingMode: BindingMode.TwoWay);
        public static readonly StyledProperty<int> RrProperty =
            AvaloniaProperty.Register<EnvelopeVisualizer, int>(nameof(Rr), 15, defaultBindingMode: BindingMode.TwoWay);
        public static readonly StyledProperty<int> D1lProperty =
            AvaloniaProperty.Register<EnvelopeVisualizer, int>(nameof(D1l), 0, defaultBindingMode: BindingMode.TwoWay);
        public static readonly StyledProperty<int> TlProperty =
            AvaloniaProperty.Register<EnvelopeVisualizer, int>(nameof(Tl), 0);

        public int Ar  { get => GetValue(ArProperty);  set => SetValue(ArProperty, value); }
        public int D1r { get => GetValue(D1rProperty); set => SetValue(D1rProperty, value); }
        public int D2r { get => GetValue(D2rProperty); set => SetValue(D2rProperty, value); }
        public int Rr  { get => GetValue(RrProperty);  set => SetValue(RrProperty, value); }
        public int D1l { get => GetValue(D1lProperty); set => SetValue(D1lProperty, value); }
        public int Tl  { get => GetValue(TlProperty);  set => SetValue(TlProperty, value); }

        static EnvelopeVisualizer()
        {
            AffectsRender<EnvelopeVisualizer>(ArProperty, D1rProperty, D2rProperty, RrProperty, D1lProperty, TlProperty);
        }

        // --- Drag state ---
        private enum HandleId { None, AttackPeak, D1LPoint, KeyOff, ReleaseEnd }
        private HandleId _dragging = HandleId.None;
        private const double HandleRadius = 5.0;
        private const double Pad = 8.0;

        // Cached handle positions (set during Render)
        private Point _hAttack, _hD1L, _hKeyOff, _hRelease;

        // --- Colors ---
        private static readonly Color AccentColor = Color.Parse("#007ACC");
        private static readonly Color AccentHover = Color.Parse("#1A9FFF");
        private static readonly Color LineColor = Color.Parse("#4ec9b0");
        private static readonly Color GridColor = Color.Parse("#2a2a2a");
        private static readonly Color BgColor = Color.Parse("#181818");
        private static readonly Color LabelColor = Color.Parse("#666666");
        private static readonly Color HandleDefault = Color.Parse("#4ec9b0");

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            double w = Bounds.Width;
            double h = Bounds.Height;
            if (w < 20 || h < 20) return;

            double gw = w - Pad * 2;
            double gh = h - Pad * 2 - 12; // 12px for bottom labels

            // Background
            context.DrawRectangle(new SolidColorBrush(BgColor), null, new Rect(0, 0, w, h));

            // Grid lines
            var gridPen = new Pen(new SolidColorBrush(GridColor), 0.5);
            for (int i = 1; i <= 3; i++)
            {
                double gy = Pad + gh * i / 4.0;
                context.DrawLine(gridPen, new Point(Pad, gy), new Point(Pad + gw, gy));
            }

            // --- Compute envelope points ---
            double toY(double attenuation)
            {
                double clamped = Math.Clamp(attenuation, 0, 127);
                return Pad + (clamped / 127.0) * gh;
            }

            // Segment widths
            double attackW  = Math.Max(6, (32 - Math.Min(Ar, 31))  * gw / 100.0);
            double decay1W  = Math.Max(6, (32 - Math.Min(D1r, 31)) * gw / 100.0);
            double sustainW = gw * 0.18;
            double releaseW = Math.Max(6, (16 - Math.Min(Rr, 15))  * gw / 50.0);

            double totalW = attackW + decay1W + sustainW + releaseW;
            if (totalW > gw)
            {
                double scale = gw / totalW;
                attackW *= scale; decay1W *= scale; sustainW *= scale; releaseW *= scale;
            }

            double d1lAtt = D1l * 8;
            double d2rEnd = d1lAtt + (D2r > 0 ? D2r * 3 : 0);
            d2rEnd = Math.Min(d2rEnd, 127);

            double x0 = Pad;
            double x1 = x0 + attackW;
            double x2 = x1 + decay1W;
            double x3 = x2 + sustainW;
            double x4 = x3 + releaseW;

            double yStart = 127, yPeak = 0, yD1L = d1lAtt, yD2End = d2rEnd, yEnd = 127;

            // Cache handle positions
            _hAttack  = new Point(x1, toY(yPeak));
            _hD1L     = new Point(x2, toY(yD1L));
            _hKeyOff  = new Point(x3, toY(yD2End));
            _hRelease = new Point(x4, toY(yEnd));

            // Filled area
            var fillBrush = new SolidColorBrush(Color.FromArgb(30, AccentColor.R, AccentColor.G, AccentColor.B));
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(x0, toY(yStart)), true);
                ctx.LineTo(_hAttack);
                ctx.LineTo(_hD1L);
                ctx.LineTo(_hKeyOff);
                ctx.LineTo(_hRelease);
                ctx.LineTo(new Point(x4, Pad + gh));
                ctx.LineTo(new Point(x0, Pad + gh));
                ctx.EndFigure(true);
            }
            context.DrawGeometry(fillBrush, null, geo);

            // Envelope lines
            var linePen = new Pen(new SolidColorBrush(LineColor), 1.5);
            context.DrawLine(linePen, new Point(x0, toY(yStart)), _hAttack);
            context.DrawLine(linePen, _hAttack, _hD1L);
            context.DrawLine(linePen, _hD1L, _hKeyOff);

            var releasePen = new Pen(new SolidColorBrush(LineColor), 1.5,
                new DashStyle(new double[] { 4, 3 }, 0));
            context.DrawLine(releasePen, _hKeyOff, _hRelease);

            // Key-off marker
            var markerPen = new Pen(new SolidColorBrush(Color.Parse("#555555")), 1,
                new DashStyle(new double[] { 2, 2 }, 0));
            context.DrawLine(markerPen, new Point(x3, Pad), new Point(x3, Pad + gh));

            // Draw handles
            void DrawHandle(Point pt, HandleId id, string label)
            {
                bool active = _dragging == id;
                var brush = new SolidColorBrush(active ? AccentHover : HandleDefault);
                double r = active ? HandleRadius + 1 : HandleRadius;
                context.DrawEllipse(brush, new Pen(Brushes.White, 1), pt, r, r);
            }

            DrawHandle(_hAttack,  HandleId.AttackPeak,  "A");
            DrawHandle(_hD1L,     HandleId.D1LPoint,    "D1");
            DrawHandle(_hKeyOff,  HandleId.KeyOff,      "D2");
            DrawHandle(_hRelease, HandleId.ReleaseEnd,  "R");

            // Phase labels at bottom
            var labelBrush = new SolidColorBrush(LabelColor);
            var typeface = new Typeface("Segoe UI");
            double fontSize = 9;
            double labelY = Pad + gh + 2;

            void DrawLabel(string text, double lx, double rx)
            {
                var ft = new FormattedText(text, CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, fontSize, labelBrush);
                double tx = (lx + rx) / 2 - ft.Width / 2;
                tx = Math.Clamp(tx, Pad, Pad + gw - ft.Width);
                context.DrawText(ft, new Point(tx, labelY));
            }

            DrawLabel("A",  x0, x1);
            DrawLabel("D1", x1, x2);
            DrawLabel("D2", x2, x3);
            DrawLabel("R",  x3, x4);
        }

        // --- Mouse interaction ---
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            var pos = e.GetPosition(this);
            _dragging = HitTest(pos);
            if (_dragging != HandleId.None)
            {
                e.Handled = true;
                e.Pointer.Capture(this);
                InvalidateVisual();
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            var pt = e.GetPosition(this);
            if (_dragging == HandleId.None)
            {
                // Show cursor hint
                var hovered = HitTest(pt);
                if (hovered == HandleId.AttackPeak || hovered == HandleId.ReleaseEnd)
                    Cursor = new Cursor(StandardCursorType.SizeWestEast);
                else if (hovered == HandleId.KeyOff)
                    Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
                else if (hovered == HandleId.D1LPoint)
                    Cursor = new Cursor(StandardCursorType.SizeAll);
                else
                    Cursor = Cursor.Default;
                return;
            }

            double w = Bounds.Width;
            double h = Bounds.Height;
            double gw = w - Pad * 2;
            double gh = h - Pad * 2 - 12;

            switch (_dragging)
            {
                case HandleId.AttackPeak:
                {
                    double attackW = Math.Clamp(pt.X - Pad, 6, gw * 0.4);
                    int newAr = 32 - (int)Math.Round(attackW * 100.0 / gw);
                    Ar = Math.Clamp(newAr, 0, 31);
                    break;
                }
                case HandleId.D1LPoint:
                {
                    double decay1W = Math.Clamp(pt.X - _hAttack.X, 6, gw * 0.4);
                    int newD1r = 32 - (int)Math.Round(decay1W * 100.0 / gw);
                    D1r = Math.Clamp(newD1r, 0, 31);

                    double yNorm = Math.Clamp((pt.Y - Pad) / gh, 0, 1);
                    int newD1l = (int)Math.Round(yNorm * 127.0 / 8.0);
                    D1l = Math.Clamp(newD1l, 0, 15);
                    break;
                }
                case HandleId.KeyOff:
                {
                    // Vertical drag changes D2R
                    double dropY = pt.Y - _hD1L.Y;
                    int newD2r = (int)Math.Round((dropY / gh) * (127.0 / 3.0));
                    D2r = Math.Clamp(newD2r, 0, 31);
                    break;
                }
                case HandleId.ReleaseEnd:
                {
                    double releaseW = Math.Clamp(pt.X - _hKeyOff.X, 6, gw * 0.5);
                    int newRr = 16 - (int)Math.Round(releaseW * 50.0 / gw);
                    Rr = Math.Clamp(newRr, 0, 15);
                    break;
                }
            }
            e.Handled = true;
            InvalidateVisual();
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (_dragging != HandleId.None)
            {
                _dragging = HandleId.None;
                e.Pointer.Capture(null);
                InvalidateVisual();
            }
        }

        private HandleId HitTest(Point pos)
        {
            double r2 = (HandleRadius + 4) * (HandleRadius + 4); // generous hit area
            if (Dist2(pos, _hAttack)  < r2) return HandleId.AttackPeak;
            if (Dist2(pos, _hD1L)     < r2) return HandleId.D1LPoint;
            if (Dist2(pos, _hKeyOff)  < r2) return HandleId.KeyOff;
            if (Dist2(pos, _hRelease) < r2) return HandleId.ReleaseEnd;
            return HandleId.None;
        }

        private static double Dist2(Point a, Point b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }
    }
}
