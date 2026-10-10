using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using Chappy.Wpf.Controls.Util;

namespace Chappy.Wpf.Controls.Behaviors;

public static class BoxSelectionHitTest
{
    private static DependencyObject? Parent(DependencyObject element)
        => element is Visual ? VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element) :
            element is FrameworkContentElement content ? content.Parent : LogicalTreeHelper.GetParent(element);

    public static bool IsInteractiveSource(DependencyObject? source)
    {
        for (var element = source; element != null; element = Parent(element))
        {
            // Header内のサイズ変更Thumbも、ヘッダーの一部として判定する前に除外する。
            if (element is Thumb) return true;
            if (element is GridViewColumnHeader or DataGridColumnHeader or ScrollBar or ButtonBase or
                TextBoxBase or PasswordBox or ComboBox or MenuItem or GridViewHeaderRowPresenter or DataGridColumnHeadersPresenter)
                return true;
        }
        return false;
    }

    public static bool IsEmptyArea(ItemsControl owner, DependencyObject? source, Point position)
    {
        if (!IsBackgroundSource(owner, source)) return false;
        // マウスキャプチャでOriginalSourceが一覧自身になっても、実際の行・操作部品を空白扱いしない。
        if (!IsBackgroundSource(owner, owner.InputHitTest(position) as DependencyObject)) return false;

        // コンテナが無いだけでは空白としない。本文のviewport内であることも確認する。
        var viewer = VirtualTreeUtil.FindDescendant<ScrollViewer>(owner);
        var viewport = viewer?.Template?.FindName("PART_ScrollContentPresenter", viewer) as ScrollContentPresenter;
        if (viewport == null) return false;
        var origin = viewport.TranslatePoint(new Point(), owner);
        return new Rect(origin, viewport.RenderSize).Contains(position);
    }

    private static bool IsBackgroundSource(ItemsControl owner, DependencyObject? source)
    {
        if (source == null || IsInteractiveSource(source)) return false;
        var element = source;
        for (; element != null && element != owner; element = Parent(element))
            if (element is ListViewItem or DataGridRow or GroupItem) return false;
        return element == owner;
    }
}
