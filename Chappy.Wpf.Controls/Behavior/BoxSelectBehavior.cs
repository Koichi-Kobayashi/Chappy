#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Chappy.Wpf.Controls.Util;

namespace Chappy.Wpf.Controls.Behaviors;

public static class BoxSelectBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(BoxSelectBehavior),
            new PropertyMetadata(false, OnChanged));

    public static void SetIsEnabled(DependencyObject d, bool v)
        => d.SetValue(IsEnabledProperty, v);

    public static bool GetIsEnabled(DependencyObject d)
        => (bool)d.GetValue(IsEnabledProperty);

    private sealed class State
    {
        public Point? DragStart;
        public bool IsDragging;
        public bool StartedOnRightEmptyArea;
        public AdornerLayer? Layer;
        public SelectionAdorner? Adorner;
    }

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(State),
            typeof(BoxSelectBehavior),
            new PropertyMetadata(null));

    private static State GetState(System.Windows.Controls.DataGrid g)
    {
        if (g.GetValue(StateProperty) is not State s)
        {
            s = new State();
            g.SetValue(StateProperty, s);
        }
        return s;
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not System.Windows.Controls.DataGrid grid) return;

        if ((bool)e.NewValue)
        {
            grid.PreviewMouseLeftButtonDown += OnDown;
            grid.PreviewMouseMove += OnMove;
            grid.PreviewMouseLeftButtonUp += OnUp;
            grid.MouseLeave += OnLeave;
        }
        else
        {
            grid.PreviewMouseLeftButtonDown -= OnDown;
            grid.PreviewMouseMove -= OnMove;
            grid.PreviewMouseLeftButtonUp -= OnUp;
            grid.MouseLeave -= OnLeave;
        }
    }

    private static void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;

        // スクロールバー上の操作（Thumb/Track/RepeatButton 等）では矩形選択を開始しない。
        // また、この場合に「余白クリック扱い」で選択解除もしない。
        if (VirtualTreeUtil.FindAncestor<ScrollBar>(e.OriginalSource as DependencyObject) != null)
            return;

        var pos = e.GetPosition(grid);
        bool onRow = VirtualTreeUtil.FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) != null;
        bool rightEmptyArea = IsRightEmptyArea(grid, pos);

        // 行上で、かつ右側余白ではないなら通常処理（行上からのドラッグは D&D を優先）
        if (onRow && !rightEmptyArea)
            return;

        var s = GetState(grid);
        s.DragStart = pos;
        s.IsDragging = false;
        s.StartedOnRightEmptyArea = rightEmptyArea;

        // 余白をクリックした場合は選択を解除
        grid.SelectedItems.Clear();
    }

    private static void OnMove(object sender, MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        var s = GetState(grid);
        if (s.DragStart == null || e.LeftButton != MouseButtonState.Pressed) return;

        // イベントが既に処理済みの場合は何もしない（DataGridDragDropBehaviorがドラッグを開始した場合）
        if (e.Handled) return;

        var pos = e.GetPosition(grid);
        
        // ドラッグ開始前のチェック：行上で開始された場合は矩形選択を無効化
        if (!s.IsDragging)
        {
            // 行上でマウスが動いている場合は矩形選択を無効化（行上でのドラッグ開始を優先）
            // ただし「右側余白」から開始した場合は矩形選択を優先する
            if (!s.StartedOnRightEmptyArea &&
                VirtualTreeUtil.FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            // ドラッグ距離が最小距離に達していない場合は何もしない
            if (Math.Abs(pos.X - s.DragStart.Value.X) <
                SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - s.DragStart.Value.Y) <
                SystemParameters.MinimumVerticalDragDistance)
                return;

            // ドラッグ開始を確定
            s.IsDragging = true;
            s.Layer = AdornerLayer.GetAdornerLayer(grid);
            if (s.Layer != null)
            {
                s.Adorner = new SelectionAdorner(grid);
                s.Layer.Add(s.Adorner);
            }
        }

        // 既にドラッグが開始されている場合は、行上を通過しても矩形選択を継続
        var selectionRect = new Rect(s.DragStart.Value, pos);
        s.Adorner?.Update(selectionRect);
        
        // 矩形選択の範囲内の行を選択
        UpdateSelection(grid, selectionRect);
    }

    private static void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        var s = GetState(grid);
        
        // ドラッグしていた場合は最終的な選択を確定
        if (s.DragStart != null && s.IsDragging)
        {
            var pos = e.GetPosition(grid);
            var selectionRect = new Rect(s.DragStart.Value, pos);
            UpdateSelection(grid, selectionRect);
        }
        // ドラッグしていない場合（余白の単純なクリック）は選択解除はOnDownで既に処理済み
        
        // ドラッグ状態をクリーンアップ
        ClearSelection(grid);
    }

    private static void OnLeave(object sender, MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        var s = GetState(grid);
        // ドラッグ中の場合のみクリア（通常のマウス移動ではクリアしない）
        if (s.IsDragging)
        {
            ClearSelection(grid);
        }
    }

    private static void UpdateSelection(System.Windows.Controls.DataGrid grid, Rect selectionRect)
    {
        var selectedItems = new HashSet<object>();
        
        // DataGridのすべての行を列挙
        for (int i = 0; i < grid.Items.Count; i++)
        {
            var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(i);
            if (row == null) continue;
            
            // 行のBoundsを取得
            var rowBounds = row.TransformToAncestor(grid).TransformBounds(
                new Rect(0, 0, row.ActualWidth, row.ActualHeight));
            
            // 矩形選択の範囲と交差するかチェック
            if (selectionRect.IntersectsWith(rowBounds))
            {
                selectedItems.Add(row.Item);
            }
        }
        
        // 選択を更新
        grid.SelectedItems.Clear();
        foreach (var item in selectedItems)
        {
            grid.SelectedItems.Add(item);
        }
    }

    public static void ClearSelection(System.Windows.Controls.DataGrid grid)
    {
        var s = GetState(grid);

        if (s.Layer != null && s.Adorner != null)
            s.Layer.Remove(s.Adorner);

        s.DragStart = null;
        s.IsDragging = false;
        s.StartedOnRightEmptyArea = false;
        s.Layer = null;
        s.Adorner = null;
    }

    private static bool IsRightEmptyArea(System.Windows.Controls.DataGrid grid, Point pos)
    {
        double columnsWidth = grid.Columns
            .Where(c => c.Visibility == Visibility.Visible)
            .Sum(c => c.ActualWidth);

        return pos.X > columnsWidth;
    }

    private sealed class SelectionAdorner : Adorner
    {
        private Rect _rect;

        public SelectionAdorner(UIElement adorned)
            : base(adorned)
        {
            IsHitTestVisible = false;
        }

        public void Update(Rect r)
        {
            _rect = r;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            var fill = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215));
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 120, 215)), 1);
            dc.DrawRectangle(fill, pen, _rect);
        }
    }
}
