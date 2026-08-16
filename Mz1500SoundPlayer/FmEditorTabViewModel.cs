using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Mz1500SoundPlayer;

public class FmEditorTabViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private string _title = "Untitled";
    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayTitle)); }
    }

    private string? _filePath;
    public string? FilePath
    {
        get => _filePath;
        set { _filePath = value; OnPropertyChanged(); }
    }

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        set { _isDirty = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayTitle)); }
    }

    public string DisplayTitle => IsDirty ? Title + " *" : Title;

    public FmEditorViewModel Editor { get; }

    public FmEditorTabViewModel()
    {
        Editor = new FmEditorViewModel();
        Editor.PropertyChanged += (s, e) => {
            if (e.PropertyName == nameof(Editor.IsDirty) && Editor.IsDirty)
            {
                IsDirty = true;
            }
        };
    }
}
