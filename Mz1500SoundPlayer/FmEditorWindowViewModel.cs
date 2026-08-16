using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Mz1500SoundPlayer;

public class FmEditorWindowViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public FmVoiceLibraryViewModel Library { get; } = new();

    public ObservableCollection<FmEditorTabViewModel> Tabs { get; } = new();

    private FmEditorTabViewModel? _selectedTab;
    public FmEditorTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set { _selectedTab = value; OnPropertyChanged(); }
    }
}
