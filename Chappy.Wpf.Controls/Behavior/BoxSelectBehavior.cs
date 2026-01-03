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

    // =========================
    // 内部状態（DataGridごと）
    // =========================
    private sealed class State
    {
        public Point? DragStart;
        public bool IsDragging;
        public int LastStart = -1;
        public int LastEnd = -1;

        public HashSet<object> BaselineSelection = new();

        public AdornerLayer? AdornerLayer;
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

    // =========================
    // 有効/無効
    // =========================
    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not System.Windows.Controls.DataGrid grid) return;

        if ((bool)e.NewValue)
        {
            grid.PreviewMouseLeftButtonDown += OnLeftDown;
            grid.PreviewMouseMove += OnMove;
            grid.PreviewMouseLeftButtonUp += OnLeftUp;
            grid.LostMouseCapture += OnLostCapture;
        }
        else
        {
            grid.PreviewMouseLeftButtonDown -= OnLeftDown;
            grid.PreviewMouseMove -= OnMove;
            grid.PreviewMouseLeftButtonUp -= OnLeftUp;
            grid.LostMouseCapture -= OnLostCapture;
        }
    }

    // =========================
    // Mouse handlers
    // =========================
    private static void OnLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        if (e.ClickCount != 1) return;

        var s = GetState(grid);

        // ★ここでは何も奪わない（通常クリックを生かす）
        s.DragStart = e.GetPosition(grid);
        s.IsDragging = false;

        s.BaselineSelection.Clear();
        foreach (var x in grid.SelectedItems.Cast<object>())
            s.BaselineSelection.Add(x);
    }

    private static void OnMove(object sender, MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;

        var s = GetState(grid);
        if (s.DragStart is null) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(grid);

        if (!s.IsDragging)
        {
            if (Math.Abs(pos.X - s.DragStart.Value.X) <
                    SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - s.DragStart.Value.Y) <
                    SystemParameters.MinimumVerticalDragDistance)
                return;

            // ★ドラッグ確定
            s.IsDragging = true;
            grid.CaptureMouse();
            EnsureAdorner(grid, s);
        }

        var rect = MakeRect(s.DragStart.Value, pos);
        s.Adorner?.Update(rect);

        ApplySelection(grid, s, rect, Keyboard.Modifiers);
        e.Handled = true;
    }

    private static void OnLeftUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;

        var s = GetState(grid);
        if (!s.IsDragging)
        {
            s.DragStart = null;
            return;
        }

        grid.ReleaseMouseCapture();
        Cleanup(grid, s);
        e.Handled = true;
    }

    private static void OnLostCapture(object sender, MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        Cleanup(grid, GetState(grid));
    }

    // =========================
    // 選択ロジック（高速）
    // =========================
    private static void ApplySelection(
        System.Windows.Controls.DataGrid grid,
        State s,
        Rect rect,
        ModifierKeys mods)
    {
        if (grid.Items.Count == 0) return;

        bool append = mods.HasFlag(ModifierKeys.Control);

        double rowHeight = grid.RowHeight;
        if (double.IsNaN(rowHeight) || rowHeight <= 0) return;

        double header = GetHeaderHeight(grid);

        double y1 = rect.Top - header;
        double y2 = rect.Bottom - header;

        int firstVisible = EstimateFirstVisibleIndex(grid, rowHeight);

        int start = firstVisible + (int)Math.Floor(Math.Min(y1, y2) / rowHeight);
        int end = firstVisible + (int)Math.Floor(Math.Max(y1, y2) / rowHeight);

        start = Math.Max(0, Math.Min(start, grid.Items.Count - 1));
        end = Math.Max(0, Math.Min(end, grid.Items.Count - 1));
        if (end < start) (start, end) = (end, start);

        if (start == s.LastStart && end == s.LastEnd) return;

        s.LastStart = start;
        s.LastEnd = end;

        grid.SelectedItems.Clear();
        if (append)
            foreach (var x in s.BaselineSelection)
                grid.SelectedItems.Add(x);

        for (int i = start; i <= end; i++)
        {
            var item = grid.Items[i];
            if (!grid.SelectedItems.Contains(item))
                grid.SelectedItems.Add(item);
        }
    }

    // =========================
    // 補助
    // =========================
    private static int EstimateFirstVisibleIndex(System.Windows.Controls.DataGrid grid, double rowHeight)
    {
        for (int i = 0; i < grid.Items.Count; i++)
        {
            if (grid.ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow row)
            {
                var p = row.TranslatePoint(new Point(0, 0), grid);
                double header = GetHeaderHeight(grid);
                int delta = (int)Math.Round((p.Y - header) / rowHeight);
                return Math.Max(0, i - delta);
            }
        }
        return 0;
    }

    private static double GetHeaderHeight(System.Windows.Controls.DataGrid grid)
    {
        var h = FindDescendant<DataGridColumnHeadersPresenter>(grid);
        return h?.ActualHeight ?? 0;
    }

    private static void EnsureAdorner(System.Windows.Controls.DataGrid grid, State s)
    {
        if (s.Adorner != null) return;

        s.AdornerLayer = AdornerLayer.GetAdornerLayer(grid);
        if (s.AdornerLayer == null) return;

        s.Adorner = new SelectionAdorner(grid);
        s.AdornerLayer.Add(s.Adorner);
    }

    private static void Cleanup(System.Windows.Controls.DataGrid grid, State s)
    {
        s.DragStart = null;
        s.IsDragging = false;
        s.LastStart = s.LastEnd = -1;

        if (s.AdornerLayer != null && s.Adorner != null)
            s.AdornerLayer.Remove(s.Adorner);

        s.Adorner = null;
        s.AdornerLayer = null;
    }

    private static Rect MakeRect(Point a, Point b)
    {
        return new Rect(
            new Point(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)),
            new Point(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            if (c is T t) return t;

            var r = FindDescendant<T>(c);
            if (r != null) return r;
        }
        return null;
    }

    // =========================
    // Adorner
    // =========================
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
            var fill = new SolidColorBrush(Color.FromArgb(60, 0, 120, 215));
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 120, 215)), 1);
            dc.DrawRectangle(fill, pen, _rect);
        }
    }
}
