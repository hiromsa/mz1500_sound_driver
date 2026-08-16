using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Globalization;

namespace Mz1500SoundPlayer
{
    public class AlgVisualizer : Control
    {
        public static readonly StyledProperty<int> AlgProperty =
            AvaloniaProperty.Register<AlgVisualizer, int>(nameof(Alg), 0);

        public int Alg
        {
            get => GetValue(AlgProperty);
            set => SetValue(AlgProperty, value);
        }

        public static readonly StyledProperty<bool> IsOp1SelectedProperty = AvaloniaProperty.Register<AlgVisualizer, bool>(nameof(IsOp1Selected), false);
        public static readonly StyledProperty<bool> IsOp2SelectedProperty = AvaloniaProperty.Register<AlgVisualizer, bool>(nameof(IsOp2Selected), false);
        public static readonly StyledProperty<bool> IsOp3SelectedProperty = AvaloniaProperty.Register<AlgVisualizer, bool>(nameof(IsOp3Selected), false);
        public static readonly StyledProperty<bool> IsOp4SelectedProperty = AvaloniaProperty.Register<AlgVisualizer, bool>(nameof(IsOp4Selected), false);

        public bool IsOp1Selected { get => GetValue(IsOp1SelectedProperty); set => SetValue(IsOp1SelectedProperty, value); }
        public bool IsOp2Selected { get => GetValue(IsOp2SelectedProperty); set => SetValue(IsOp2SelectedProperty, value); }
        public bool IsOp3Selected { get => GetValue(IsOp3SelectedProperty); set => SetValue(IsOp3SelectedProperty, value); }
        public bool IsOp4Selected { get => GetValue(IsOp4SelectedProperty); set => SetValue(IsOp4SelectedProperty, value); }

        static AlgVisualizer()
        {
            AffectsRender<AlgVisualizer>(AlgProperty);
            AffectsRender<AlgVisualizer>(IsOp1SelectedProperty);
            AffectsRender<AlgVisualizer>(IsOp2SelectedProperty);
            AffectsRender<AlgVisualizer>(IsOp3SelectedProperty);
            AffectsRender<AlgVisualizer>(IsOp4SelectedProperty);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            int alg = Alg;
            if (alg < 0) alg = 0;
            if (alg > 7) alg = 7;
            
            double width = 30; // default for 1 col
            if (alg == 1 || alg == 2 || alg == 3 || alg == 4) width = 55; // 2 cols
            if (alg == 5 || alg == 6) width = 80; // 3 cols
            if (alg == 7) width = 105; // 4 cols

            double height = 100; // 4 rows + arrow
            if (alg == 4 || alg == 5 || alg == 6) height = 55; // 2 rows + arrow
            if (alg == 7) height = 30; // 1 row + arrow
            
            return new Size(width, 100); // Fixed height to align the diagrams nicely
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var pen = new Pen(new SolidColorBrush(Color.Parse("#cccccc")), 1);
            var linePen = new Pen(new SolidColorBrush(Color.Parse("#cccccc")), 1.5);
            var carrierBrush = new SolidColorBrush(Color.Parse("#444444")); // Muted fill for carrier
            var selectedPen = new Pen(new SolidColorBrush(Color.Parse("#007ACC")), 2);
            var selectedBrush = new SolidColorBrush(Color.Parse("#2A303A"));
            var textBrush = new SolidColorBrush(Color.Parse("#cccccc"));
            var typeface = new Typeface("Arial");

            int alg = Alg;
            if (alg < 0) alg = 0;
            if (alg > 7) alg = 7;

            double bx = 16;
            double by = 16;

            // Arrays to define if an operator is a carrier
            bool[] isCarrier = new bool[4];
            switch (alg)
            {
                case 0: isCarrier[3] = true; break;
                case 1: isCarrier[3] = true; break;
                case 2: isCarrier[3] = true; break;
                case 3: isCarrier[3] = true; break;
                case 4: isCarrier[1] = true; isCarrier[3] = true; break;
                case 5: isCarrier[1] = true; isCarrier[2] = true; isCarrier[3] = true; break;
                case 6: isCarrier[1] = true; isCarrier[2] = true; isCarrier[3] = true; break;
                case 7: isCarrier[0] = true; isCarrier[1] = true; isCarrier[2] = true; isCarrier[3] = true; break;
            }

            void DrawOp(int opNum, double x, double y)
            {
                var rect = new Rect(x, y, bx, by);
                bool isSelected = opNum == 1 ? IsOp1Selected : 
                                  opNum == 2 ? IsOp2Selected : 
                                  opNum == 3 ? IsOp3Selected : 
                                  opNum == 4 ? IsOp4Selected : false;

                if (isSelected)
                {
                    context.DrawRectangle(selectedBrush, selectedPen, rect);
                }
                else if (isCarrier[opNum - 1])
                {
                    context.DrawRectangle(carrierBrush, pen, rect);
                }
                else
                {
                    context.DrawRectangle(null, pen, rect);
                }

                // Draw number
                var ft = new FormattedText(opNum.ToString(), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10, textBrush);
                double tx = x + (bx - ft.Width) / 2;
                double ty = y + (by - ft.Height) / 2;
                context.DrawText(ft, new Point(tx, ty));

                // If carrier, draw arrow down
                if (isCarrier[opNum - 1])
                {
                    double ax = x + bx / 2;
                    double ay = y + by;
                    context.DrawLine(linePen, new Point(ax, ay), new Point(ax, ay + 6));
                    // Arrowhead
                    context.DrawLine(linePen, new Point(ax, ay + 6), new Point(ax - 3, ay + 3));
                    context.DrawLine(linePen, new Point(ax, ay + 6), new Point(ax + 3, ay + 3));
                }
            }

            void DrawLink(double x1, double y1, double x2, double y2)
            {
                if (x1 == x2)
                {
                    // vertical line down
                    context.DrawLine(linePen, new Point(x1 + bx/2, y1 + by), new Point(x2 + bx/2, y2));
                }
                else
                {
                    // draw stepped line
                    double cx1 = x1 + bx/2;
                    double cy1 = y1 + by;
                    double cx2 = x2 + bx/2;
                    double cy2 = y2;
                    double midY = cy1 + (cy2 - cy1) / 2;
                    context.DrawLine(linePen, new Point(cx1, cy1), new Point(cx1, midY));
                    context.DrawLine(linePen, new Point(cx1, midY), new Point(cx2, midY));
                    context.DrawLine(linePen, new Point(cx2, midY), new Point(cx2, cy2));
                }
            }

            double cx(int col) => col * 24 + 4;
            double cy(int row) => row * 24 + 4;

            switch (alg)
            {
                case 0:
                    DrawLink(cx(0), cy(0), cx(0), cy(1));
                    DrawLink(cx(0), cy(1), cx(0), cy(2));
                    DrawLink(cx(0), cy(2), cx(0), cy(3));
                    DrawOp(1, cx(0), cy(0)); DrawOp(2, cx(0), cy(1)); DrawOp(3, cx(0), cy(2)); DrawOp(4, cx(0), cy(3));
                    break;
                case 1:
                    DrawLink(cx(0), cy(0), cx(0), cy(1)); // 1->3 (drawn at row 1)
                    DrawLink(cx(1), cy(0), cx(0), cy(1)); // 2->3
                    DrawLink(cx(0), cy(1), cx(0), cy(2)); // 3->4
                    DrawOp(1, cx(0), cy(0)); DrawOp(2, cx(1), cy(0)); DrawOp(3, cx(0), cy(1)); DrawOp(4, cx(0), cy(2));
                    break;
                case 2:
                    DrawLink(cx(1), cy(0), cx(1), cy(1)); // 2->3
                    DrawLink(cx(1), cy(1), cx(0), cy(2)); // 3->4
                    DrawLink(cx(0), cy(1), cx(0), cy(2)); // 1->4
                    DrawOp(1, cx(0), cy(1)); DrawOp(2, cx(1), cy(0)); DrawOp(3, cx(1), cy(1)); DrawOp(4, cx(0), cy(2));
                    break;
                case 3:
                    DrawLink(cx(1), cy(0), cx(1), cy(1)); // 1->2
                    DrawLink(cx(1), cy(1), cx(0), cy(2)); // 2->4
                    DrawLink(cx(0), cy(1), cx(0), cy(2)); // 3->4
                    DrawOp(1, cx(1), cy(0)); DrawOp(2, cx(1), cy(1)); DrawOp(3, cx(0), cy(1)); DrawOp(4, cx(0), cy(2));
                    break;
                case 4:
                    DrawLink(cx(0), cy(0), cx(0), cy(1)); // 1->2
                    DrawLink(cx(1), cy(0), cx(1), cy(1)); // 3->4
                    DrawOp(1, cx(0), cy(0)); DrawOp(2, cx(0), cy(1)); DrawOp(3, cx(1), cy(0)); DrawOp(4, cx(1), cy(1));
                    break;
                case 5:
                    DrawLink(cx(1), cy(0), cx(0), cy(1)); // 1->2
                    DrawLink(cx(1), cy(0), cx(1), cy(1)); // 1->3
                    DrawLink(cx(1), cy(0), cx(2), cy(1)); // 1->4
                    DrawOp(1, cx(1), cy(0)); DrawOp(2, cx(0), cy(1)); DrawOp(3, cx(1), cy(1)); DrawOp(4, cx(2), cy(1));
                    break;
                case 6:
                    DrawLink(cx(0), cy(0), cx(0), cy(1)); // 1->2
                    DrawOp(1, cx(0), cy(0)); DrawOp(2, cx(0), cy(1)); DrawOp(3, cx(1), cy(1)); DrawOp(4, cx(2), cy(1));
                    break;
                case 7:
                    DrawOp(1, cx(0), cy(0)); DrawOp(2, cx(1), cy(0)); DrawOp(3, cx(2), cy(0)); DrawOp(4, cx(3), cy(0));
                    break;
            }
        }
    }
}
