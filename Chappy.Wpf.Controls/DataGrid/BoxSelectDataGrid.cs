using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Chappy.Wpf.Controls.DataGrid;

public class BoxSelectDataGrid : System.Windows.Controls.DataGrid
{
    public static readonly DependencyProperty IsBoxSelectionEnabledProperty =
        DependencyProperty.Register(nameof(IsBoxSelectionEnabled), typeof(bool), typeof(BoxSelectDataGrid),
            new PropertyMetadata(true));

    public bool IsBoxSelectionEnabled
    {
        get => (bool)GetValue(IsBoxSelectionEnabledProperty);
        set => SetValue(IsBoxSelectionEnabledProperty, value);
    }

    // Drag state
    private Point? _dragStart;
    private bool _isDragging;

    // Visuals
    private AdornerLayer? _adornerLayer;
    private SelectionAdorner? _adorner;

    // Cached visual tree parts
    private ScrollViewer? _scrollViewer;
    private VirtualizingStackPanel? _itemsHost;

    // Selection state (rebuilt at drag start)
    private readonly HashSet<object> _baselineSelection = new(); // Ctrl-drag baseline (snapshot)
    private readonly HashSet<object> _selectedSet = new();       // current SelectedItems set (only used during drag)
    private bool _selectionBasePrepared;
    private bool _appendMode;

    // Range state
    private int _lastRangeStart = -1;
    private int _lastRangeEnd = -1;

    // Auto-scroll
    private const double AutoScrollMargin = 18; // px
    private const double AutoScrollStep = 20;   // px per tick

    // Throttle selection updates
    private readonly DispatcherTimer _selectionTimer;
    private bool _hasPendingSelection;
    private Rect _pendingRect;
    private ModifierKeys _pendingMods;

    public BoxSelectDataGrid()
    {
        // 60fps-ish throttle. Keeps UI smooth even if MouseMove is 200Hz.
        _selectionTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _selectionTimer.Tick += (_, __) =>
        {
            if (!_isDragging || !_hasPendingSelection) return;

            _hasPendingSelection = false;
            ApplySelectionByIndexRangeCore(_pendingRect, _pendingMods);

            // stop if nothing pending (saves idle cost)
            if (!_hasPendingSelection)
                _selectionTimer.Stop();
        };
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _scrollViewer = FindDescendant<ScrollViewer>(this);
        _itemsHost = FindDescendant<VirtualizingStackPanel>(this); // ItemsHost under DataGrid
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);

        if (!IsBoxSelectionEnabled) return;
        if (e.ClickCount != 1) return;

        // スクロールバー上の操作（Thumb/Track/RepeatButton 等）では矩形選択を開始しない
        var dep = e.OriginalSource as DependencyObject;
        if (dep != null && FindParent<ScrollBar>(dep) != null)
        {
            _dragStart = null;
            _isDragging = false;
            return;
        }

        // Do NOT kill normal click selection here.
        _dragStart = e.GetPosition(this);
        _isDragging = false;

        _selectionBasePrepared = false;
        _appendMode = false;

        _lastRangeStart = _lastRangeEnd = -1;
        _hasPendingSelection = false;
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);

        if (!IsBoxSelectionEnabled) return;
        if (_dragStart is null) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(this);

        if (!_isDragging)
        {
            if (Math.Abs(pos.X - _dragStart.Value.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _dragStart.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            // Drag start confirmed
            _isDragging = true;

            CaptureMouse();
            EnsureAdorner();

            // Snapshot selection baseline at drag start (Explorer-like)
            PrepareSelectionBaseline(Keyboard.Modifiers);

            // We started a box drag, suppress further default processing.
            e.Handled = true;
        }

        // Update rectangle visual immediately
        var rect = MakeRect(_dragStart.Value, pos);
        _adorner?.Update(rect);

        // Auto scroll
        TryAutoScroll(pos);

        // Throttled selection apply
        _pendingRect = rect;
        _pendingMods = Keyboard.Modifiers;
        _hasPendingSelection = true;
        if (!_selectionTimer.IsEnabled)
            _selectionTimer.Start();
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (!IsBoxSelectionEnabled) return;

        bool wasDragging = _isDragging;

        CleanupDrag();

        // Only suppress click-up if we actually box-dragged.
        if (wasDragging)
            e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);

        if (_isDragging || _dragStart != null)
            CleanupDrag();
    }

    protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseRightButtonDown(e);

        // ignore during box dragging
        if (_isDragging) return;

        var dep = InputHitTest(e.GetPosition(this)) as DependencyObject;
        if (dep == null) return;

        var row = FindParent<System.Windows.Controls.DataGridRow>(dep);
        if (row == null) return;

        var item = row.Item;

        // If multi-select and right-click inside it => keep selection (Explorer-like)
        if (SelectedItems.Count > 1 && SelectedItems.Contains(item))
            return;

        SelectedItems.Clear();
        SelectedItem = item;
        row.Focus();
    }

    private void CleanupDrag()
    {
        try { ReleaseMouseCapture(); } catch { /* ignore */ }

        _dragStart = null;
        _isDragging = false;

        _selectionTimer.Stop();
        _hasPendingSelection = false;

        _lastRangeStart = _lastRangeEnd = -1;
        _selectionBasePrepared = false;

        _baselineSelection.Clear();
        _selectedSet.Clear();

        RemoveAdorner();
    }

    private void PrepareSelectionBaseline(ModifierKeys mods)
    {
        _baselineSelection.Clear();
        _selectedSet.Clear();

        _appendMode = mods.HasFlag(ModifierKeys.Control);

        if (_appendMode)
        {
            foreach (var obj in SelectedItems)
            {
                if (obj != null)
                    _baselineSelection.Add(obj);
            }
        }

        // Build selection base ONCE (avoid Clear/Add every MouseMove)
        SelectedItems.Clear();
        _selectedSet.Clear();

        if (_appendMode)
        {
            foreach (var obj in _baselineSelection)
            {
                SelectedItems.Add(obj);
                _selectedSet.Add(obj);
            }
        }

        _selectionBasePrepared = true;
        _lastRangeStart = _lastRangeEnd = -1;
    }

    // =========================
    // Fast: rect -> index range
    // =========================
    private void ApplySelectionByIndexRangeCore(Rect selectionRect, ModifierKeys mods)
    {
        if (!_selectionBasePrepared) return;
        if (Items.Count == 0) return;

        // If user pressed/released Ctrl mid-drag, treat as fixed mode from drag start.
        // (Explorer does similar; keeps stable behavior.)
        _ = mods;

        double headerHeight = GetColumnHeaderHeight();

        // Convert to row-area Y (excluding header)
        double y1 = selectionRect.Top - headerHeight;
        double y2 = selectionRect.Bottom - headerHeight;

        double rh = GetRowHeightOrEstimate();
        if (rh <= 0.0) return;

        int firstVisibleIndex = EstimateFirstVisibleIndex(rh);

        int start = firstVisibleIndex + (int)Math.Floor(Math.Min(y1, y2) / rh);
        int end = firstVisibleIndex + (int)Math.Floor(Math.Max(y1, y2) / rh);

        start = Clamp(start, 0, Items.Count - 1);
        end = Clamp(end, 0, Items.Count - 1);
        if (end < start) (start, end) = (end, start);

        // No change => no work
        if (start == _lastRangeStart && end == _lastRangeEnd)
            return;

        // Diff update (no more Clear/Add-all every frame)
        if (_lastRangeStart != -1 && _lastRangeEnd != -1)
        {
            // Remove items that were in old range but not in new range
            for (int i = _lastRangeStart; i <= _lastRangeEnd; i++)
            {
                if (i >= start && i <= end) continue;

                var item = Items[i];
                if (item == null) continue;

                // never remove baseline in append mode
                if (_appendMode && _baselineSelection.Contains(item)) continue;

                if (_selectedSet.Remove(item))
                    SelectedItems.Remove(item);
            }

            // Add items that are in new range but were not in old range
            for (int i = start; i <= end; i++)
            {
                if (i >= _lastRangeStart && i <= _lastRangeEnd) continue;

                var item = Items[i];
                if (item == null) continue;

                if (_selectedSet.Add(item))
                    SelectedItems.Add(item);
            }
        }
        else
        {
            // First time range apply during this drag
            for (int i = start; i <= end; i++)
            {
                var item = Items[i];
                if (item == null) continue;

                if (_selectedSet.Add(item))
                    SelectedItems.Add(item);
            }
        }

        _lastRangeStart = start;
        _lastRangeEnd = end;

        // Optional: ensure focus/cell is valid (prevents "selection not visible" quirks in some templates)
        if (SelectedItems.Count > 0 && CurrentCell.Item == null)
        {
            var firstCol = Columns.FirstOrDefault();
            if (firstCol != null)
                CurrentCell = new System.Windows.Controls.DataGridCellInfo(Items[start], firstCol);
        }
    }

    private double GetRowHeightOrEstimate()
    {
        // Prefer RowHeight (fast + stable)
        if (!double.IsNaN(RowHeight) && RowHeight > 0)
            return RowHeight;

        // Fallback: use an existing generated row
        if (_itemsHost != null)
        {
            foreach (var child in _itemsHost.Children)
            {
                if (child is System.Windows.Controls.DataGridRow row && row.ActualHeight > 0)
                    return row.ActualHeight;
            }
        }

        // Last resort: try any container
        for (int i = 0; i < Math.Min(Items.Count, 128); i++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(i) is System.Windows.Controls.DataGridRow row && row.ActualHeight > 0)
                return row.ActualHeight;
        }

        return -1;
    }

    private int EstimateFirstVisibleIndex(double rowHeight)
    {
        // Critical optimization:
        // Do NOT scan Items.Count. Only look at generated rows (itemsHost children).
        if (_itemsHost != null)
        {
            for (int i = 0; i < _itemsHost.Children.Count; i++)
            {
                if (_itemsHost.Children[i] is not System.Windows.Controls.DataGridRow row)
                    continue;

                int idx = ItemContainerGenerator.IndexFromContainer(row);
                if (idx < 0) continue;

                double headerHeight = GetColumnHeaderHeight();
                var p = row.TranslatePoint(new Point(0, 0), this);
                double yInRowsArea = p.Y - headerHeight;

                int delta = (int)Math.Round(yInRowsArea / rowHeight);
                int first = idx - delta;

                return Clamp(first, 0, Items.Count - 1);
            }
        }

        // Fallback: if we cannot see any generated row, return 0.
        return 0;
    }

    private double GetColumnHeaderHeight()
    {
        var header = FindDescendant<DataGridColumnHeadersPresenter>(this);
        return header?.ActualHeight ?? 0;
    }

    private void TryAutoScroll(Point currentPos)
    {
        if (_scrollViewer == null) return;

        double headerHeight = GetColumnHeaderHeight();
        double y = currentPos.Y;

        double topEdge = headerHeight + AutoScrollMargin;
        double bottomEdge = ActualHeight - AutoScrollMargin;

        if (y < topEdge)
        {
            _scrollViewer.ScrollToVerticalOffset(Math.Max(0, _scrollViewer.VerticalOffset - AutoScrollStep));
        }
        else if (y > bottomEdge)
        {
            _scrollViewer.ScrollToVerticalOffset(_scrollViewer.VerticalOffset + AutoScrollStep);
        }
    }

    // =========================
    // Adorner
    // =========================
    private void EnsureAdorner()
    {
        if (_adorner != null) return;

        _adornerLayer = AdornerLayer.GetAdornerLayer(this);
        if (_adornerLayer == null) return;

        _adorner = new SelectionAdorner(this);
        _adornerLayer.Add(_adorner);
    }

    private void RemoveAdorner()
    {
        if (_adornerLayer != null && _adorner != null)
            _adornerLayer.Remove(_adorner);

        _adorner = null;
        _adornerLayer = null;
    }

    private static Rect MakeRect(Point a, Point b)
    {
        var x1 = Math.Min(a.X, b.X);
        var y1 = Math.Min(a.Y, b.Y);
        var x2 = Math.Max(a.X, b.X);
        var y2 = Math.Max(a.Y, b.Y);
        return new Rect(new Point(x1, y1), new Point(x2, y2));
    }

    private sealed class SelectionAdorner : Adorner
    {
        private Rect _rect;

        private static readonly SolidColorBrush FillBrush;
        private static readonly Pen BorderPen;

        static SelectionAdorner()
        {
            FillBrush = new SolidColorBrush(Color.FromArgb(60, 0, 120, 215));
            if (FillBrush.CanFreeze) FillBrush.Freeze();

            var borderBrush = new SolidColorBrush(Color.FromArgb(200, 0, 120, 215));
            if (borderBrush.CanFreeze) borderBrush.Freeze();

            BorderPen = new Pen(borderBrush, 1);
            if (BorderPen.CanFreeze) BorderPen.Freeze();
        }

        public SelectionAdorner(UIElement adornedElement) : base(adornedElement)
        {
            IsHitTestVisible = false;
        }

        public void Update(Rect rect)
        {
            _rect = rect;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(FillBrush, BorderPen, _rect);
        }
    }

    private static T? FindParent<T>(DependencyObject d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T t) return t;
            d = LogicalTreeHelper.GetParent(d) ?? VisualTreeHelper.GetParent(d);
        }
        return null;
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) return t;

            var found = FindDescendant<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
