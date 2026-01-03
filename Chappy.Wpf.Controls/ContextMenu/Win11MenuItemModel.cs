#nullable enable
using System.Collections.Generic;
using System.Windows.Input;

namespace Chappy.Wpf.Controls.ContextMenu;

public sealed class Win11MenuItemModel
{
    public string? Text { get; set; }
    public string? Gesture { get; set; }
    public ICommand? Command { get; set; }

    public bool IsChecked { get; set; }
    public bool IsEnabled { get; set; } = true;

    // サブメニュー
    public List<object> Children { get; } = new();

    // ===== 互換層（Win11ContextMenuView.cs 用）=====
    public static Win11MenuItemModel Sep() => new Win11MenuItemModel { Text = null };

    public bool HasSubmenu { get; set; }
}
