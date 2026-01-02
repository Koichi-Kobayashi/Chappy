using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace Chappy.Wpf.Controls.Test;

public enum EntryKind { File, Folder }

public class FileEntry : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string CurrentFolder { get; set; } = "";
    public EntryKind Kind { get; set; } // File / Folder

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _currentFolderPath;
    public string CurrentFolderPath
    {
        get => _currentFolderPath;
        set
        {
            if (_currentFolderPath != value)
            {
                _currentFolderPath = value;
                OnPropertyChanged();
            }
        }
    }
}
