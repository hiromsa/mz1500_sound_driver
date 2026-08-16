using System.ComponentModel;
using System.Runtime.CompilerServices;
using AvaloniaEdit.Document;

namespace Mz1500SoundPlayer;

public class MmlDocumentTab : INotifyPropertyChanged
{
    private string _filePath = "";
    public string FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath != value)
            {
                _filePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    private string _title = "untitled.mml";
    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty != value)
            {
                _isDirty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    public string DisplayTitle => (IsDirty ? "*" : "") + Title;

    public TextDocument Document { get; set; }

    public int CaretOffset { get; set; }

    public MmlDocumentTab()
    {
        Document = new TextDocument();
        Document.TextChanged += (s, e) => IsDirty = true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
