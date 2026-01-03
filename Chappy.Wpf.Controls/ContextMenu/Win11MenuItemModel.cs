#nullable enable
using System.Collections.Generic;
using System.Windows.Input;

namespace Chappy.Wpf.Controls.ContextMenu;

/// <summary>
/// Windows 11風コンテキストメニューの項目モデル
/// </summary>
public sealed class Win11MenuItemModel
{
    /// <summary>メニュー項目のテキスト</summary>
    public string? Text { get; set; }
    /// <summary>入力ジェスチャー（ショートカットキー）</summary>
    public string? Gesture { get; set; }
    /// <summary>実行するコマンド</summary>
    public ICommand? Command { get; set; }

    /// <summary>チェック状態</summary>
    public bool IsChecked { get; set; }
    /// <summary>有効/無効</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>サブメニューの子項目</summary>
    public List<object> Children { get; } = new();

    // ===== 互換層（Win11ContextMenuView.cs 用）=====
    /// <summary>
    /// セパレータ用のWin11MenuItemModelを作成する
    /// </summary>
    /// <returns>セパレータ用のモデル</returns>
    public static Win11MenuItemModel Sep() => new Win11MenuItemModel { Text = null };

    /// <summary>サブメニューがあるかどうか</summary>
    public bool HasSubmenu { get; set; }
}
