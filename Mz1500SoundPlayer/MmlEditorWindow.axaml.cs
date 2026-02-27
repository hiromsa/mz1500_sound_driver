using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Mz1500SoundPlayer.Sound;

namespace Mz1500SoundPlayer;

public partial class MmlEditorWindow : Window
{
    private string _originalText = "";
    private string _prefixContext = "";

    public string ResultText { get; private set; } = "";

    public MmlEditorWindow()
    {
        InitializeComponent();
    }

    public MmlEditorWindow(string originalText, string prefixContext) : this()
    {
        _originalText = originalText;
        _prefixContext = prefixContext;

        TxtOriginal.Text = _originalText;
        UpdatePreview();
    }

    private void UpdatePreview_Triggers(object? sender, RoutedEventArgs e)
    {
        // Use Post to ensure this executes AFTER all mutually exclusive UI elements settle their IsChecked states.
        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            UpdatePreview();
        }, Avalonia.Threading.DispatcherPriority.Input);
    }

    private void UpdatePreview()
    {
        if (TxtPreview == null) return;

        var options = new MmlEditorOptions();

        // Length
        if (RadLenNone.IsChecked == true) options.LengthMode = MmlEditorOptions.LengthEditMode.None;
        else if (RadLenRemove.IsChecked == true) options.LengthMode = MmlEditorOptions.LengthEditMode.RemoveAll;
        else if (RadLenUnify.IsChecked == true) 
        {
            options.LengthMode = MmlEditorOptions.LengthEditMode.Unify;
            options.UnifyLength = (int)(NumUnify.Value ?? 16);
            options.UseLCommandForUnify = ChkUseLCommand.IsChecked ?? false;
        }
        else if (RadLenInc.IsChecked == true)
        {
            options.LengthMode = MmlEditorOptions.LengthEditMode.Increment;
            options.IncrementLengthValue = (int)(NumInc.Value ?? 16);
        }
        else if (RadLenOptimize.IsChecked == true) options.LengthMode = MmlEditorOptions.LengthEditMode.Optimize;
        else if (RadLenExpand.IsChecked == true) options.LengthMode = MmlEditorOptions.LengthEditMode.Expand;

        // Pitch
        if (RadOctNone.IsChecked == true) options.OctaveMode = MmlEditorOptions.OctaveEditMode.None;
        else if (RadOctRelative.IsChecked == true) options.OctaveMode = MmlEditorOptions.OctaveEditMode.ToRelative;
        else if (RadOctAbsolute.IsChecked == true) options.OctaveMode = MmlEditorOptions.OctaveEditMode.ToAbsolute;

        options.TransposeAmount = (int)(NumTranspose.Value ?? 0);

        if (TxtNoteReplace != null && !string.IsNullOrWhiteSpace(TxtNoteReplace.Text))
        {
            var pairs = TxtNoteReplace.Text.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var kv = pair.Split('=', StringSplitOptions.RemoveEmptyEntries);
                if (kv.Length == 2)
                {
                    string k = kv[0].Trim();
                    string v = kv[1].Trim();
                    if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v))
                    {
                        options.NoteReplacements[k] = v;
                    }
                }
            }
        }

        try
        {
            ResultText = MmlTextTransformer.Transform(_originalText, _prefixContext, options);
            TxtPreview.Text = ResultText;
        }
        catch (Exception ex)
        {
            TxtPreview.Text = "Error formatting: " + ex.Message;
        }
    }

    private void Apply_Click(object? sender, RoutedEventArgs e)
    {
        Close(ResultText);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
