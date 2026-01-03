#nullable enable
using System.Windows.Input;

namespace Chappy.Wpf.Controls.ContextMenu;

public sealed class FilesMenuCommands
{
    public ICommand? Cut { get; set; }
    public ICommand? Copy { get; set; }
    public ICommand? Paste { get; set; }
    public ICommand? Rename { get; set; }
    public ICommand? Share { get; set; }
    public ICommand? Delete { get; set; }
}
