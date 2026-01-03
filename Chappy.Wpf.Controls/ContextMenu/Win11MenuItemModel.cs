#nullable enable
using System.Windows.Input;

namespace Chappy.Wpf.Controls.ContextMenu;

public sealed class Win11MenuItemModel
{
    public string? Text { get; set; }
    public string? Gesture { get; set; }
    public bool IsSeparator { get; set; }
    public bool HasSubmenu { get; set; }
    public ICommand? Command { get; set; }

    public static Win11MenuItemModel Sep() => new Win11MenuItemModel { IsSeparator = true };
}
