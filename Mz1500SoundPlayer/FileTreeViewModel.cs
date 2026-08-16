using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Mz1500SoundPlayer;

public class FileTreeViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public ObservableCollection<FileTreeNodeViewModel> Nodes { get; } = new();

    private string _rootPath = "";
    public string RootPath
    {
        get => _rootPath;
        set { _rootPath = value; OnPropertyChanged(); }
    }

    private string _searchPattern = "*.*";
    public string SearchPattern
    {
        get => _searchPattern;
        set { _searchPattern = value; OnPropertyChanged(); }
    }

    public FileTreeViewModel(string rootPath, string searchPattern)
    {
        _rootPath = rootPath;
        _searchPattern = searchPattern;
        Refresh();
    }

    public void Refresh()
    {
        var expandedPaths = new HashSet<string>();
        CaptureExpandedPaths(Nodes, expandedPaths);

        Nodes.Clear();
        if (!Directory.Exists(_rootPath))
        {
            try
            {
                Directory.CreateDirectory(_rootPath);
            }
            catch
            {
                return;
            }
        }
        
        LoadDirectory(_rootPath, Nodes, expandedPaths);
    }

    private void CaptureExpandedPaths(IEnumerable<FileTreeNodeViewModel> nodes, HashSet<string> expandedPaths)
    {
        foreach (var node in nodes)
        {
            if (node.IsExpanded)
            {
                expandedPaths.Add(node.FullPath);
            }
            CaptureExpandedPaths(node.Children, expandedPaths);
        }
    }

    private void LoadDirectory(string path, ObservableCollection<FileTreeNodeViewModel> target, HashSet<string> expandedPaths)
    {
        try
        {
            var dirs = Directory.GetDirectories(path).OrderBy(d => d).ToArray();
            var files = Directory.GetFiles(path, _searchPattern).OrderBy(f => f).ToArray();

            foreach (var dir in dirs)
            {
                var node = new FileTreeNodeViewModel
                {
                    Name = Path.GetFileName(dir),
                    FullPath = dir,
                    IsDirectory = true,
                    IsExpanded = expandedPaths.Contains(dir)
                };
                LoadDirectory(dir, node.Children, expandedPaths);
                target.Add(node);
            }

            foreach (var file in files)
            {
                target.Add(new FileTreeNodeViewModel
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    IsDirectory = false
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading directory: {ex.Message}");
        }
    }
}
