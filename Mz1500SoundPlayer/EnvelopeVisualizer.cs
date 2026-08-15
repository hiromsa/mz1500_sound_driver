using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace Mz1500SoundPlayer
{
    /// <summary>
    /// YM2151のADSRエンベロープを折れ線グラフで描画するカスタムコントロール。
    /// AR, D1R, D2R, RR, D1L, TL の各パラメータをバインディングで受け取る。
    /// </summary>
    public class EnvelopeVisualizer : Control
    {
        public static readonly StyledProperty<int> ArProperty =
            AvaloniaProperty.Register<EnvelopeVisualizer, int>(nameof(Ar), 31);
        public static readonly StyledProperty<int> D1rProperty =
            AvaloniaProperty.Register<EnvelopeVisualizer, int>(nameof(D1r), 0);
        public static readonly StyledProperty<int> D2rProperty =
            AvaloniaProperty.Register<EnvelopeVisualizer, int>(nameof(D2r), 0);
        public static readonly StyledProperty<int> RrProperty =
            AvaloniaProperty.Register<EnvelopeVisualizer, int>(nameof(Rr), 15);
        public static readonly StyledProperty<int> D1lProperty =
            AvaloniaProperty.Register<EnvelopeVisualizer, int>(nameof(D1l), 0);
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

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            double w = Bounds.Width;
            double h = Bounds.Height;
            if (w < 10 || h < 10) return;

            double pad = 4;
            double gw = w - pad * 2;  // graph width
            double gh = h - pad * 2;  // graph height

            // Background
            context.DrawRectangle(new SolidColorBrush(Color.Parse("#1a1a1a")), null, new Rect(0, 0, w, h));

            // Grid lines (subtle)
            var gridPen = new Pen(new SolidColorBrush(Color.Parse("#2a2a2a")), 0.5);
            for (int i = 1; i <= 3; i++)
            {
                double gy = pad + gh * i / 4.0;
                context.DrawLine(gridPen, new Point(pad, gy), new Point(pad + gw, gy));
            }

            // --- Calculate envelope points ---
            // Y axis: top = full volume (0 attenuation), bottom = silent (127 attenuation)
            // toY maps an attenuation value (0..127) to pixel Y
            double toY(double attenuation)
            {
                double clamped = Math.Clamp(attenuation, 0, 127);
                return pad + (clamped / 127.0) * gh;
            }

            // Segment widths based on rate values
            // Higher rate = faster = narrower segment
            double attackW  = Math.Max(4, (32 - Math.Min(Ar, 31))  * gw / 120.0);
            double decay1W  = Math.Max(4, (32 - Math.Min(D1r, 31)) * gw / 120.0);
            double sustainW = gw * 0.2;  // fixed width for sustain/D2R display
            double releaseW = Math.Max(4, (16 - Math.Min(Rr, 15))  * gw / 60.0);

            // Normalize so total fits within gw
            double totalW = attackW + decay1W + sustainW + releaseW;
            if (totalW > gw)
            {
                double scale = gw / totalW;
                attackW  *= scale;
                decay1W  *= scale;
                sustainW *= scale;
                releaseW *= scale;
            }

            // D1L level: 0 = sustain at full volume, 15 = sustain at max attenuation
            // D1L maps to attenuation: d1l * 8 roughly
            double d1lAttenuation = D1l * 8;

            // D2R end level: how far the sustain decays
            double d2rEndAttenuation = d1lAttenuation + (D2r > 0 ? D2r * 3 : 0);
            d2rEndAttenuation = Math.Min(d2rEndAttenuation, 127);

            // Points (x, attenuation)
            double x0 = pad;
            double x1 = x0 + attackW;               // end of attack
            double x2 = x1 + decay1W;               // end of decay1
            double x3 = x2 + sustainW;              // end of sustain/D2R (key off point)
            double x4 = x3 + releaseW;              // end of release

            double y_start = 127;                    // start: silent
            double y_peak  = 0;                      // after attack: full volume
            double y_d1l   = d1lAttenuation;         // after decay1: D1L level
            double y_d2end = d2rEndAttenuation;      // after decay2
            double y_end   = 127;                    // after release: silent

            // Draw filled area
            var fillBrush = new SolidColorBrush(Color.FromArgb(40, 78, 201, 176)); // semi-transparent teal
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(x0, toY(y_start)), true);
                ctx.LineTo(new Point(x1, toY(y_peak)));
                ctx.LineTo(new Point(x2, toY(y_d1l)));
                ctx.LineTo(new Point(x3, toY(y_d2end)));
                ctx.LineTo(new Point(x4, toY(y_end)));
                ctx.LineTo(new Point(x4, pad + gh)); // bottom-right
                ctx.LineTo(new Point(x0, pad + gh)); // bottom-left
                ctx.EndFigure(true);
            }
            context.DrawGeometry(fillBrush, null, geo);

            // Draw envelope line
            var linePen = new Pen(new SolidColorBrush(Color.Parse("#4ec9b0")), 1.5);
            context.DrawLine(linePen, new Point(x0, toY(y_start)), new Point(x1, toY(y_peak)));
            context.DrawLine(linePen, new Point(x1, toY(y_peak)),  new Point(x2, toY(y_d1l)));
            context.DrawLine(linePen, new Point(x2, toY(y_d1l)),   new Point(x3, toY(y_d2end)));

            // Release line (dashed style)
            var releasePen = new Pen(new SolidColorBrush(Color.Parse("#4ec9b0")), 1.5,
                new DashStyle(new double[] { 4, 3 }, 0));
            context.DrawLine(releasePen, new Point(x3, toY(y_d2end)), new Point(x4, toY(y_end)));

            // Key-off marker
            var markerPen = new Pen(new SolidColorBrush(Color.Parse("#666666")), 1,
                new DashStyle(new double[] { 2, 2 }, 0));
            context.DrawLine(markerPen, new Point(x3, pad), new Point(x3, pad + gh));

            // Dot at each breakpoint
            var dotBrush = new SolidColorBrush(Color.Parse("#4ec9b0"));
            double dotR = 2.5;
            context.DrawEllipse(dotBrush, null, new Point(x1, toY(y_peak)), dotR, dotR);
            context.DrawEllipse(dotBrush, null, new Point(x2, toY(y_d1l)), dotR, dotR);
            context.DrawEllipse(dotBrush, null, new Point(x3, toY(y_d2end)), dotR, dotR);

            // Labels
            var labelBrush = new SolidColorBrush(Color.Parse("#666666"));
            var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);
            double fontSize = 9;

            void DrawLabel(string text, double x, double y, bool above)
            {
                var ft = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, fontSize, labelBrush);
                double tx = x - ft.Width / 2;
                double ty = above ? y - ft.Height - 2 : y + 2;
                tx = Math.Clamp(tx, pad, pad + gw - ft.Width);
                context.DrawText(ft, new Point(tx, ty));
            }

            DrawLabel("A", (x0 + x1) / 2, pad + gh, false);
            DrawLabel("D1", (x1 + x2) / 2, pad + gh, false);
            DrawLabel("D2", (x2 + x3) / 2, pad + gh, false);
            DrawLabel("R", (x3 + x4) / 2, pad + gh, false);
        }
    }
}
