#nullable enable
using System.Windows.Input;

namespace Chappy.Wpf.Controls.ContextMenu;

/// <summary>
/// Files風メニューのコマンドを保持するクラス
/// </summary>
public sealed class FilesMenuCommands
{
    /// <summary>切り取りコマンド</summary>
    public ICommand? Cut { get; set; }
    /// <summary>コピーコマンド</summary>
    public ICommand? Copy { get; set; }
    /// <summary>貼り付けコマンド</summary>
    public ICommand? Paste { get; set; }
    /// <summary>名前変更コマンド</summary>
    public ICommand? Rename { get; set; }
    /// <summary>共有コマンド</summary>
    public ICommand? Share { get; set; }
    /// <summary>削除コマンド</summary>
    public ICommand? Delete { get; set; }
}
