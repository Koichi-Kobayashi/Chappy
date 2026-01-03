#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Chappy.Wpf.Controls.ContextMenu;

/// <summary>
/// Windows 11風のコンテキストメニュー用ポップアップ
/// </summary>
public class Win11ContextMenuPopup : Popup
{
    /// <summary>
    /// Win11ContextMenuPopupのインスタンスを初期化する
    /// </summary>
    public Win11ContextMenuPopup()
    {
        AllowsTransparency = true;
        StaysOpen = false;
        Placement = PlacementMode.AbsolutePoint;
    }

}
