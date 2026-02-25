using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace Mz1500SoundPlayer;

public class PlaybackHighlightRenderer : IBackgroundRenderer
{
    private readonly SolidColorBrush _highlightBrush;
    private readonly Pen _borderPen;

    // The text segments to highlight. We track it using (Offset, Length).
    public List<(int Offset, int Length)> ActiveSegments { get; set; } = new();

    public PlaybackHighlightRenderer()
    {
        // Use a visible yellow semi-transparent color for the highlight
        _highlightBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 0)); 
        _borderPen = new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 255, 0)), 1);
    }

    public KnownLayer Layer => KnownLayer.Selection; // Draw below text but above background

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (ActiveSegments.Count == 0) return;

        try
        {
            var builder = new BackgroundGeometryBuilder
            {
                AlignToWholePixels = true,
                CornerRadius = 3
            };
            
            foreach (var seg in ActiveSegments)
            {
                if (seg.Offset < 0 || seg.Length <= 0) continue;
                if (seg.Offset >= textView.Document.TextLength) continue;

                int safeLength = Math.Min(seg.Length, textView.Document.TextLength - seg.Offset);
                var segment = new AvaloniaEdit.Document.TextSegment 
                { 
                    StartOffset = seg.Offset, 
                    Length = safeLength 
                };
                builder.AddSegment(textView, segment);
            }

            var geometry = builder.CreateGeometry();
            if (geometry != null)
            {
                drawingContext.DrawGeometry(_highlightBrush, _borderPen, geometry);
            }
        }
        catch
        {
            // Ignore layout exceptions if text changes rapidly
        }
    }
}
