using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using System.Collections.Generic;
using Mz1500SoundPlayer.Sound;
using System.Linq;

namespace Mz1500SoundPlayer;

public class ErrorHighlightRenderer : IBackgroundRenderer
{
    public KnownLayer Layer => KnownLayer.Selection;

    // List of validation errors from the parser
    public List<MmlError> ActiveErrors { get; set; } = new();

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (ActiveErrors == null || ActiveErrors.Count == 0) return;

        // Draw red squiggly line (or just a solid red line) under the text
        IBrush brush = Brushes.Red;
        var pen = new Pen(brush, 1.5, dashStyle: DashStyle.Dash); // Dashed line to simulate a squiggly line visually

        foreach (var error in ActiveErrors)
        {
            // Safeguard against invalid lengths
            if (error.TextStartIndex < 0 || error.Length <= 0) continue;
            
            // Limit length within document
            int maxLen = textView.Document.TextLength - error.TextStartIndex;
            if (maxLen <= 0) continue;
            int length = System.Math.Min(error.Length, maxLen);

            // Calculate visuals
            var start = textView.Document.GetLocation(error.TextStartIndex);
            var end = textView.Document.GetLocation(error.TextStartIndex + length);

            // Since errors may cross lines, use BackgroundGeometryBuilder to find exact rects
            var builder = new BackgroundGeometryBuilder
            {
                AlignToWholePixels = true,
                CornerRadius = 0,
            };

            builder.AddSegment(textView, new AvaloniaEdit.Document.TextSegment 
            { 
                StartOffset = error.TextStartIndex, 
                Length = length 
            });

            var geometry = builder.CreateGeometry();
            if (geometry != null)
            {
                // Draw a dashed underline at the bottom of the geometry bounds
                var rect = geometry.Bounds;
                var p1 = new Point(rect.Left, rect.Bottom - 2);
                var p2 = new Point(rect.Right, rect.Bottom - 2);
                drawingContext.DrawLine(pen, p1, p2);

                // Alternatively, to draw an actual wavy line we could construct an arbitrary path
                // However drawing a dashed red line is very standard and efficient.
            }
        }
    }
}
