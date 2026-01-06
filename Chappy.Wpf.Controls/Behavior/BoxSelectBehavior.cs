#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        }
        else
        {
            grid.PreviewMouseLeftButtonDown -= OnDown;
            grid.PreviewMouseMove -= OnMove;
            grid.PreviewMouseLeftButtonUp -= OnUp;
        }
    }

    private static void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;

        // 行上なら通常処理
        if (VisualTreeUtil.FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) != null)
            return;

        var s = GetState(grid);
        s.DragStart = e.GetPosition(grid);
        s.IsDragging = false;
    }

    private static void OnMove(object sender, MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        var s = GetState(grid);
        if (s.DragStart == null || e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(grid);
        if (!s.IsDragging)
        {
            if (Math.Abs(pos.X - s.DragStart.Value.X) <
                SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - s.DragStart.Value.Y) <
                SystemParameters.MinimumVerticalDragDistance)
                return;

            s.IsDragging = true;
            s.Layer = AdornerLayer.GetAdornerLayer(grid);
            if (s.Layer != null)
            {
                s.Adorner = new SelectionAdorner(grid);
                s.Layer.Add(s.Adorner);
            }
        }

        s.Adorner?.Update(new Rect(s.DragStart.Value, pos));
    }

    private static void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        var s = GetState(grid);

        if (s.Layer != null && s.Adorner != null)
            s.Layer.Remove(s.Adorner);

        s.DragStart = null;
        s.IsDragging = false;
        s.Layer = null;
        s.Adorner = null;
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
