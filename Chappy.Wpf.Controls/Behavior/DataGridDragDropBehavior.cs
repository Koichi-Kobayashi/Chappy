#nullable enable
using Chappy.Wpf.Controls.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Chappy.Wpf.Controls.Behaviors;

public static class DataGridDragDropBehavior
{
    #region 添付プロパティ

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(DataGridDragDropBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject d, bool v) => d.SetValue(IsEnabledProperty, v);
    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);

    public static readonly DependencyProperty BuildPayloadProperty =
        DependencyProperty.RegisterAttached(
            "BuildPayload",
            typeof(Func<IList, IDataObject?>),
            typeof(DataGridDragDropBehavior));

    public static void SetBuildPayload(DependencyObject d, Func<IList, IDataObject?>? v) => d.SetValue(BuildPayloadProperty, v);
    public static Func<IList, IDataObject?>? GetBuildPayload(DependencyObject d)
        => (Func<IList, IDataObject?>?)d.GetValue(BuildPayloadProperty);

    public static readonly DependencyProperty DropHandlerProperty =
        DependencyProperty.RegisterAttached(
            "DropHandler",
            typeof(Action<IDataObject, object?>),
            typeof(DataGridDragDropBehavior));

    public static void SetDropHandler(DependencyObject d, Action<IDataObject, object?>? v) => d.SetValue(DropHandlerProperty, v);
    public static Action<IDataObject, object?>? GetDropHandler(DependencyObject d)
        => (Action<IDataObject, object?>?)d.GetValue(DropHandlerProperty);

    #endregion

    #region State

    private sealed class State
    {
        public Point MouseDownPos;
        public bool IsDragging;
        public DataGridRow? DragRow;
    }

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(State),
            typeof(DataGridDragDropBehavior),
            new PropertyMetadata(null));

    private static State GetState(System.Windows.Controls.DataGrid g)
    {
        var s = (State?)g.GetValue(StateProperty);
        if (s == null)
        {
            s = new State();
            g.SetValue(StateProperty, s);
        }
        return s;
    }

    #endregion

    #region 有効化

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not System.Windows.Controls.DataGrid grid) return;

        if ((bool)e.NewValue)
        {
            grid.PreviewMouseLeftButtonDown += OnMouseDown;
            grid.PreviewMouseMove += OnMouseMove;
            grid.Drop += OnDrop;
            grid.AllowDrop = true;
        }
        else
        {
            grid.PreviewMouseLeftButtonDown -= OnMouseDown;
            grid.PreviewMouseMove -= OnMouseMove;
            grid.Drop -= OnDrop;
        }
    }

    #endregion

    #region Mouse

    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;

        var s = GetState(grid);
        s.IsDragging = false;
        s.MouseDownPos = e.GetPosition(grid);

        s.DragRow = VisualTreeUtil.FindAncestor<DataGridRow>(
            e.OriginalSource as DependencyObject);
    }

    private static void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var s = GetState(grid);
        if (s.IsDragging || s.DragRow == null) return;

        var pos = e.GetPosition(grid);
        if (Math.Abs(pos.X - s.MouseDownPos.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - s.MouseDownPos.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        // ==== ドラッグ開始 ====
        s.IsDragging = true;

        // 矩形選択の枠をクリア
        BoxSelectBehavior.ClearSelection(grid);

        var items = grid.SelectedItems.Count > 0
            ? grid.SelectedItems
            : new List<object> { s.DragRow.Item };

        var build = GetBuildPayload(grid);
        var data = build?.Invoke(items);
        if (data == null) return;

        DragDrop.DoDragDrop(grid, data, DragDropEffects.Move);
        
        // ドラッグ操作完了後に矩形選択をクリア
        BoxSelectBehavior.ClearSelection(grid);
    }

    #endregion

    #region Drop

    private static void OnDrop(object sender, DragEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;

        var handler = GetDropHandler(grid);
        if (handler == null) return;

        var row = VisualTreeUtil.FindAncestor<DataGridRow>(
            e.OriginalSource as DependencyObject);

        handler(e.Data, row?.Item);
    }

    #endregion
}
