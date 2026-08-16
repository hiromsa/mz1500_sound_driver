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
    /// FB (Feedback) 0〜7 を横バーメーターで表示するカスタムコントロール。
    /// 値の領域をアクセントカラーで塗りつぶし、マウスドラッグで値変更可能。
    /// </summary>
    public class FbMeterControl : Control
    {
        public static readonly StyledProperty<int> ValueProperty =
            AvaloniaProperty.Register<FbMeterControl, int>(nameof(Value), 0, defaultBindingMode: BindingMode.TwoWay);

        public int Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        static FbMeterControl()
        {
            AffectsRender<FbMeterControl>(ValueProperty);
        }

        private bool _isDragging;

        private static readonly Color AccentColor = Color.Parse("#007ACC");
        private static readonly Color TrackBg = Color.Parse("#181818");
        private static readonly Color TrackBorder = Color.Parse("#404040");

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            double w = Bounds.Width;
            double h = Bounds.Height;
            if (w < 20 || h < 8) return;

            double barX = 0;
            double barY = (h - 16) / 2; // Center vertically, height 16
            double barW = w;
            double barH = 16;

            // Track background
            var trackRect = new Rect(barX, barY, barW, barH);
            context.DrawRectangle(new SolidColorBrush(TrackBg),
                new Pen(new SolidColorBrush(TrackBorder), 1), trackRect);

            // Fill: from left up to current value position
            // Value 0 = empty, Value 7 = full bar
            double fillRatio = Math.Clamp(Value / 7.0, 0, 1);
            double fillW = barW * fillRatio;
            if (fillW > 0)
            {
                var fillRect = new Rect(barX + 1, barY + 1, fillW - 2, barH - 2);
                context.DrawRectangle(new SolidColorBrush(AccentColor), null, fillRect);
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            _isDragging = true;
            e.Pointer.Capture(this);
            UpdateValueFromPointer(e.GetPosition(this));
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (_isDragging)
            {
                UpdateValueFromPointer(e.GetPosition(this));
                e.Handled = true;
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (_isDragging)
            {
                _isDragging = false;
                e.Pointer.Capture(null);
            }
        }

        private void UpdateValueFromPointer(Point pos)
        {
            double barW = Bounds.Width;
            if (barW <= 0) return;

            double ratio = Math.Clamp(pos.X / barW, 0, 1);
            Value = (int)Math.Round(ratio * 7);
            InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(100, 24);
        }
    }
}
