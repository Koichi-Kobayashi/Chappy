#nullable enable
using Chappy.Wpf.Controls.Util;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Chappy.Wpf.Controls.Behaviors;

public static class DataGridEditGuardBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(DataGridEditGuardBehavior),
            new PropertyMetadata(false, OnChanged));

    public static void SetIsEnabled(DependencyObject d, bool v)
        => d.SetValue(IsEnabledProperty, v);

    public static bool GetIsEnabled(DependencyObject d)
        => (bool)d.GetValue(IsEnabledProperty);

    public static readonly DependencyProperty RenameColumnSortMemberPathProperty =
        DependencyProperty.RegisterAttached(
            "RenameColumnSortMemberPath",
            typeof(string),
            typeof(DataGridEditGuardBehavior),
            new PropertyMetadata("Name"));

    public static string GetRenameColumnSortMemberPath(DependencyObject d)
        => (string)d.GetValue(RenameColumnSortMemberPathProperty);

    public static void SetRenameColumnSortMemberPath(DependencyObject d, string v)
        => d.SetValue(RenameColumnSortMemberPathProperty, v);

    private sealed class State
    {
        public bool IsRenaming;
        public DispatcherTimer? Timer;
        public object? PendingItem;
        public Point DownPos;
        public bool SuppressCommitOnce;     // 次のフォーカス喪失時の CommitEdit を抑制
        public bool SuppressMouseEditOnce;  // マウスの編集開始を 1 回だけ抑止するフラグ
        public bool SuppressRenameOnce;     // 次の MouseUp で予約しない
    }

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(State),
            typeof(DataGridEditGuardBehavior),
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
            // Key は tunneling で確実に拾う
            grid.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown), true);

            // Mouse は bubbling で拾う
            grid.PreviewMouseLeftButtonDown += OnMouseDown;
            grid.PreviewMouseLeftButtonUp += OnMouseUp;
            grid.PreviewMouseMove += OnMouseMove;

            // ダブルクリックでの編集開始を防止
            grid.PreviewMouseDoubleClick += OnPreviewMouseDoubleClick;
            grid.BeginningEdit += OnBeginningEdit;

            // フォーカス喪失で確定
            grid.LostKeyboardFocus += OnLostFocus;
            grid.PreparingCellForEdit += OnPreparingCellForEdit;
        }
        else
        {
            grid.RemoveHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown));

            grid.PreviewMouseLeftButtonDown -= OnMouseDown;
            grid.PreviewMouseLeftButtonUp -= OnMouseUp;
            grid.PreviewMouseMove -= OnMouseMove;

            grid.PreviewMouseDoubleClick -= OnPreviewMouseDoubleClick;
            grid.BeginningEdit -= OnBeginningEdit;

            grid.LostKeyboardFocus -= OnLostFocus;
            grid.PreparingCellForEdit -= OnPreparingCellForEdit;
        }
    }

    #region キー操作

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        var s = GetState(grid);

        if (!s.IsRenaming)
        {
            if (e.Key == Key.F2)
            {
                RequestRename(grid);
                e.Handled = true;
            }
            return;
        }

        // ===== 編集中 =====

        // ★TextBoxの削除を自前で実行
        var src = e.OriginalSource as DependencyObject;
        var tb = src as TextBox ?? VirtualTreeUtil.FindAncestor<TextBox>(src)
                 ?? FindCurrentEditingTextBox(grid);

        if (e.Key == Key.Enter)
        {
            CommitEdit(grid);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            CancelEdit(grid);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Back || e.Key == Key.Delete)
        {
            // ★戻るを確実に止める
            e.Handled = true;

            if (tb != null)
            {
                if (e.Key == Key.Back)
                {
                    DeleteBackspace(tb);
                }
                else
                {
                    DeleteKey(tb);
                }
            }
            return;
        }

        // ←→ / Home / End
        if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Home || e.Key == Key.End)
        {
            e.Handled = true;

            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            int target = tb.CaretIndex;

            if (e.Key == Key.Home)
            {
                target = 0;
            }
            else if (e.Key == Key.End)
            {
                target = tb.Text?.Length ?? 0;
            }
            else if (e.Key == Key.Left)
            {
                target = ctrl
                    ? FindPrevWordBoundary(tb.Text ?? "", tb.CaretIndex)
                    : Math.Max(0, tb.CaretIndex - 1);
            }
            else if (e.Key == Key.Right)
            {
                target = ctrl
                    ? FindNextWordBoundary(tb.Text ?? "", tb.CaretIndex)
                    : Math.Min((tb.Text?.Length ?? 0), tb.CaretIndex + 1);
            }

            MoveCaret(tb, target, extendSelection: shift, direction: e.Key);
            return;
        }
    }

    private static TextBox? FindCurrentEditingTextBox(System.Windows.Controls.DataGrid grid)
    {
        var cellInfo = grid.CurrentCell;
        if (cellInfo.Item == null || cellInfo.Column == null) return null;

        grid.ScrollIntoView(cellInfo.Item, cellInfo.Column);

        var row = (DataGridRow?)grid.ItemContainerGenerator.ContainerFromItem(cellInfo.Item);
        if (row == null) return null;

        row.ApplyTemplate();

        // 行の中のTextBox（編集テンプレ内）
        return VirtualTreeUtil.FindDescendant<TextBox>(row);
    }

    /// <summary>
    /// バックスペースで文字削除を行う
    /// </summary>
    private static void DeleteBackspace(TextBox tb)
    {
        // 選択範囲があればそれを削除
        if (tb.SelectionLength > 0)
        {
            int start = tb.SelectionStart;
            tb.Text = tb.Text.Remove(start, tb.SelectionLength);
            tb.SelectionStart = start;
            tb.SelectionLength = 0;
            return;
        }

        // キャレット左の1文字を削除
        int i = tb.CaretIndex;
        if (i <= 0) return;

        tb.Text = tb.Text.Remove(i - 1, 1);
        tb.CaretIndex = i - 1;
    }
    /// <summary>
    /// Deleteキーで文字削除を行う（キャレット位置の1文字を削除）
    /// </summary>
    private static void DeleteKey(TextBox tb)
    {
        // 選択範囲があればそれを削除（キャレットは start に置く）
        if (tb.SelectionLength > 0)
        {
            int start = tb.SelectionStart;
            tb.Text = tb.Text.Remove(start, tb.SelectionLength);
            tb.SelectionStart = start;
            tb.SelectionLength = 0;
            tb.CaretIndex = start;
            return;
        }

        int i = tb.CaretIndex;

        // Delete は「末尾」だと削除対象が無い
        if (i < 0) i = 0;
        if (i >= tb.Text.Length) return;

        tb.Text = tb.Text.Remove(i, 1);

        // Delete は基本「その場」に残る（左に寄せない）
        tb.CaretIndex = i;
    }

    #region キャレット移動＋選択処理（Shift対応）

    /// <summary>
    /// キャレット移動＋選択処理（Shift対応）
    /// Shiftなし：選択があれば「左右キーの方向に合わせて」選択を畳む（Explorerっぽい挙動）
    /// Shiftあり：選択範囲を伸縮
    /// </summary>
    private static void MoveCaret(TextBox tb, int target, bool extendSelection, Key direction)
    {
        int len = tb.Text?.Length ?? 0;
        target = Math.Max(0, Math.Min(len, target));

        if (!extendSelection)
        {
            // 選択があれば畳む（←は先頭、→は末尾へ）
            if (tb.SelectionLength > 0)
            {
                if (direction == Key.Left || direction == Key.Home)
                    tb.CaretIndex = tb.SelectionStart;
                else
                    tb.CaretIndex = tb.SelectionStart + tb.SelectionLength;

                tb.SelectionLength = 0;
                return;
            }

            tb.CaretIndex = target;
            tb.SelectionLength = 0;
            return;
        }

        // Shiftあり：範囲選択を伸ばす
        int caret = tb.CaretIndex;

        // “アンカー”を推定
        // 選択中で caret が SelectionStart にいるなら、反対側がアンカー
        int anchor = caret;
        if (tb.SelectionLength > 0)
        {
            anchor = (caret == tb.SelectionStart)
                ? tb.SelectionStart + tb.SelectionLength
                : tb.SelectionStart;
        }

        // 先に caret を target に動かしてから selection を作り直す
        tb.CaretIndex = target;

        int start = Math.Min(anchor, target);
        int end = Math.Max(anchor, target);

        tb.SelectionStart = start;
        tb.SelectionLength = end - start;
    }

    #endregion

    #region Ctrl+← / Ctrl+→ の単語境界（簡易版）

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static int FindPrevWordBoundary(string text, int index)
    {
        index = Math.Max(0, Math.Min(text.Length, index));
        if (index == 0) return 0;

        int i = index;

        // まず左側の空白を飛ばす
        while (i > 0 && char.IsWhiteSpace(text[i - 1])) i--;

        // 次に単語/記号の塊を飛ばす
        bool word = (i > 0) && IsWordChar(text[i - 1]);
        while (i > 0)
        {
            char c = text[i - 1];
            if (char.IsWhiteSpace(c)) break;
            if (IsWordChar(c) != word) break;
            i--;
        }
        return i;
    }

    private static int FindNextWordBoundary(string text, int index)
    {
        index = Math.Max(0, Math.Min(text.Length, index));
        if (index >= text.Length) return text.Length;

        int i = index;

        // 現在位置の塊（単語/記号）を飛ばす
        bool word = IsWordChar(text[i]);
        while (i < text.Length)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c)) break;
            if (IsWordChar(c) != word) break;
            i++;
        }

        // 次の空白を飛ばして、次の塊の先頭へ
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;

        return i;
    }

    #endregion

    private static void CancelEdit(System.Windows.Controls.DataGrid grid)
    {
        var s = GetState(grid);

        if (s.SuppressCommitOnce)
        {
            s.SuppressCommitOnce = false;
            return;
        }

        grid.CancelEdit(DataGridEditingUnit.Cell);
        grid.CancelEdit(DataGridEditingUnit.Row);
        s.IsRenaming = false;
    }

    #endregion

    #region マウスによる編集開始防止

    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        var s = GetState(grid);

        // ★ ダブルクリックなら「予約は作らない」＆既存予約も潰す
        if (e.ClickCount >= 2)
        {
            s.Timer?.Stop();
            s.PendingItem = null;
            s.SuppressRenameOnce = true; // 次の MouseUp も予約させない
            return;
        }

        s.PendingItem = null;
        s.DownPos = e.GetPosition(grid);

        var row = VirtualTreeUtil.FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row == null) return;

        if (!Equals(grid.SelectedItem, row.Item)) return;

        s.PendingItem = row.Item;
    }

    private static void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        var s = GetState(grid);

        // ★ ダブルクリック or 抑止フラグなら予約しない（ここが最重要）
        if (e.ClickCount >= 2 || s.SuppressRenameOnce)
        {
            s.SuppressRenameOnce = false;
            s.PendingItem = null;
            s.Timer?.Stop();
            return;
        }

        // ダブルクリック直後は予約しない
        if (s.SuppressRenameOnce || e.ClickCount >= 2)
        {
            s.SuppressRenameOnce = false;
            s.PendingItem = null;
            s.Timer?.Stop();
            return;
        }

        if (s.PendingItem == null) return;

        s.Timer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };

        s.Timer.Stop();
        s.Timer.Tick -= OnRenameTimerTick; // ★一旦外す
        s.Timer.Tick += OnRenameTimerTick; // ★付け直す
        s.Timer.Start();

        void OnRenameTimerTick(object? _, EventArgs __)
        {
            s.Timer!.Stop();
            s.Timer.Tick -= OnRenameTimerTick;

            if (Equals(grid.SelectedItem, s.PendingItem))
                RequestRename(grid);

            s.PendingItem = null;
        }
    }

    private static void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        var s = GetState(grid);
        if (s.PendingItem == null) return;

        var p = e.GetPosition(grid);
        if (Math.Abs(p.X - s.DownPos.X) >= SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(p.Y - s.DownPos.Y) >= SystemParameters.MinimumVerticalDragDistance)
        {
            s.PendingItem = null;
        }
    }

    public static void RequestRename(System.Windows.Controls.DataGrid grid)
    {
        if (grid.SelectedItem == null) return;

        var s = GetState(grid);
        s.SuppressCommitOnce = true;

        CommitEdit(grid);

        var col = grid.Columns.FirstOrDefault(c =>
            c.SortMemberPath == GetRenameColumnSortMemberPath(grid));

        if (col == null) return;

        grid.CurrentCell = new DataGridCellInfo(grid.SelectedItem, col);
        grid.Focus();

        if (grid.BeginEdit())
            GetState(grid).IsRenaming = true;
    }

    #endregion

    #region ダブルクリックでの編集開始を防止

    /// <summary>
    /// ダブルクリックで DataGrid が標準で BeginEdit するのを止める
    /// かつ、リネーム予約も必ずキャンセルする
    /// </summary>
    private static void OnPreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        var s = GetState(grid);

        // リネーム予約を止める
        s.Timer?.Stop();
        s.PendingItem = null;

        s.IsRenaming = false;

        // 次に来る BeginningEdit を 1 回だけ抑止
        s.SuppressMouseEditOnce = true;

        // ダブルクリック直後の MouseUp で予約させない
        s.SuppressRenameOnce = true;
    }

    /// <summary>
    /// 保険：マウス起因で勝手に編集開始されるのを抑止する
    /// </summary>
    private static void OnBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;
        var s = GetState(grid);

        // 直前のダブルクリックから来た BeginEdit は必ずキャンセル
        if (s.SuppressMouseEditOnce)
        {
            e.Cancel = true;
            s.SuppressMouseEditOnce = false;
            return;
        }
    }

    #endregion

    #region フォーカス喪失で確定

    private static void OnLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;

        // フォーカスが DataGrid 内（例：DataGrid→TextBox）へ移動しただけなら閉じない
        if (e.NewFocus is DependencyObject nf && IsDescendantOf(nf, grid))
            return;

        // 本当にグリッド外へ出た時だけ確定
        CommitEdit(grid);
    }

    private static void CommitEdit(System.Windows.Controls.DataGrid grid)
    {
        var s = GetState(grid);

        if (s.SuppressCommitOnce)
        {
            s.SuppressCommitOnce = false;
            return;
        }

        grid.CommitEdit(DataGridEditingUnit.Cell, true);
        grid.CommitEdit(DataGridEditingUnit.Row, true);
        s.IsRenaming = false;
    }

    private static void OnPreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (e.EditingElement is TextBox tb)
        {
            tb.Dispatcher.BeginInvoke(new Action(() =>
            {
                tb.Focus();
                SelectNameWithoutExtension(tb);
            }), DispatcherPriority.Input);
            return;
        }

        if (e.EditingElement is ContentPresenter cp)
        {
            cp.ApplyTemplate();

            var textBox = VirtualTreeUtil.FindDescendant<TextBox>(cp);
            if (textBox != null)
            {
                textBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    textBox.Focus();
                    SelectNameWithoutExtension(textBox);
                }), DispatcherPriority.Input);
            }
        }
    }

    /// <summary>
    /// 子孫判定ヘルパー
    /// </summary>
    private static bool IsDescendantOf(DependencyObject? node, DependencyObject root)
    {
        while (node != null)
        {
            if (ReferenceEquals(node, root)) return true;

            // Visual優先
            var parent = VisualTreeHelper.GetParent(node);
            if (parent != null)
            {
                node = parent;
                continue;
            }

            // Visualが取れない時だけLogical
            node = LogicalTreeHelper.GetParent(node);
        }
        return false;
    }

    private static void SelectNameWithoutExtension(TextBox tb)
    {
        // バインディングが反映される前の瞬間があるので、フォーカス後に走らせるのが安全
        var text = tb.Text ?? string.Empty;

        // フォルダー（拡張子無し）や「.gitignore」みたいなドット始まりは全選択に寄せる
        // ※ Explorer はドット始まりファイルは全部選択寄りの挙動です
        if (text.Length == 0 || text[0] == '.')
        {
            tb.SelectAll();
            return;
        }

        var lastDot = text.LastIndexOf('.');
        if (lastDot <= 0 || lastDot >= text.Length - 1)
        {
            // ドット無し / 末尾ドット → 全選択
            tb.SelectAll();
            return;
        }

        // "name.ext" の "name" 部分だけ
        tb.Select(0, lastDot);
    }

    #endregion

}
