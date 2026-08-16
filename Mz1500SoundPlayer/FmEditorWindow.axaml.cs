using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Mz1500SoundPlayer.Sound;
using System;

namespace Mz1500SoundPlayer;

public partial class FmEditorWindow : Window
{
    public FmEditorViewModel ViewModel { get; }
    public Action<string>? OnApply { get; set; }
    
    private readonly int _fmNumber;

    public FmEditorWindow()
    {
        InitializeComponent();
        ViewModel = new FmEditorViewModel();
        DataContext = ViewModel;
        _fmNumber = 1;
    }

    public FmEditorWindow(int fmNumber, string mml)
    {
        InitializeComponent();
        ViewModel = new FmEditorViewModel();
        DataContext = ViewModel;
        _fmNumber = fmNumber;

        ViewModel.ParseMml(mml);
        
        // Initialize keyboard
        var state = new ChannelState($"F1", 4, 15, -1, fmNumber, 0, 3, 127, 0, 0);
        var mmlData = new MmlData();
        var td = new FmToneData { Parameters = ParseParameters(ViewModel.ToMml(fmNumber)) };
        td.KeyOnMask = 0x78; // Start fully unmuted
        mmlData.FmVoiceEnvelopes[fmNumber] = td;
        KeyboardControl.InitializeState(state, mmlData);

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.Op1.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.Op2.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.Op3.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.Op4.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        UpdateKeyboardState();
    }

    private void UpdateKeyboardState()
    {
        var state = new ChannelState($"F1", 4, 15, -1, _fmNumber, 0, 3, 127, 0, 0);
        var mmlData = new MmlData();
        var td = new FmToneData { Parameters = ParseParameters(ViewModel.ToMml(_fmNumber)) };
        
        byte mask = 0x78;
        if (ViewModel.Op1.IsMuted) mask &= unchecked((byte)~0x08);
        if (ViewModel.Op2.IsMuted) mask &= unchecked((byte)~0x10);
        if (ViewModel.Op3.IsMuted) mask &= unchecked((byte)~0x20);
        if (ViewModel.Op4.IsMuted) mask &= unchecked((byte)~0x40);

        bool hasSolo = ViewModel.Op1.IsSolo || ViewModel.Op2.IsSolo || ViewModel.Op3.IsSolo || ViewModel.Op4.IsSolo;
        if (hasSolo) {
            mask = 0;
            if (ViewModel.Op1.IsSolo) mask |= 0x08;
            if (ViewModel.Op2.IsSolo) mask |= 0x10;
            if (ViewModel.Op3.IsSolo) mask |= 0x20;
            if (ViewModel.Op4.IsSolo) mask |= 0x40;
        }

        td.KeyOnMask = mask;
        mmlData.FmVoiceEnvelopes[_fmNumber] = td;
        KeyboardControl.UpdateState(state, mmlData);
    }

    private int[] ParseParameters(string mml)
    {
        var match = System.Text.RegularExpressions.Regex.Match(mml, @"\{([^}]+)\}");
        if (match.Success)
        {
            var parts = match.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 46)
            {
                var ret = new int[46];
                for(int i = 0; i < 46; i++)
                    if (int.TryParse(parts[i].Trim(), out int val))
                        ret[i] = val;
                return ret;
            }
        }
        return new int[46];
    }

    private void Apply_Click(object? sender, RoutedEventArgs e)
    {
        OnApply?.Invoke(ViewModel.ToMml(_fmNumber));
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void Copy_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is FmOperatorViewModel op)
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(op.ToClipboardString());
            }
        }
    }

    private async void Paste_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is FmOperatorViewModel op)
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                var text = await clipboard.GetTextAsync();
                if (!string.IsNullOrEmpty(text))
                {
                    op.FromClipboardString(text);
                }
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        KeyboardControl.DisposeAudio();
    }

    private Avalonia.Point _dragStartPoint;
    private bool _isPointerDown;
    private FmOperatorViewModel? _dragOperator;

    private void OperatorPanel_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is FmOperatorViewModel op)
        {
            var visual = e.Source as Avalonia.Visual;
            while (visual != null && visual != sender)
            {
                if (visual is Avalonia.Controls.Primitives.ToggleButton || 
                    visual is Button ||
                    visual is NumericUpDown || 
                    visual is TextBox || 
                    visual is EnvelopeVisualizer ||
                    visual is TlMeterControl ||
                    visual is FbMeterControl)
                {
                    return; // Ignore clicks on interactive controls
                }
                visual = visual.GetVisualParent();
            }

            _isPointerDown = true;
            _dragStartPoint = e.GetPosition(border);
            _dragOperator = op;
        }
    }

    private async void OperatorPanel_PointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (!_isPointerDown || _dragOperator == null || sender is not Border border) return;

        var currentPoint = e.GetPosition(border);
        var diff = currentPoint - _dragStartPoint;

        if (Math.Abs(diff.X) > 3 || Math.Abs(diff.Y) > 3)
        {
            _isPointerDown = false;
            
            var data = new Avalonia.Input.DataObject();
            data.Set("FmOperatorViewModel", _dragOperator);

            await Avalonia.Input.DragDrop.DoDragDrop(e, data, Avalonia.Input.DragDropEffects.Copy);
        }
    }

    private void OperatorPanel_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        if (_isPointerDown && _dragOperator != null && sender is Border border)
        {
            _isPointerDown = false;
            
            bool isShift = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift);

            if (isShift)
            {
                _dragOperator.IsSelected = !_dragOperator.IsSelected;
            }
            else
            {
                var allOps = new[] { ViewModel.Op1, ViewModel.Op2, ViewModel.Op3, ViewModel.Op4 };
                var selectedCount = System.Linq.Enumerable.Count(allOps, o => o.IsSelected);

                if (selectedCount == 1 && _dragOperator.IsSelected)
                {
                    _dragOperator.IsSelected = false; // toggle off
                }
                else
                {
                    foreach (var op in allOps)
                    {
                        op.IsSelected = (op == _dragOperator);
                    }
                }
            }

            e.Handled = true;
        }
        _dragOperator = null;
        _isPointerDown = false;
    }

    private void OperatorPanel_Drop(object? sender, Avalonia.Input.DragEventArgs e)
    {
        if (sender is Border border && border.DataContext is FmOperatorViewModel targetOp)
        {
            if (e.Data.Contains("FmOperatorViewModel"))
            {
                var sourceOp = e.Data.Get("FmOperatorViewModel") as FmOperatorViewModel;
                if (sourceOp != null && sourceOp != targetOp)
                {
                    targetOp.FromClipboardString(sourceOp.ToClipboardString());
                    e.Handled = true;
                }
            }
        }
    }
}
