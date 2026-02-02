#nullable enable
using Chappy.Wpf.Controls.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
        public long MouseDownTimestamp;
        public bool IsDragging;
        public bool CancelDragRequested;
        public bool SuppressDragUntilLeftUp;
        public long DragStartTimestamp;
        public bool StartedOnRightEmptyArea;
        public DataGridRow? DragRow;
        public DataGridRow? HoverRow;
        public DataGridRow? HoverCandidateRow;
        public long HoverCandidateTimestamp;
        // Background は IsSelected 等のスタイルトリガーで動的に変わるため、
        // 「見えている Brush」を保存して Set で戻すとローカル値として固定化され、選択っぽい見た目が残ることがある。
        // なのでローカル値（UnsetValue含む）を保存し、復元時は ClearValue でトリガー制御に戻す。
        public object? HoverOriginalBackgroundLocalValue; // Control.BackgroundProperty のローカル値（UnsetValue含む）
        public List<DataGridRow>? DraggedRows; // ドラッグ中の複数選択された行
        public Dictionary<DataGridRow, object?>? OriginalBackgroundLocalValues; // 各行の Background ローカル値（UnsetValue含む）
        public List<object>? SavedSelectedItems; // マウスダウン時の選択されたアイテムを保存

        // Explorer 互換の「複数選択からのドラッグ」用。
        // WPF DataGrid は MouseDown の時点で選択を単一化しがちなので、
        // 複数選択中に "既に選択されている行" を押した場合は既定の選択変更を抑止し、
        // ドラッグにならなかった（クリック確定）時だけ単一選択に落とす。
        public bool SuppressSelectionOnMouseDown;
        public object? ClickedSelectedItem;
    }

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(State),
            typeof(DataGridDragDropBehavior),
            new PropertyMetadata(null));

    private static int s_inputHookRefCount;
    private static long s_lastRightButtonDownTimestamp;
    private static readonly PreProcessInputEventHandler s_preProcessInputHandler = OnPreProcessInput;

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
            if (s_inputHookRefCount++ == 0)
                InputManager.Current.PreProcessInput += s_preProcessInputHandler;
            grid.PreviewMouseLeftButtonDown += OnMouseDown;
            grid.PreviewMouseLeftButtonUp += OnMouseUp;
            grid.PreviewMouseRightButtonDown += OnRightMouseDown;
            grid.PreviewMouseMove += OnMouseMove;
            grid.QueryContinueDrag += OnQueryContinueDrag;
            grid.DragEnter += OnDragEnter;
            grid.DragOver += OnDragOver;
            grid.DragLeave += OnDragLeave;
            grid.Drop += OnDrop;
            grid.AllowDrop = true;
        }
        else
        {
            grid.PreviewMouseLeftButtonDown -= OnMouseDown;
            grid.PreviewMouseLeftButtonUp -= OnMouseUp;
            grid.PreviewMouseRightButtonDown -= OnRightMouseDown;
            grid.PreviewMouseMove -= OnMouseMove;
            grid.QueryContinueDrag -= OnQueryContinueDrag;
            grid.DragEnter -= OnDragEnter;
            grid.DragOver -= OnDragOver;
            grid.DragLeave -= OnDragLeave;
            grid.Drop -= OnDrop;
            if (--s_inputHookRefCount == 0)
                InputManager.Current.PreProcessInput -= s_preProcessInputHandler;
        }
    }

    #endregion

    #region Mouse

    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;

        var s = GetState(grid);

        // 右クリックや Esc キャンセル後に変な状態が残らないよう、ここで一旦クリア。
        // （ドラッグ中の OnMouseDown は下のガードで弾く）
        s.SuppressSelectionOnMouseDown = false;
        s.ClickedSelectedItem = null;
        
        // ドラッグ中またはドラッグが完了していない場合は、SavedSelectedItemsを上書きしない
        if (s.IsDragging)
        {
            return;
        }
        
        s.IsDragging = false;
        s.MouseDownPos = e.GetPosition(grid);
        s.MouseDownTimestamp = Stopwatch.GetTimestamp();

        s.DragRow = VirtualTreeUtil.FindAncestor<DataGridRow>(
            e.OriginalSource as DependencyObject);

        bool firstColumnOnly = BoxSelectBehavior.GetIsRowSelectionFirstColumnOnly(grid);
        bool nonPrimaryColumnArea = firstColumnOnly &&
            s.DragRow != null &&
            !BoxSelectBehavior.IsRowSelectionPrimaryArea(grid, e.OriginalSource as DependencyObject);

        // 右側余白（列の合計幅より右）または先頭列以外からは矩形選択を優先し、D&D開始判定を無効化する
        s.StartedOnRightEmptyArea = IsRightEmptyArea(grid, s.MouseDownPos) || nonPrimaryColumnArea;

        if (s.StartedOnRightEmptyArea)
        {
            // 右側余白からは D&D を始めない
            s.DragRow = null;
        }

        // ===== Explorer 互換：複数選択中の「選択済み行」マウスダウンでは選択を単一化しない =====
        // Explorer は「ドラッグになるかもしれない」間は選択を変えず、
        // クリック確定（MouseUp）した時だけ単一選択に落ちます。
        // WPF DataGrid は MouseDown 時点で単一化しがちなので、ここで既定処理を止めます。
        s.SuppressSelectionOnMouseDown = false;
        s.ClickedSelectedItem = null;

        if (s.DragRow != null &&
            grid.SelectedItems.Count > 1 &&
            grid.SelectedItems.Contains(s.DragRow.Item) &&
            Keyboard.Modifiers == ModifierKeys.None)
        {
            s.SuppressSelectionOnMouseDown = true;
            s.ClickedSelectedItem = s.DragRow.Item;

            // DataGrid の既定の選択変更を抑止
            e.Handled = true;

            // フォーカスだけは当てておく（キーボード操作・見た目の一貫性）
            if (!grid.IsKeyboardFocusWithin)
                grid.Focus();

            // CurrentCell もクリック行に寄せる（セル選択や編集起点のズレ防止）
            if (grid.Columns.Count > 0)
                grid.CurrentCell = new DataGridCellInfo(s.DragRow.Item, grid.Columns[0]);
        }

        // マウスダウン時の選択されたアイテムを保存（BoxSelectBehaviorが選択を変更する前に）
        // ただし、SavedSelectedItemsが既に存在する場合は上書きしない（選択状態の復元処理中の場合）
        if (s.SavedSelectedItems == null)
        {
            s.SavedSelectedItems = new List<object>();
            foreach (var item in grid.SelectedItems)
            {
                s.SavedSelectedItems.Add(item);
            }
        }
    }

    /// <summary>
    /// Explorer 互換：複数選択中に「選択済み行」を右クリックしても選択を単一化しない。
    /// （Windows Explorer は右クリックで選択を崩さず、そのままコンテキストメニューが出ます）
    /// </summary>
    private static void OnRightMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;

        var s = GetState(grid);
        if (s.IsDragging)
        {
            // 右クリックを押した瞬間にドラッグをキャンセルする
            s.CancelDragRequested = true;
            // 右クリックを離すまで再ドラッグを抑止
            s.SuppressDragUntilLeftUp = true;
            e.Handled = true;
            return;
        }

        var row = VirtualTreeUtil.FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row == null) return;

        if (grid.SelectedItems.Count > 1 &&
            grid.SelectedItems.Contains(row.Item) &&
            Keyboard.Modifiers == ModifierKeys.None)
        {
            // DataGrid の既定挙動（右クリックで単一化）を抑止。
            // ※ ContextMenu は通常 MouseRightButtonUp / ContextMenuOpening で出るので、
            //    ここを Handled にしても多くのケースで問題ありません。
            e.Handled = true;

            // フォーカス/カレントセルだけは合わせる（見た目・次操作の一貫性）
            if (!grid.IsKeyboardFocusWithin)
                grid.Focus();

            if (grid.Columns.Count > 0)
                grid.CurrentCell = new DataGridCellInfo(row.Item, grid.Columns[0]);
        }
    }

    private static void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        var s = GetState(grid);

        // ドラッグに発展しなかった「クリック確定」時だけ単一選択に落とす（Explorer と同じ）
        if (!s.IsDragging && s.SuppressSelectionOnMouseDown && s.ClickedSelectedItem != null)
        {
            try
            {
                var item = s.ClickedSelectedItem;
                grid.SelectedItems.Clear();
                grid.SelectedItems.Add(item);
                grid.SelectedItem = item;

                // CurrentCell も合わせる（選択の見た目のズレ/次操作のズレ防止）
                if (grid.Columns.Count > 0)
                    grid.CurrentCell = new DataGridCellInfo(item, grid.Columns[0]);
            }
            finally
            {
                s.SuppressSelectionOnMouseDown = false;
                s.ClickedSelectedItem = null;
                s.SavedSelectedItems = null;
            }
        }
        else
        {
            // 通常ケース：抑止フラグは必ずクリア
            s.SuppressSelectionOnMouseDown = false;
            s.ClickedSelectedItem = null;
            s.StartedOnRightEmptyArea = false;
            if (e.ChangedButton == MouseButton.Left)
                s.SuppressDragUntilLeftUp = false;
        }
    }

    private static void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var s = GetState(grid);
        if (s.SuppressDragUntilLeftUp) return;
        if (s.StartedOnRightEmptyArea) return; // 右側余白からは矩形選択を優先
        if (s.IsDragging || s.DragRow == null) return;

        const int dragStartDelayMs = 1300;  // 1.3秒間ドラッグ開始を遅延させる（誤ドラッグ防止）
        var elapsedSinceMouseDownMs = (Stopwatch.GetTimestamp() - s.MouseDownTimestamp) * 1000.0 / Stopwatch.Frequency;
        if (elapsedSinceMouseDownMs < dragStartDelayMs) return;

        var pos = e.GetPosition(grid);
        if (Math.Abs(pos.X - s.MouseDownPos.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - s.MouseDownPos.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        // ==== ドラッグ開始 ====
        // まず、BoxSelectBehaviorが選択を変更する前に、選択状態を確実に保存
        // 保存された選択がある場合はそれを使用（OnMouseDownで保存済み）
        var items = new List<object>();
        if (s.SavedSelectedItems != null && s.SavedSelectedItems.Count > 0)
        {
            items.AddRange(s.SavedSelectedItems);
        }
        else if (grid.SelectedItems.Count > 0)
        {
            // 保存された選択がない場合、現在の選択を使用（BoxSelectBehaviorが変更する前の状態）
            foreach (var item in grid.SelectedItems)
            {
                items.Add(item);
            }
        }
        else if (s.DragRow?.Item != null)
        {
            // 選択されていない場合、ドラッグ開始した行のアイテムを使用
            items.Add(s.DragRow.Item);
        }
        
        // ドラッグ開始フラグを設定
        s.IsDragging = true;
        s.DragStartTimestamp = Stopwatch.GetTimestamp();

        // MouseDown で抑止していた「クリック確定時の単一化」は、ドラッグになったので無効化
        s.SuppressSelectionOnMouseDown = false;
        s.ClickedSelectedItem = null;
        
        // イベントを処理済みとしてマーク（BoxSelectBehaviorが後続の処理を行わないようにする）
        e.Handled = true;
        
        // 矩形選択の枠をクリア（選択されたアイテムは既に取得済み）
        BoxSelectBehavior.ClearSelection(grid);
        
        // ドラッグ開始時に選択状態を保持（BoxSelectBehaviorが選択を変更した後でも、正しい選択状態を復元）
        // 選択状態を明示的に設定
        grid.SelectedItems.Clear();
        foreach (var item in items)
        {
            grid.SelectedItems.Add(item);
        }
        
        // ドラッグ元の複数選択された行を取得してホバー状態にする
        s.DraggedRows = new List<DataGridRow>();
        s.OriginalBackgroundLocalValues = new Dictionary<DataGridRow, object?>();
        
        foreach (var item in items)
        {
            var row = grid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
            if (row != null)
            {
                s.DraggedRows.Add(row);
                s.OriginalBackgroundLocalValues[row] = row.ReadLocalValue(Control.BackgroundProperty);
                
                // ホバー効果として背景色を変更（薄い青色）
                var hoverBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0x7A, 0xCC));
                hoverBrush.Freeze();
                row.SetCurrentValue(Control.BackgroundProperty, hoverBrush);
            }
        }

        var build = GetBuildPayload(grid);
        if (build == null)
        {
            ClearDraggedRows(grid);
            return;
        }
        
        var data = build.Invoke(items);
        if (data == null)
        {
            ClearDraggedRows(grid);
            return;
        }

        var dragResult = DragDrop.DoDragDrop(grid, data, DragDropEffects.Move | DragDropEffects.Copy);
        
        // ドラッグ元の行のホバー効果をクリア
        ClearDraggedRows(grid);
        
        // ドラッグ操作完了後に矩形選択をクリア
        BoxSelectBehavior.ClearSelection(grid);
        
        // ドラッグがキャンセルされた場合（DragDropEffects.None）、選択状態を復元（OS標準のエクスプローラーと同じ動作）
        if (dragResult == DragDropEffects.None && s.SavedSelectedItems != null && s.SavedSelectedItems.Count > 0)
        {
            // 選択状態を保存（非同期処理で使用するため）
            var savedItems = new List<object>(s.SavedSelectedItems);
            
            // 選択状態を復元（非同期で実行して、他のイベントハンドラが実行された後に確実に復元）
            grid.Dispatcher.BeginInvoke(new System.Action(() =>
            {
                try
                {
                    var state = GetState(grid);
                    grid.SelectedItems.Clear();
                    foreach (var item in savedItems)
                    {
                        if (item != null)
                        {
                            grid.SelectedItems.Add(item);
                        }
                    }
                    
                    // フォーカスを復元
                    if (!grid.IsFocused)
                    {
                        grid.Focus();
                    }
                    
                    // 選択状態の復元が完了した後、IsDraggingをfalseに設定
                    state.IsDragging = false;
                    state.SavedSelectedItems = null;
                }
                catch
                {
                    var state = GetState(grid);
                    state.IsDragging = false;
                    state.SavedSelectedItems = null;
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        else
        {
            // ドラッグ操作が完了したことをマーク
            s.IsDragging = false;
            // 保存された選択をクリア
            s.SavedSelectedItems = null;
        }
    }

    private static void OnQueryContinueDrag(object sender, QueryContinueDragEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        var s = GetState(grid);
        if (!s.IsDragging) return;

        if (e.EscapePressed)
        {
            s.SuppressDragUntilLeftUp = true;
            if (Mouse.Captured != null)
                Mouse.Capture(null);
            e.Action = DragAction.Cancel;
            e.Handled = true;
            return;
        }

        if (s_lastRightButtonDownTimestamp >= s.DragStartTimestamp)
        {
            s_lastRightButtonDownTimestamp = 0;
            s.SuppressDragUntilLeftUp = true;
            if (Mouse.Captured != null)
                Mouse.Capture(null);
            e.Action = DragAction.Cancel;
            e.Handled = true;
            return;
        }

        if (s.CancelDragRequested)
        {
            s.CancelDragRequested = false;
            s.SuppressDragUntilLeftUp = true;
            // 左ボタンのドラッグ状態も終わらせるため、キャプチャを解放
            if (Mouse.Captured != null)
                Mouse.Capture(null);
            e.Action = DragAction.Cancel;
            e.Handled = true;
            return;
        }

        // 右クリック中はドラッグをキャンセルする
        if (e.KeyStates.HasFlag(DragDropKeyStates.RightMouseButton) ||
            Mouse.RightButton == MouseButtonState.Pressed)
        {
            s.SuppressDragUntilLeftUp = true;
            if (Mouse.Captured != null)
                Mouse.Capture(null);
            e.Action = DragAction.Cancel;
            e.Handled = true;
        }
    }

    private static void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (e.StagingItem.Input is MouseButtonEventArgs mbe &&
            mbe.ChangedButton == MouseButton.Right &&
            mbe.ButtonState == MouseButtonState.Pressed)
        {
            s_lastRightButtonDownTimestamp = Stopwatch.GetTimestamp();
        }
    }

    #endregion

    private static bool IsRightEmptyArea(System.Windows.Controls.DataGrid grid, Point pos)
    {
        double columnsWidth = grid.Columns
            .Where(c => c.Visibility == Visibility.Visible)
            .Sum(c => c.ActualWidth);

        return pos.X > columnsWidth;
    }

    #region Drag Events

    private static void OnDragEnter(object sender, DragEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        UpdateHoverRow(grid, e);
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        // ホバー効果のみを更新（e.Effectsは既存のハンドラーに任せる）
        UpdateHoverRow(grid, e);
        // e.Handledは設定しない（既存のDragOverハンドラーと共存）
    }

    private static void OnDragLeave(object sender, DragEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        ClearHoverRow(grid);
        ClearHoverCandidate(grid);
    }

    private static DataGridRow? GetRowFromPoint(System.Windows.Controls.DataGrid grid, Point position)
    {
        if (IsRightEmptyArea(grid, position)) return null;

        var hit = grid.InputHitTest(position) as DependencyObject;
        var row = VirtualTreeUtil.FindAncestor<DataGridRow>(hit);
        if (row == null) return null;

        var origin = row.TranslatePoint(new Point(0, 0), grid);
        var rect = new Rect(origin, row.RenderSize);
        return rect.Contains(position) ? row : null;
    }

    private static void UpdateHoverRow(System.Windows.Controls.DataGrid grid, DragEventArgs e)
    {
        var s = GetState(grid);
        var pos = e.GetPosition(grid);
        var row = GetRowFromPoint(grid, pos);

        if (row == null)
        {
            ClearHoverRow(grid);
            ClearHoverCandidate(grid);
            return;
        }

        // 同じ行の場合は何もしない
        if (s.HoverRow == row) return;

        if (s.HoverCandidateRow != row)
        {
            s.HoverCandidateRow = row;
            s.HoverCandidateTimestamp = Stopwatch.GetTimestamp();
            return;
        }

        const int hoverActivationMs = 120;
        var elapsedMs = (Stopwatch.GetTimestamp() - s.HoverCandidateTimestamp) * 1000.0 / Stopwatch.Frequency;
        if (elapsedMs < hoverActivationMs) return;

        // 前の行のホバー効果をクリア
        ClearHoverRow(grid);
        ClearHoverCandidate(grid);

        // 新しい行にホバー効果を適用
        if (row != null)
        {
            s.HoverRow = row;
            s.HoverOriginalBackgroundLocalValue = row.ReadLocalValue(Control.BackgroundProperty);
            
            // ホバー効果として背景色を変更（薄い青色）
            var hoverBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0x7A, 0xCC));
            hoverBrush.Freeze();
            row.SetCurrentValue(Control.BackgroundProperty, hoverBrush);
        }
    }

    private static void ClearHoverRow(System.Windows.Controls.DataGrid grid)
    {
        var s = GetState(grid);
        if (s.HoverRow != null)
        {
            if (ReferenceEquals(s.HoverOriginalBackgroundLocalValue, DependencyProperty.UnsetValue))
            {
                s.HoverRow.ClearValue(Control.BackgroundProperty);
            }
            else
            {
                s.HoverRow.SetValue(Control.BackgroundProperty, s.HoverOriginalBackgroundLocalValue);
            }
            s.HoverRow = null;
            s.HoverOriginalBackgroundLocalValue = null;
        }
    }

    private static void ClearHoverCandidate(System.Windows.Controls.DataGrid grid)
    {
        var s = GetState(grid);
        s.HoverCandidateRow = null;
        s.HoverCandidateTimestamp = 0;
    }

    private static void ClearDraggedRows(System.Windows.Controls.DataGrid grid)
    {
        var s = GetState(grid);
        if (s.DraggedRows != null && s.OriginalBackgroundLocalValues != null)
        {
            foreach (var row in s.DraggedRows)
            {
                if (s.OriginalBackgroundLocalValues.TryGetValue(row, out var originalLocalValue))
                {
                    if (ReferenceEquals(originalLocalValue, DependencyProperty.UnsetValue))
                    {
                        row.ClearValue(Control.BackgroundProperty);
                    }
                    else
                    {
                        row.SetValue(Control.BackgroundProperty, originalLocalValue);
                    }
                }
            }
            s.DraggedRows = null;
            s.OriginalBackgroundLocalValues = null;
        }
    }

    #endregion

    #region Drop

    private static void OnDrop(object sender, DragEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;

        // ホバー効果をクリア
        ClearHoverRow(grid);
        ClearDraggedRows(grid);

        var handler = GetDropHandler(grid);
        if (handler == null)
        {
            return;
        }

        // 内部のドラッグ（DataGridから開始されたドラッグ）かどうかをチェック
        // BuildPayloadで作成されたデータ形式をチェック
        bool isInternalDrag = e.Data.GetDataPresent("FileSystemItem") || 
                              e.Data.GetDataPresent("FileSystemItems");
        
        var pos = e.GetPosition(grid);
        var row = GetRowFromPoint(grid, pos);
        var s = GetState(grid);
        if (row == null)
            row = s.HoverRow;

        // 1列目選択モードの場合、1列目にドロップした場合のみ行のアイテムを渡す
        // 2列目以降にドロップした場合はnullを渡す（現在のディレクトリにドロップ）
        object? dropTargetItem = row?.Item;
        var hitSource = e.OriginalSource as DependencyObject;
        bool isOnPrimaryArea = BoxSelectBehavior.IsRowSelectionPrimaryArea(grid, hitSource);
        if (!isOnPrimaryArea)
        {
            // 2列目以降にドロップした場合は、行のアイテムをnullにして現在のディレクトリにドロップ
            dropTargetItem = null;
        }

        handler(e.Data, dropTargetItem);
        
        // 内部のドラッグの場合のみ、既存のDropハンドラーが呼ばれないようにする
        // 外部からのドラッグ（DataFormats.FileDrop）の場合は、既存のListView_Dropも処理する
        if (isInternalDrag)
        {
            e.Handled = true;
        }
    }

    #endregion
}
