#nullable enable
using System.Windows.Input;

namespace Chappy.Wpf.Controls.ContextMenu;

/// <summary>
/// Files風のコンテキストメニュークラス
/// </summary>
public class FilesLikeContextMenu : System.Windows.Controls.ContextMenu   // ★ 完全修飾名
{
    /// <summary>切り取りコマンド</summary>
    public ICommand? CutCommand { get; set; }
    /// <summary>コピーコマンド</summary>
    public ICommand? CopyCommand { get; set; }
    /// <summary>貼り付けコマンド</summary>
    public ICommand? PasteCommand { get; set; }
    /// <summary>名前変更コマンド</summary>
    public ICommand? RenameCommand { get; set; }
    /// <summary>共有コマンド</summary>
    public ICommand? ShareCommand { get; set; }
    /// <summary>削除コマンド</summary>
    public ICommand? DeleteCommand { get; set; }
}
