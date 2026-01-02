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

public class Win11ContextMenuPopup : Popup
{
    public Win11ContextMenuPopup()
    {
        AllowsTransparency = true;
        StaysOpen = false;
        Placement = PlacementMode.AbsolutePoint;
    }

}
