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
    /// TL (Total Level) 0〜127 を縦バーメーターで表示するカスタムコントロール。
    /// 値の領域をアクセントカラーで塗りつぶし、マウスドラッグで値変更可能。
    /// 上=0 (最大音量), 下=127 (無音)。
    /// </summary>
    public class TlMeterControl : Control
    {
        public static readonly StyledProperty<int> ValueProperty =
            AvaloniaProperty.Register<TlMeterControl, int>(nameof(Value), 127, defaultBindingMode: BindingMode.TwoWay);

        public int Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        static TlMeterControl()
        {
            AffectsRender<TlMeterControl>(ValueProperty);
        }

        private bool _isDragging;

        private static readonly Color AccentColor = Color.Parse("#007ACC");
        private static readonly Color TrackBg = Color.Parse("#181818");
        private static readonly Color TrackBorder = Color.Parse("#404040");
        private static readonly Color TextColor = Color.Parse("#E0E0E0");
        private static readonly Color LabelColor = Color.Parse("#888888");

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            double w = Bounds.Width;
            double h = Bounds.Height;
            if (w < 8 || h < 30) return;

            double labelH = 14; // "TL" label at top
            double valueH = 14; // value display at bottom
            double barX = 4;
            double barW = w - 8;
            double barY = labelH + 2;
            double barH = h - labelH - valueH - 4;

            // Label "TL"
            var typeface = new Typeface("Segoe UI");
            var labelBrush = new SolidColorBrush(LabelColor);
            var ft = new FormattedText("TL", CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, 10, labelBrush);
            context.DrawText(ft, new Point((w - ft.Width) / 2, 0));

            // Track background
            var trackRect = new Rect(barX, barY, barW, barH);
            context.DrawRectangle(new SolidColorBrush(TrackBg),
                new Pen(new SolidColorBrush(TrackBorder), 1), trackRect);

            // Fill: from bottom up to current value position
            // Value 0 = full bar (loudest), Value 127 = empty (silent)
            double fillRatio = 1.0 - Math.Clamp(Value / 127.0, 0, 1);
            double fillH = barH * fillRatio;
            if (fillH > 0)
            {
                var fillRect = new Rect(barX + 1, barY + barH - fillH, barW - 2, fillH);
                context.DrawRectangle(new SolidColorBrush(AccentColor), null, fillRect);
            }

            // Value text
            var valueBrush = new SolidColorBrush(TextColor);
            var vt = new FormattedText(Value.ToString(), CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, 11, valueBrush);
            context.DrawText(vt, new Point((w - vt.Width) / 2, h - valueH));
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
            double labelH = 14;
            double valueH = 14;
            double barY = labelH + 2;
            double barH = Bounds.Height - labelH - valueH - 4;
            if (barH <= 0) return;

            // Top = 0 (loudest), Bottom = 127 (silent)
            double ratio = Math.Clamp((pos.Y - barY) / barH, 0, 1);
            Value = (int)Math.Round(ratio * 127);
            InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(32, 160);
        }
    }
}
