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

/// <summary>
/// DataGridにボックス選択機能を追加するビヘイビアクラス
/// </summary>
public static class BoxSelectBehavior
{
    /// <summary>
    /// ボックス選択機能の有効/無効を制御する依存プロパティ
    /// </summary>
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(BoxSelectBehavior),
            new PropertyMetadata(false, OnChanged));

    /// <summary>
    /// 指定された依存オブジェクトにボックス選択機能を有効にする
    /// </summary>
    /// <param name="d">対象の依存オブジェクト</param>
    /// <param name="v">有効にする場合はtrue、無効にする場合はfalse</param>
    public static void SetIsEnabled(DependencyObject d, bool v)
        => d.SetValue(IsEnabledProperty, v);

    /// <summary>
    /// 指定された依存オブジェクトのボックス選択機能の有効/無効状態を取得する
    /// </summary>
    /// <param name="d">対象の依存オブジェクト</param>
    /// <returns>有効な場合はtrue、無効な場合はfalse</returns>
    public static bool GetIsEnabled(DependencyObject d)
        => (bool)d.GetValue(IsEnabledProperty);

    // =========================
    // 内部状態（DataGridごと）
    // =========================
    /// <summary>
    /// DataGridごとの内部状態を保持するクラス
    /// </summary>
    private sealed class State
    {
        /// <summary>ドラッグ開始位置（DataGrid座標系）</summary>
        public Point? DragStart;
        /// <summary>現在ドラッグ中かどうか</summary>
        public bool IsDragging;
        /// <summary>最後に選択された範囲の開始インデックス</summary>
        public int LastStart = -1;
        /// <summary>最後に選択された範囲の終了インデックス</summary>
        public int LastEnd = -1;

        /// <summary>Ctrlキー押下時の基準となる選択項目の集合</summary>
        public HashSet<object> BaselineSelection = new();

        /// <summary>選択範囲を表示するためのAdornerLayer</summary>
        public AdornerLayer? AdornerLayer;
        /// <summary>選択範囲を表示するためのAdorner</summary>
        public SelectionAdorner? Adorner;
    }

    /// <summary>
    /// DataGridにStateを保持するための依存プロパティ
    /// </summary>
    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(State),
            typeof(BoxSelectBehavior),
            new PropertyMetadata(null));

    /// <summary>
    /// 指定されたDataGridのStateを取得する（存在しない場合は新規作成）
    /// </summary>
    /// <param name="g">対象のDataGrid</param>
    /// <returns>Stateインスタンス</returns>
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
    /// <summary>
    /// IsEnabledPropertyの変更時に呼ばれるコールバック
    /// イベントハンドラの登録/解除を行う
    /// </summary>
    /// <param name="d">変更された依存オブジェクト</param>
    /// <param name="e">変更イベントの引数</param>
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
    /// <summary>
    /// マウス左ボタン押下時のイベントハンドラ
    /// ドラッグ開始位置を記録し、現在の選択を基準として保存する
    /// </summary>
    /// <param name="sender">イベント送信元</param>
    /// <param name="e">マウスボタンイベントの引数</param>
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

    /// <summary>
    /// マウス移動時のイベントハンドラ
    /// ドラッグ距離が一定以上になったら選択範囲を更新する
    /// </summary>
    /// <param name="sender">イベント送信元</param>
    /// <param name="e">マウスイベントの引数</param>
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

    /// <summary>
    /// マウスキャプチャが失われた時のイベントハンドラ
    /// ドラッグ状態をリセットし、リソースをクリーンアップする
    /// </summary>
    /// <param name="sender">イベント送信元</param>
    /// <param name="e">マウスイベントの引数</param>
    private static void OnLostCapture(object sender, MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        Cleanup(grid, GetState(grid));
    }

    // =========================
    // 選択ロジック（高速）
    // =========================
    /// <summary>
    /// 指定された矩形範囲内の行を選択する
    /// Ctrlキーが押されている場合は既存の選択に追加する
    /// </summary>
    /// <param name="grid">対象のDataGrid</param>
    /// <param name="s">内部状態</param>
    /// <param name="rect">選択範囲の矩形</param>
    /// <param name="mods">押下されている修飾キー</param>
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
    /// <summary>
    /// 現在表示されている最初の行のインデックスを推定する
    /// </summary>
    /// <param name="grid">対象のDataGrid</param>
    /// <param name="rowHeight">1行の高さ</param>
    /// <returns>最初に表示されている行のインデックス</returns>
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

    /// <summary>
    /// 選択範囲を表示するためのAdornerを確保する
    /// </summary>
    /// <param name="grid">対象のDataGrid</param>
    /// <param name="s">内部状態</param>
    private static void EnsureAdorner(System.Windows.Controls.DataGrid grid, State s)
    {
        if (s.Adorner != null) return;

        s.AdornerLayer = AdornerLayer.GetAdornerLayer(grid);
        if (s.AdornerLayer == null) return;

        s.Adorner = new SelectionAdorner(grid);
        s.AdornerLayer.Add(s.Adorner);
    }

    /// <summary>
    /// ドラッグ状態をリセットし、Adornerなどのリソースをクリーンアップする
    /// </summary>
    /// <param name="grid">対象のDataGrid</param>
    /// <param name="s">内部状態</param>
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

    /// <summary>
    /// 2つの点から矩形を作成する
    /// </summary>
    /// <param name="a">1つ目の点</param>
    /// <param name="b">2つ目の点</param>
    /// <returns>2つの点を対角とする矩形</returns>
    private static Rect MakeRect(Point a, Point b)
    {
        return new Rect(
            new Point(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)),
            new Point(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));
    }

    /// <summary>
    /// ビジュアルツリーを再帰的に探索し、指定された型の子孫要素を検索する
    /// </summary>
    /// <typeparam name="T">検索する要素の型</typeparam>
    /// <param name="root">探索を開始するルート要素</param>
    /// <returns>見つかった要素、見つからない場合はnull</returns>
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
    /// <summary>
    /// 選択範囲を視覚的に表示するためのAdornerクラス
    /// </summary>
    private sealed class SelectionAdorner : Adorner
    {
        /// <summary>表示する選択範囲の矩形</summary>
        private Rect _rect;

        /// <summary>
        /// SelectionAdornerのインスタンスを初期化する
        /// </summary>
        /// <param name="adorned">装飾対象のUI要素</param>
        public SelectionAdorner(UIElement adorned)
            : base(adorned)
        {
            IsHitTestVisible = false;
        }

        /// <summary>
        /// 表示する選択範囲の矩形を更新する
        /// </summary>
        /// <param name="r">新しい選択範囲の矩形</param>
        public void Update(Rect r)
        {
            _rect = r;
            InvalidateVisual();
        }

        /// <summary>
        /// Adornerの描画処理
        /// </summary>
        /// <param name="dc">描画コンテキスト</param>
        protected override void OnRender(DrawingContext dc)
        {
            var fill = new SolidColorBrush(Color.FromArgb(60, 0, 120, 215));
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 120, 215)), 1);
            dc.DrawRectangle(fill, pen, _rect);
        }
    }
}
