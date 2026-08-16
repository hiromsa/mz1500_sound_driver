using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Mz1500SoundPlayer;

public partial class FmEditorWindow : Window
{
    public FmEditorWindowViewModel ViewModel { get; }
    public Action<string>? OnApply { get; set; }
    private int _fmNumber = 1;

    public FmEditorWindow()
    {
        InitializeComponent();
        ViewModel = new FmEditorWindowViewModel();
        DataContext = ViewModel;
    }

    public FmEditorWindow(int fmNumber, string mml) : this()
    {
        _fmNumber = fmNumber;
        var tab = new FmEditorTabViewModel
        {
            Title = $"FM{fmNumber} (トラッカー)",
            FilePath = null
        };
        tab.Editor.ParseMml(mml);
        tab.IsDirty = false;
        ViewModel.Tabs.Add(tab);
        ViewModel.SelectedTab = tab;
    }

    private void OpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        string path = Path.GetFullPath(ViewModel.Library.RootPath);
        if (!Directory.Exists(path)) return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo("explorer", path) { UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", path);
        }
        else
        {
            Process.Start("xdg-open", path);
        }
    }

    private void NewFile_Click(object? sender, RoutedEventArgs e)
    {
        var tab = new FmEditorTabViewModel
        {
            Title = "Untitled",
            FilePath = null
        };
        ViewModel.Tabs.Add(tab);
        ViewModel.SelectedTab = tab;
    }

    private void TreeView_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Control c && c.DataContext is FmVoiceNodeViewModel node && !node.IsDirectory)
        {
            OpenFile(node.FullPath);
        }
    }

    private void TreeNode_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2 && sender is Control c && c.DataContext is FmVoiceNodeViewModel node && !node.IsDirectory)
        {
            OpenFile(node.FullPath);
            e.Handled = true;
        }
    }

    private void OpenFile(string path)
    {
        // Check if already open
        var existingTab = ViewModel.Tabs.FirstOrDefault(t => t.FilePath == path);
        if (existingTab != null)
        {
            ViewModel.SelectedTab = existingTab;
            return;
        }

        try
        {
            string content = File.ReadAllText(path);
            var tab = new FmEditorTabViewModel
            {
                Title = Path.GetFileName(path),
                FilePath = path
            };
            tab.Editor.ParseMml(content);
            tab.IsDirty = false;
            
            ViewModel.Tabs.Add(tab);
            ViewModel.SelectedTab = tab;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading file: {ex.Message}");
        }
    }

    private async void CloseTab_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is FmEditorTabViewModel tab)
        {
            if (tab.IsDirty)
            {
                // Unsaved changes dialog
                var result = await ShowMessageDialog("未保存の変更", $"'{tab.Title}' には未保存の変更があります。保存しますか？", true);
                if (result == "Save")
                {
                    if (string.IsNullOrEmpty(tab.FilePath))
                    {
                        bool saved = await SaveAs(tab);
                        if (!saved) return;
                    }
                    else
                    {
                        SaveFile(tab);
                    }
                }
                else if (result == "Cancel")
                {
                    return;
                }
            }
            
            ViewModel.Tabs.Remove(tab);
        }
    }

    private void Apply_Click(object? sender, RoutedEventArgs e)
    {
        var tab = ViewModel.SelectedTab;
        if (tab == null) return;
        
        string mml = tab.Editor.ToMml(_fmNumber);
        OnApply?.Invoke(mml);
    }

    private async void CopyMml_Click(object? sender, RoutedEventArgs e)
    {
        var tab = ViewModel.SelectedTab;
        if (tab == null) return;
        
        string mml = tab.Editor.ToMml(1);
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(mml);
        }
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        var tab = ViewModel.SelectedTab;
        if (tab == null) return;

        if (string.IsNullOrEmpty(tab.FilePath))
        {
            await SaveAs(tab);
        }
        else
        {
            SaveFile(tab);
        }
    }

    private async void SaveAs_Click(object? sender, RoutedEventArgs e)
    {
        var tab = ViewModel.SelectedTab;
        if (tab == null) return;

        await SaveAs(tab);
    }

    private async Task<bool> SaveAs(FmEditorTabViewModel tab)
    {
        var dialog = new SaveFileDialog
        {
            Title = "別名で保存",
            DefaultExtension = "mml",
            InitialFileName = tab.Title == "Untitled" ? "new_voice.mml" : tab.Title,
            Directory = Path.GetFullPath(ViewModel.Library.RootPath)
        };
        dialog.Filters.Add(new FileDialogFilter { Name = "MML Files", Extensions = { "mml" } });

        var result = await dialog.ShowAsync(this);
        if (!string.IsNullOrEmpty(result))
        {
            tab.FilePath = result;
            tab.Title = Path.GetFileName(result);
            SaveFile(tab);
            ViewModel.Library.Refresh();
            return true;
        }
        return false;
    }

    private void SaveFile(FmEditorTabViewModel tab)
    {
        if (string.IsNullOrEmpty(tab.FilePath)) return;

        try
        {
            string mml = tab.Editor.ToMml(1);
            File.WriteAllText(tab.FilePath, mml);
            tab.IsDirty = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving file: {ex.Message}");
        }
    }

    private async Task<string> ShowMessageDialog(string title, string message, bool showCancel)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 20 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });

        var btnPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 10 };
        
        var tcs = new TaskCompletionSource<string>();

        var btnSave = new Button { Content = "保存 (Save)" };
        btnSave.Click += (_, _) => { tcs.SetResult("Save"); dialog.Close(); };
        btnPanel.Children.Add(btnSave);

        var btnDiscard = new Button { Content = "破棄 (Discard)" };
        btnDiscard.Click += (_, _) => { tcs.SetResult("Discard"); dialog.Close(); };
        btnPanel.Children.Add(btnDiscard);

        if (showCancel)
        {
            var btnCancel = new Button { Content = "キャンセル (Cancel)" };
            btnCancel.Click += (_, _) => { tcs.SetResult("Cancel"); dialog.Close(); };
            btnPanel.Children.Add(btnCancel);
        }

        panel.Children.Add(btnPanel);
        dialog.Content = panel;

        await dialog.ShowDialog(this);
        return await tcs.Task;
    }
}
