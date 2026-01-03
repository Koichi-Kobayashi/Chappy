using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

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

    // ドラッグ開始位置（DataGrid座標）
    private Point? _dragStart;
    private bool _isDragging;

    // 表示用
    private AdornerLayer? _adornerLayer;
    private SelectionAdorner? _adorner;

    // 選択の内部状態
    private int _lastRangeStart = -1;
    private int _lastRangeEnd = -1;
    private readonly HashSet<object> _baselineSelection = new(); // Ctrlドラッグ時の「元の選択」

    // スクロール
    private ScrollViewer? _scrollViewer;
    private const double AutoScrollMargin = 18;   // 上下端から何pxでオートスクロール開始
    private const double AutoScrollStep = 20;     // 1 tick のスクロール量（px相当）

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _scrollViewer = FindDescendant<ScrollViewer>(this);
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);

        if (!IsBoxSelectionEnabled) return;
        if (e.ClickCount != 1) return;

        // ★ここでは通常クリックを殺さない
        _dragStart = e.GetPosition(this);
        _isDragging = false;

        // ★Captureもしない
        // ★Handledもしない
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

            _isDragging = true;

            // ★ドラッグ開始が確定してから掴む
            CaptureMouse();
            EnsureAdorner();

            // ★ここで初めてイベントを抑止したいなら抑止
            e.Handled = true;
        }

        // 矩形更新
        var rect = MakeRect(_dragStart.Value, pos);
        _adorner?.Update(rect);

        // オートスクロール（上下端）
        TryAutoScroll(pos);

        // 選択反映（超高速：index 計算）
        ApplySelectionByIndexRange(rect, Keyboard.Modifiers);
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (!IsBoxSelectionEnabled) return;

        ReleaseMouseCapture();
        _dragStart = null;
        _isDragging = false;
        _lastRangeStart = _lastRangeEnd = -1;

        RemoveAdorner();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);

        // ドラッグ状態を強制リセット（Adornerも消す）
        _dragStart = null;
        _isDragging = false;
        RemoveAdorner();
    }

    protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseRightButtonDown(e);

        // 矩形選択ドラッグ中は右クリック無視（またはドラッグ終了させる）
        if (_isDragging) return;

        var dep = InputHitTest(e.GetPosition(this)) as DependencyObject;
        if (dep == null) return;

        var row = FindParent<DataGridRow>(dep);
        if (row == null) return;

        var item = row.Item;

        // 複数選択中で、その中を右クリックしたら選択維持（Explorer風）
        if (SelectedItems.Count > 1 && SelectedItems.Contains(item))
            return;

        SelectedItems.Clear();
        SelectedItem = item;
        row.Focus();

        // ★ここで e.Handled = true は基本やらない
    }

    private static T? FindParent<T>(DependencyObject d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T t) return t;
            d = LogicalTreeHelper.GetParent(d) ?? System.Windows.Media.VisualTreeHelper.GetParent(d);
        }
        return null;
    }

    // =========================
    // 高速：矩形→インデックス範囲
    // =========================
    private void ApplySelectionByIndexRange(Rect selectionRect, ModifierKeys mods)
    {
        if (Items.Count == 0) return;

        bool append = mods.HasFlag(ModifierKeys.Control);

        // DataGrid 内の “行領域” の上端（ヘッダー分を除外）
        double headerHeight = GetColumnHeaderHeight();
        double y1 = selectionRect.Top - headerHeight;
        double y2 = selectionRect.Bottom - headerHeight;

        // スクロール位置（CanContentScroll=Trueでも、ScrollViewer.VerticalOffsetは “行” ではなく内容座標のこともある）
        // ここでは「表示領域座標→行インデックス」へ変換するため、ViewPort内のYを使う方針にする。
        // つまり「見えている範囲内のY」から index を作る。
        // ※ RowHeight 固定前提
        double rh = GetFixedRowHeight();
        if (rh <= 0.0) return;

        // いま画面に見えている先頭行 index を推定
        int firstVisibleIndex = EstimateFirstVisibleIndex(rh);

        // 表示領域内の y を行番号に変換
        // y=0 が firstVisibleIndex の行の上端
        int start = firstVisibleIndex + (int)Math.Floor(Math.Min(y1, y2) / rh);
        int end = firstVisibleIndex + (int)Math.Floor(Math.Max(y1, y2) / rh);

        // 範囲をクランプ
        start = Math.Max(0, Math.Min(start, Items.Count - 1));
        end = Math.Max(0, Math.Min(end, Items.Count - 1));
        if (end < start) (start, end) = (end, start);

        // 変化なしなら何もしない（ドラッグ中の無駄な更新を抑制）
        if (start == _lastRangeStart && end == _lastRangeEnd && append == (mods.HasFlag(ModifierKeys.Control)))
            return;

        _lastRangeStart = start;
        _lastRangeEnd = end;

        // 置き換え選択：矩形の範囲だけを選択
        // 追加選択：元の選択 + 矩形の範囲
        SelectedItems.Clear();
        if (append)
        {
            foreach (var obj in _baselineSelection) SelectedItems.Add(obj);
        }

        for (int i = start; i <= end; i++)
        {
            var item = Items[i];
            if (!SelectedItems.Contains(item))
                SelectedItems.Add(item);
        }

        // “選択が見えない”問題を避けるなら、フォーカスだけ軽く入れる（任意）
        if (SelectedItems.Count > 0 && CurrentCell.Item == null)
            CurrentCell = new System.Windows.Controls.DataGridCellInfo(Items[start], Columns.FirstOrDefault());
    }

    private double GetFixedRowHeight()
    {
        // RowHeight > 0 を固定値として使う（NaNの場合はダメ）
        if (!double.IsNaN(RowHeight) && RowHeight > 0) return RowHeight;

        // RowStyle で Height を固定しているケースはここでは取れないので、
        // 原則 RowHeight を指定してください（推奨）。
        return -1;
    }

    private int EstimateFirstVisibleIndex(double rowHeight)
    {
        // 仮想化 + Recycling だと、ContainerFromIndex(0) が null のことが多いので
        // “いま生成されている一番上の行”から推定する。
        // 生成されていない場合は 0 とみなす。

        // まず、生成済み row を探す（見えてる範囲のどこか）
        for (int i = 0; i < Items.Count; i++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(i) is System.Windows.Controls.DataGridRow row)
            {
                // row の上端の DataGrid 座標
                var p = row.TranslatePoint(new Point(0, 0), this);
                double headerHeight = GetColumnHeaderHeight();

                // 表示領域（行領域）の y=0 を基準に、現在の row が何行目か推定
                // p.Y - headerHeight が row の “表示領域内Y”
                double yInRowsArea = p.Y - headerHeight;
                int delta = (int)Math.Round(yInRowsArea / rowHeight);

                int first = i - delta;
                if (first < 0) first = 0;
                if (first >= Items.Count) first = Items.Count - 1;
                return first;
            }
        }

        return 0;
    }

    private double GetColumnHeaderHeight()
    {
        var header = FindDescendant<DataGridColumnHeadersPresenter>(this);
        return header?.ActualHeight ?? 0;
    }

    // =========================
    // オートスクロール
    // =========================
    private void TryAutoScroll(Point currentPos)
    {
        if (_scrollViewer == null) return;

        double headerHeight = GetColumnHeaderHeight();
        double y = currentPos.Y;

        // 行領域の上下端（ヘッダー除外）
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
            var fill = new SolidColorBrush(Color.FromArgb(60, 0, 120, 215));
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 120, 215)), 1);
            dc.DrawRectangle(fill, pen, _rect);
        }
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
}
