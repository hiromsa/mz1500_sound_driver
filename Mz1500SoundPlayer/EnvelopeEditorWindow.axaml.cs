using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Mz1500SoundPlayer.Sound;

namespace Mz1500SoundPlayer;

public partial class EnvelopeEditorWindow : Window
{
    public string ResultMmlLine { get; private set; } = string.Empty;

    public enum EnvelopeType { Volume, Pitch }

    private EnvelopeType _type;
    private int _initialIndex = -1;
    private List<int> _values = new List<int>();
    private int _loopIndex = -1;    // -1 means no loop
    private int _releaseIndex = -1; // -1 means no release
    private HashSet<int> _existingIds = new HashSet<int>();

    // Constants for drawing
    private const int MaxSteps = 64; // arbitrary max for UI
    private const double StepWidth = 8.0;
    private const double Spacing = 2.0;
    
    // Bounds for different types
    private int _minValue = 0;
    private int _maxValue = 15;
    
    private bool _isUpdatingFromText = false;
    private bool _isDraggingGraph = false;

    public EnvelopeEditorWindow()
    {
        InitializeComponent();
    }

    public EnvelopeEditorWindow(EnvelopeType type, int id, string existingData, HashSet<int> existingIds) : this()
    {
        _type = type;
        _initialIndex = id;
        _existingIds = existingIds ?? new HashSet<int>();

        TxtType.Text = type == EnvelopeType.Volume ? "@v" : "@EP";
        NumIndex.Value = id;

        if (type == EnvelopeType.Volume)
        {
            _minValue = 0;
            _maxValue = 15;
        }
        else
        {
            _minValue = -128; // Standard MML pitch bend range is usually broad, let's limit arbitrarily for UI initially
            _maxValue = 127;  // This might need adjustment based on user feedback
        }

        CmbPreset.Items.Clear();
        CmbPreset.Items.Add(new ComboBoxItem { Content = "PSG (単音)" });
        CmbPreset.Items.Add(new ComboBoxItem { Content = "PSG (音階)" });
        CmbPreset.Items.Add(new ComboBoxItem { Content = "ノイズ: ホワイト (固定周波数)" });
        CmbPreset.Items.Add(new ComboBoxItem { Content = "ノイズ: 周期 (固定周波数)" });
        CmbPreset.Items.Add(new ComboBoxItem { Content = "ノイズ: ホワイト (3ch連動)" });
        CmbPreset.Items.Add(new ComboBoxItem { Content = "ノイズ: 周期 (3ch連動)" });
        CmbPreset.SelectedIndex = 0;

        ParseExistingData(existingData);
        GeneratePreviewMml();
        RequestRender();
    }

    private void ParseExistingData(string mmlData)
    {
        if (string.IsNullOrWhiteSpace(mmlData))
        {
            // Default 1 step empty
            _values = new List<int> { 0 };
            _loopIndex = -1;
            UpdateTextBox();
            return;
        }

        // Extremely simplified parser for just initial data populate.
        // E.g. {15x3, 14, |, 10x2} -> removes {}, parses "15x3", finds "|"
        mmlData = mmlData.Replace("{", "").Replace("}", "").Trim();
        
        var parts = mmlData.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        _values.Clear();
        _loopIndex = -1;
        _releaseIndex = -1;

        foreach (var p in parts)
        {
            if (p == "|")
            {
                _loopIndex = _values.Count;
            }
            else if (p == ">")
            {
                _releaseIndex = _values.Count;
            }
            else if (p.Contains('x', StringComparison.OrdinalIgnoreCase))
            {
                var kv = p.Split(new[] { 'x', 'X' });
                if (kv.Length == 2 && int.TryParse(kv[0], out int val) && int.TryParse(kv[1], out int count))
                {
                    for (int i = 0; i < count; i++) _values.Add(val);
                }
            }
            else if (int.TryParse(p, out int val))
            {
                _values.Add(val);
            }
        }

        if (_values.Count == 0) _values.Add(0); // Failsafe
        UpdateTextBox();
    }

    private void UpdateTextBox()
    {
        _isUpdatingFromText = true;
        
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < _values.Count; i++)
        {
            if (i == _loopIndex)    sb.Append("|, ");
            if (i == _releaseIndex) sb.Append(">, ");
            sb.Append(_values[i]);
            if (i < _values.Count - 1) sb.Append(", ");
        }
        
        // Edge cases: markers placed after last element
        if (_loopIndex == _values.Count)    sb.Append(", |");
        if (_releaseIndex == _values.Count) sb.Append(", >");

        TxtData.Text = sb.ToString();
        _isUpdatingFromText = false;
    }

    private void TxtData_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdatingFromText) return;
        
        // Try to parse back to arrays to render
        try
        {
             ParseDataString(TxtData.Text);
             RequestRender();
        }
        catch
        {
             // Ignore partial invalid typed states
        }
    }

    private void ParseDataString(string dataStr)
    {
        if (string.IsNullOrWhiteSpace(dataStr)) return;
        var parts = dataStr.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var newVals = new List<int>();
        int newLoop = -1;
        int newRelease = -1;

        foreach(var p in parts)
        {
            if (p == "|")      newLoop    = newVals.Count;
            else if (p == ">") newRelease = newVals.Count;
            else if (int.TryParse(p, out int val))
            {
               val = Math.Clamp(val, _minValue, _maxValue);
               newVals.Add(val);
            }
        }

        if (newVals.Count > 0)
        {
             _values = newVals;
             _loopIndex = newLoop;
             _releaseIndex = newRelease;
        }
    }

    private void RequestRender()
    {
        GraphCanvas.Children.Clear();
        LoopMarkerCanvas.Children.Clear();
        
        if (_values == null || _values.Count == 0) return;

        double cvH = GraphCanvas.Bounds.Height;
        if (cvH <= 0) cvH = 200; // fallback if not layouted yet

        int count = Math.Min(_values.Count, MaxSteps); // Limit visual steps
        
        // Render Bars
        for (int i = 0; i < count; i++)
        {
             double x = i * (StepWidth + Spacing);
             
             // Normalize Y
             double normalizedVal = (double)(_values[i] - _minValue) / (_maxValue - _minValue);
             double h = normalizedVal * cvH;
             double y = cvH - h;

             // Center zero for Pitch
             if (_type == EnvelopeType.Pitch)
             {
                  double zeroY = cvH / 2.0;
                  h = Math.Abs(normalizedVal - 0.5) * cvH;
                  if (_values[i] >= 0) y = zeroY - h; // Positive goes up from middle
                  else y = zeroY;                     // Negative goes down from middle
                  if (h < 1) h = 1; // min height for visibility
             }

             // Use different colors: default blue, release = HotPink
             bool isRelease = _releaseIndex >= 0 && i >= _releaseIndex;
             string barColor = isRelease ? "#FF69B4" : "#4FC1FF";

             if (_values[i] == 0)
             {
                 double zeroHeight = 2;
                 double zeroY = _type == EnvelopeType.Volume ? cvH - zeroHeight : (cvH / 2.0) - (zeroHeight / 2.0);
                 
                 var rect = new Avalonia.Controls.Shapes.Rectangle
                 {
                     Width = StepWidth,
                     Height = zeroHeight,
                     Fill = new SolidColorBrush(Color.Parse(barColor)),
                     RadiusX = 2,
                     RadiusY = 2
                 };
                 
                 Canvas.SetLeft(rect, x);
                 Canvas.SetTop(rect, zeroY);
                 GraphCanvas.Children.Add(rect);
             }
             else
             {
                 var rect = new Avalonia.Controls.Shapes.Rectangle
                 {
                     Width = StepWidth,
                     Height = Math.Max(1, h),
                     Fill = new SolidColorBrush(Color.Parse(barColor)),
                     RadiusX = 2,
                     RadiusY = 2
                 };
                 
                 Canvas.SetLeft(rect, x);
                 Canvas.SetTop(rect, y);
                 GraphCanvas.Children.Add(rect);
             }
        }

        // Render zero line for pitch
        if (_type == EnvelopeType.Pitch)
        {
             var zeroLine = new Avalonia.Controls.Shapes.Line
             {
                 StartPoint = new Point(0, cvH/2),
                 EndPoint = new Point(GraphCanvas.Bounds.Width, cvH/2),
                 Stroke = new SolidColorBrush(Color.Parse("#555555")),
                 StrokeThickness = 1,
                 StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 2, 2 }
             };
             GraphCanvas.Children.Add(zeroLine);
        }

        // Render Loop / Release markers
        ReleaseMarkerCanvas.Children.Clear();
        for (int i = 0; i <= count; i++)
        {
            double barCenter = i < count
                ? i * (StepWidth + Spacing) + (StepWidth / 2)
                : count * (StepWidth + Spacing) + (StepWidth / 2);

            // ------ Loop lane (upper) ------
            if (i == _loopIndex && _loopIndex != -1)
            {
               var marker = new Avalonia.Controls.Shapes.Rectangle { Width=8, Height=14, Fill=Brushes.Orange, RadiusX=2, RadiusY=2 };
               Canvas.SetLeft(marker, barCenter - 4);
               LoopMarkerCanvas.Children.Add(marker);
               var gline = new Avalonia.Controls.Shapes.Line
               {
                 StartPoint = new Point(barCenter, 0), EndPoint = new Point(barCenter, cvH),
                 Stroke = Brushes.Orange, StrokeThickness = 1, StrokeDashArray=new Avalonia.Collections.AvaloniaList<double>{4,4}
               };
               GraphCanvas.Children.Add(gline);
            }
            else
            {
               var hit = new Avalonia.Controls.Shapes.Rectangle { Width=StepWidth+Spacing, Height=14, Fill=Brushes.Transparent };
               Canvas.SetLeft(hit, barCenter - ((StepWidth+Spacing)/2));
               LoopMarkerCanvas.Children.Add(hit);
            }

            // ------ Release lane (lower) ------
            if (i == _releaseIndex && _releaseIndex != -1)
            {
               var marker = new Avalonia.Controls.Shapes.Rectangle { Width=8, Height=14, Fill=Brushes.HotPink, RadiusX=2, RadiusY=2 };
               Canvas.SetLeft(marker, barCenter - 4);
               ReleaseMarkerCanvas.Children.Add(marker);
               var gline = new Avalonia.Controls.Shapes.Line
               {
                 StartPoint = new Point(barCenter, 0), EndPoint = new Point(barCenter, cvH),
                 Stroke = Brushes.HotPink, StrokeThickness = 1, StrokeDashArray=new Avalonia.Collections.AvaloniaList<double>{4,4}
               };
               GraphCanvas.Children.Add(gline);
            }
            else
            {
               var hit = new Avalonia.Controls.Shapes.Rectangle { Width=StepWidth+Spacing, Height=14, Fill=Brushes.Transparent };
               Canvas.SetLeft(hit, barCenter - ((StepWidth+Spacing)/2));
               ReleaseMarkerCanvas.Children.Add(hit);
            }
        }
    }
    
    // Defer actual render until bounds are known
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        RequestRender();
    }

    private void GraphCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(GraphCanvas).Properties;
        if (props.IsLeftButtonPressed || props.IsRightButtonPressed)
        {
             _isDraggingGraph = true;
             UpdateValueFromPointer(e.GetPosition(GraphCanvas), props.IsRightButtonPressed);
        }
    }

    private void GraphCanvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        var props = e.GetCurrentPoint(GraphCanvas).Properties;
        if (_isDraggingGraph)
        {
            UpdateValueFromPointer(e.GetPosition(GraphCanvas), props.IsRightButtonPressed);
        }
    }

    private void GraphCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDraggingGraph = false;
        UpdateTextBox();
    }

    private void UpdateValueFromPointer(Point p, bool isRightClick)
    {
        double cvH = GraphCanvas.Bounds.Height;
        int step = (int)(p.X / (StepWidth + Spacing));
        if (step < 0) return;
        
        if (step >= MaxSteps) step = MaxSteps - 1;

        if (isRightClick)
        {
            if (step < _values.Count)
            {
                _values.RemoveRange(step, _values.Count - step);
                if (_loopIndex >= _values.Count) _loopIndex = -1;
                if (_releaseIndex >= _values.Count) _releaseIndex = -1;
                RequestRender();
                if (!_isDraggingGraph) UpdateTextBox();
            }
            return;
        }

        // 左クリック・ドラッグ：値の追加または更新
        // Auto-expand list if drawing beyond current end
        while (_values.Count <= step) 
        {
             _values.Add(_values.Count > 0 ? _values.Last() : 0);
        }

        int val;
        if (_type == EnvelopeType.Volume)
        {
            // linear from bottom
            double normalized = 1.0 - (p.Y / cvH);
            val = (int)Math.Round(normalized * (_maxValue - _minValue) + _minValue);
        }
        else 
        {
            // pitch from center (roughly)
            double normalized = 1.0 - (p.Y / cvH);
            val = (int)Math.Round(normalized * (_maxValue - _minValue) + _minValue);
        }

        val = Math.Clamp(val, _minValue, _maxValue);
        _values[step] = val;
        
        RequestRender();
    }

    private void LoopMarkerCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var p = e.GetPosition(LoopMarkerCanvas);
        int step = (int)Math.Floor(p.X / (StepWidth + Spacing));
        if (step < 0) step = 0;
        
        if (step == _loopIndex) _loopIndex = -1; // toggle off
        else _loopIndex = step;
        
        RequestRender();
        UpdateTextBox();
    }

    private void ReleaseMarkerCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var p = e.GetPosition(ReleaseMarkerCanvas);
        int step = (int)Math.Floor(p.X / (StepWidth + Spacing));
        if (step < 0) step = 0;
        
        if (step == _releaseIndex) _releaseIndex = -1; // toggle off
        else _releaseIndex = step;
        
        RequestRender();
        UpdateTextBox();
    }

    private void NumIndex_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        int newId = (int)(NumIndex.Value ?? 0);
        if (_existingIds != null && _existingIds.Contains(newId) && newId != _initialIndex)
        {
            TxtWarning.IsVisible = true;
            BtnApply.IsEnabled = false;
        }
        else
        {
            TxtWarning.IsVisible = false;
            BtnApply.IsEnabled = true;
        }
        GeneratePreviewMml(); // update identifier
    }

    // -- Preview Area --
    private void CmbPreset_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        GeneratePreviewMml();
    }

    private string _previewTrack = "A";

    private void GeneratePreviewMml()
    {
        if (TxtPreviewMml == null || TxtType == null || NumIndex == null) return;

        string envId = $"{TxtType.Text}{NumIndex.Value}"; // e.g @v0 or @EP0
        string rawId = $"{TxtType.Text.Replace("@", "")}{NumIndex.Value}"; // e.g v0 or EP0
        string cmd = _type == EnvelopeType.Pitch ? rawId : $"@{rawId}";

        switch (CmbPreset.SelectedIndex)
        {
            case 0: 
                TxtPreviewMml.Text = $"{cmd} o4 c2"; 
                _previewTrack = "A";
                break;
            case 1: 
                TxtPreviewMml.Text = $"{cmd} o4 c4def4g2"; 
                _previewTrack = "A";
                break;
            case 2: // ノイズ: ホワイト (固定周波数)
                TxtPreviewMml.Text = $"@wn1 {cmd} o4 c e g"; 
                _previewTrack = "D";
                break;
            case 3: // ノイズ: 周期 (固定周波数)
                TxtPreviewMml.Text = $"@wn0 {cmd} o4 c e g"; 
                _previewTrack = "D";
                break;
            case 4: // ノイズ: ホワイト (3ch連動)
                TxtPreviewMml.Text = $"@in2 {cmd} o11 c2"; 
                _previewTrack = "C";
                break;
            case 5: // ノイズ: 周期 (3ch連動)
                TxtPreviewMml.Text = $"@in1 {cmd} o6 c2"; 
                _previewTrack = "C";
                break;
        }
    }

    private MmlPlayerModel? _previewPlayer;

    private async void BtnPlay_Click(object? sender, RoutedEventArgs e)
    {
        if (_previewPlayer != null)
        {
            _previewPlayer.Stop();
            _previewPlayer = null;
        }

        BtnPlay.IsEnabled = false;
        BtnApply.IsEnabled = false;

        try
        {
            _previewPlayer = new MmlPlayerModel();
            
            // Generate full temporary MML context
            string envDef = GenerateRleDataString();
            string testSeq = TxtPreviewMml.Text ?? "";
            
            string fullMml = $"{envDef}\n{_previewTrack} {testSeq}";

            await _previewPlayer.PlayMmlAsync(fullMml);
        }
        catch (Exception ex)
        {
            // Simple visual error feedback for preview
            TxtPreviewMml.Text = "Error: " + ex.Message;
        }
        finally
        {
            BtnPlay.IsEnabled = true;
            BtnApply.IsEnabled = true;
        }
    }

    private void BtnStop_Click(object? sender, RoutedEventArgs e)
    {
        if (_previewPlayer != null)
        {
            _previewPlayer.Stop();
            _previewPlayer = null;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_previewPlayer != null)
        {
            _previewPlayer.Stop();
            _previewPlayer = null;
        }
        base.OnClosed(e);
    }

    // -- Appy / Cancel --

    private void Apply_Click(object? sender, RoutedEventArgs e)
    {
        // 1. Compile value list + loop point to string definition
        ResultMmlLine = GenerateRleDataString();
        Close(ResultMmlLine);
    }
    
    private string GenerateRleDataString()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"{TxtType.Text}{NumIndex.Value} = {{ ");

        int i = 0;
        while (i < _values.Count)
        {
            if (i == _loopIndex)    sb.Append("|, ");
            if (i == _releaseIndex) sb.Append(">, ");

            int count = 1;
            int val = _values[i];

            // Count run-length. Do NOT cross loop or release boundary.
            while (i + count < _values.Count
                && _values[i + count] == val
                && (i + count) != _loopIndex
                && (i + count) != _releaseIndex)
            {
                count++;
            }

            if (count >= 3)
            {
                sb.Append($"{val}x{count}");
                i += count;
            }
            else
            {
                sb.Append(val);
                i++;
            }

            if (i < _values.Count || _loopIndex == _values.Count || _releaseIndex == _values.Count)
                sb.Append(", ");
        }
        
        if (_loopIndex == _values.Count)    sb.Append("| ");
        if (_releaseIndex == _values.Count) sb.Append("> ");
        
        string res = sb.ToString().Trim();
        if (res.EndsWith(",")) res = res.Substring(0, res.Length - 1);
        
        res += " }";
        return res;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Enter)
        {
            e.Handled = true;
            if (BtnPlay.IsEnabled)
            {
                BtnPlay_Click(BtnPlay, new RoutedEventArgs());
            }
            else
            {
                BtnStop_Click(BtnStop, new RoutedEventArgs());
            }
        }
    }
}
