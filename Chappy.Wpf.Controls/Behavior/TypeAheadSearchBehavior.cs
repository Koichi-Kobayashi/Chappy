#nullable enable
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Chappy.Wpf.Controls.Behaviors;

/// <summary>
/// DataGridでタイプ先行検索（インクリメンタルサーチ）を実現するビヘイビア。
/// ファイル名を入力することで、その名前で始まるアイテムにフォーカスを移動します。
/// Windows Explorerと同じ動作をします。
/// </summary>
public static class TypeAheadSearchBehavior
{
    #region 添付プロパティ

    /// <summary>
    /// タイプ先行検索を有効にするかどうか
    /// </summary>
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(TypeAheadSearchBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>
    /// IsEnabled添付プロパティの値を設定します。
    /// </summary>
    /// <param name="d">対象の依存オブジェクト</param>
    /// <param name="value">タイプ先行検索を有効にする場合はtrue</param>
    public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

    /// <summary>
    /// IsEnabled添付プロパティの値を取得します。
    /// </summary>
    /// <param name="d">対象の依存オブジェクト</param>
    /// <returns>タイプ先行検索が有効な場合はtrue</returns>
    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);

    /// <summary>
    /// 検索対象のプロパティ名（デフォルトは "Name"）
    /// </summary>
    public static readonly DependencyProperty SearchPropertyNameProperty =
        DependencyProperty.RegisterAttached(
            "SearchPropertyName",
            typeof(string),
            typeof(TypeAheadSearchBehavior),
            new PropertyMetadata("Name"));

    /// <summary>
    /// SearchPropertyName添付プロパティの値を設定します。
    /// </summary>
    /// <param name="d">対象の依存オブジェクト</param>
    /// <param name="value">検索対象のプロパティ名</param>
    public static void SetSearchPropertyName(DependencyObject d, string value) => d.SetValue(SearchPropertyNameProperty, value);

    /// <summary>
    /// SearchPropertyName添付プロパティの値を取得します。
    /// </summary>
    /// <param name="d">対象の依存オブジェクト</param>
    /// <returns>検索対象のプロパティ名</returns>
    public static string GetSearchPropertyName(DependencyObject d) => (string)d.GetValue(SearchPropertyNameProperty);

    /// <summary>
    /// 連続入力とみなすタイムアウト時間（ミリ秒）。デフォルトは1000ms。
    /// </summary>
    public static readonly DependencyProperty InputTimeoutMsProperty =
        DependencyProperty.RegisterAttached(
            "InputTimeoutMs",
            typeof(int),
            typeof(TypeAheadSearchBehavior),
            new PropertyMetadata(1000));

    /// <summary>
    /// InputTimeoutMs添付プロパティの値を設定します。
    /// </summary>
    /// <param name="d">対象の依存オブジェクト</param>
    /// <param name="value">連続入力とみなすタイムアウト時間（ミリ秒）</param>
    public static void SetInputTimeoutMs(DependencyObject d, int value) => d.SetValue(InputTimeoutMsProperty, value);

    /// <summary>
    /// InputTimeoutMs添付プロパティの値を取得します。
    /// </summary>
    /// <param name="d">対象の依存オブジェクト</param>
    /// <returns>連続入力とみなすタイムアウト時間（ミリ秒）</returns>
    public static int GetInputTimeoutMs(DependencyObject d) => (int)d.GetValue(InputTimeoutMsProperty);

    #endregion

    #region State

    /// <summary>
    /// タイプ先行検索の状態を保持する内部クラス。
    /// 各DataGridインスタンスに対して1つの状態が関連付けられます。
    /// </summary>
    private sealed class State
    {
        /// <summary>
        /// 現在の検索文字列
        /// </summary>
        public string SearchText = string.Empty;

        /// <summary>
        /// 最後の入力時刻（Stopwatch.GetTimestamp()）
        /// </summary>
        public long LastInputTimestamp;

        /// <summary>
        /// 検索文字列をリセットするためのタイマー
        /// </summary>
        public DispatcherTimer? ResetTimer;

        /// <summary>
        /// 同じ検索文字列で次のアイテムに移動するためのインデックス
        /// （同じ文字を繰り返し入力したときに次のアイテムに移動するため）
        /// </summary>
        public int CurrentMatchIndex;

        /// <summary>
        /// 直前の検索文字列（同じ文字の繰り返し検出用）
        /// </summary>
        public string? PreviousSearchText;
    }

    /// <summary>
    /// 検索状態を保持するための内部添付プロパティ。
    /// </summary>
    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(State),
            typeof(TypeAheadSearchBehavior),
            new PropertyMetadata(null));

    /// <summary>
    /// 指定されたDataGridの検索状態を取得します。
    /// 状態が存在しない場合は新しく作成します。
    /// </summary>
    /// <param name="grid">対象のDataGrid</param>
    /// <returns>DataGridに関連付けられた検索状態</returns>
    private static State GetState(System.Windows.Controls.DataGrid grid)
    {
        var state = (State?)grid.GetValue(StateProperty);
        if (state == null)
        {
            state = new State();
            grid.SetValue(StateProperty, state);
        }
        return state;
    }

    #endregion

    #region 有効化/無効化

    /// <summary>
    /// IsEnabledプロパティが変更されたときに呼び出されるコールバック。
    /// DataGridへのイベントハンドラのアタッチ/デタッチを行います。
    /// </summary>
    /// <param name="d">プロパティが変更された依存オブジェクト</param>
    /// <param name="e">イベント引数</param>
    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not System.Windows.Controls.DataGrid grid) return;

        if ((bool)e.NewValue)
        {
            grid.PreviewTextInput += OnPreviewTextInput;
            grid.PreviewKeyDown += OnPreviewKeyDown;
        }
        else
        {
            grid.PreviewTextInput -= OnPreviewTextInput;
            grid.PreviewKeyDown -= OnPreviewKeyDown;

            // タイマーをクリーンアップ
            var state = (State?)grid.GetValue(StateProperty);
            if (state?.ResetTimer != null)
            {
                state.ResetTimer.Stop();
                state.ResetTimer = null;
            }
        }
    }

    #endregion

    #region イベントハンドラ

    /// <summary>
    /// テキスト入力のプレビューイベントハンドラ。
    /// 入力された文字を検索文字列に追加し、マッチするアイテムを検索します。
    /// 同じ文字が繰り返し入力された場合は、次のマッチングアイテムに移動します。
    /// </summary>
    /// <param name="sender">イベント発生元</param>
    /// <param name="e">テキスト入力イベント引数</param>
    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;

        // テキストが空の場合は無視
        if (string.IsNullOrEmpty(e.Text)) return;

        // 制御文字は無視
        if (char.IsControl(e.Text[0])) return;

        // 編集モード中は無視
        if (grid.IsEditing()) return;

        var state = GetState(grid);
        var timeoutMs = GetInputTimeoutMs(grid);

        // タイムアウト内かどうかを確認
        var now = Stopwatch.GetTimestamp();
        var elapsedMs = (now - state.LastInputTimestamp) * 1000.0 / Stopwatch.Frequency;

        if (elapsedMs > timeoutMs)
        {
            // タイムアウト超過：検索文字列をリセット
            state.SearchText = string.Empty;
            state.CurrentMatchIndex = 0;
            state.PreviousSearchText = null;
        }

        // 同じ文字の繰り返しかどうかをチェック
        bool isSameCharRepeat = state.SearchText.Length > 0 &&
                                e.Text.Length == 1 &&
                                state.SearchText.All(c => c == e.Text[0]) &&
                                state.SearchText[0] == e.Text[0];

        if (isSameCharRepeat)
        {
            // 同じ文字の繰り返し：次のマッチに移動
            state.CurrentMatchIndex++;
            // 検索文字列は変更しない（最初の文字のみで検索を継続）
        }
        else
        {
            // 新しい文字：検索文字列に追加
            state.SearchText += e.Text;
            state.CurrentMatchIndex = 0;
        }

        state.LastInputTimestamp = now;

        // 検索を実行
        PerformSearch(grid, state);

        // リセットタイマーを再起動
        ResetTimer(grid, state, timeoutMs);

        e.Handled = true;
    }

    /// <summary>
    /// キー押下のプレビューイベントハンドラ。
    /// Backspaceで最後の文字を削除、Deleteで検索クリア、Escapeで検索終了を行います。
    /// </summary>
    /// <param name="sender">イベント発生元</param>
    /// <param name="e">キーイベント引数</param>
    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.DataGrid grid) return;

        var state = GetState(grid);

        // BackspaceやDeleteで検索文字列をクリア
        if (e.Key == Key.Back || e.Key == Key.Delete)
        {
            if (state.SearchText.Length > 0)
            {
                if (e.Key == Key.Back && state.SearchText.Length > 1)
                {
                    // Backspace: 最後の文字を削除
                    state.SearchText = state.SearchText.Substring(0, state.SearchText.Length - 1);
                    state.CurrentMatchIndex = 0;
                    PerformSearch(grid, state);
                    
                    var timeoutMs = GetInputTimeoutMs(grid);
                    state.LastInputTimestamp = Stopwatch.GetTimestamp();
                    ResetTimer(grid, state, timeoutMs);
                }
                else
                {
                    // Delete または Backspace で1文字だけの場合: 検索をクリア
                    ClearSearch(state);
                }
                // e.Handled = true; // 他のハンドラーも処理する必要がある場合はコメントアウト
            }
            return;
        }

        // Escapeで検索文字列をクリア
        if (e.Key == Key.Escape)
        {
            if (state.SearchText.Length > 0)
            {
                ClearSearch(state);
                // e.Handled = true;
            }
            return;
        }
    }

    #endregion

    #region 検索ロジック

    /// <summary>
    /// 検索を実行し、マッチするアイテムを選択してスクロールします。
    /// 同じ文字の繰り返しの場合は、最初の1文字で検索を行い、
    /// CurrentMatchIndexに応じて次のマッチングアイテムを選択します。
    /// </summary>
    /// <param name="grid">検索対象のDataGrid</param>
    /// <param name="state">現在の検索状態</param>
    private static void PerformSearch(System.Windows.Controls.DataGrid grid, State state)
    {
        if (string.IsNullOrEmpty(state.SearchText)) return;

        var propertyName = GetSearchPropertyName(grid);
        var searchText = state.SearchText;

        // 同じ文字の繰り返しの場合、最初の1文字で検索
        if (searchText.Length > 1 && searchText.All(c => c == searchText[0]))
        {
            searchText = searchText[0].ToString();
        }

        // アイテムを検索
        var items = grid.Items.Cast<object>().ToList();
        var matchingItems = items
            .Select((item, index) => new { Item = item, Index = index })
            .Where(x => MatchesSearch(x.Item, propertyName, searchText))
            .ToList();

        if (matchingItems.Count == 0) return;

        // 現在のインデックスに応じてマッチを選択
        var matchIndex = state.CurrentMatchIndex % matchingItems.Count;
        var targetItem = matchingItems[matchIndex];

        // アイテムを選択してスクロール
        grid.SelectedItem = targetItem.Item;
        grid.ScrollIntoView(targetItem.Item);

        // フォーカスも移動
        var row = grid.ItemContainerGenerator.ContainerFromItem(targetItem.Item) as System.Windows.Controls.DataGridRow;
        if (row != null)
        {
            row.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }

        Debug.WriteLine($"TypeAheadSearch: Found '{searchText}' at index {targetItem.Index} (match {matchIndex + 1}/{matchingItems.Count})");
    }

    /// <summary>
    /// 指定されたアイテムが検索条件に一致するかどうかを判定します。
    /// 大文字小文字を区別せず、プロパティ値の先頭が検索文字列で始まるかどうかを確認します。
    /// </summary>
    /// <param name="item">検索対象のアイテム</param>
    /// <param name="propertyName">検索対象のプロパティ名</param>
    /// <param name="searchText">検索文字列</param>
    /// <returns>マッチする場合はtrue</returns>
    private static bool MatchesSearch(object item, string propertyName, string searchText)
    {
        if (item == null) return false;

        var property = item.GetType().GetProperty(propertyName);
        if (property == null) return false;

        var value = property.GetValue(item)?.ToString();
        if (value == null || value.Length == 0) return false;

        return value.StartsWith(searchText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 検索状態をリセットするためのタイマーを開始または再起動します。
    /// タイムアウト後、検索文字列がクリアされます。
    /// </summary>
    /// <param name="grid">対象のDataGrid</param>
    /// <param name="state">現在の検索状態</param>
    /// <param name="timeoutMs">タイムアウト時間（ミリ秒）</param>
    private static void ResetTimer(System.Windows.Controls.DataGrid grid, State state, int timeoutMs)
    {
        if (state.ResetTimer == null)
        {
            state.ResetTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(timeoutMs)
            };
            state.ResetTimer.Tick += (s, e) =>
            {
                ClearSearch(state);
            };
        }
        else
        {
            state.ResetTimer.Stop();
            state.ResetTimer.Interval = TimeSpan.FromMilliseconds(timeoutMs);
        }
        state.ResetTimer.Start();
    }

    /// <summary>
    /// 検索状態をクリアします。
    /// 検索文字列、マッチインデックス、前回の検索文字列をリセットし、タイマーを停止します。
    /// </summary>
    /// <param name="state">クリアする検索状態</param>
    private static void ClearSearch(State state)
    {
        state.SearchText = string.Empty;
        state.CurrentMatchIndex = 0;
        state.PreviousSearchText = null;
        state.ResetTimer?.Stop();
        Debug.WriteLine("TypeAheadSearch: Search cleared");
    }

    #endregion

    #region ヘルパー

    /// <summary>
    /// DataGridが編集モードかどうかを判定します。
    /// 現在のセルが編集状態にあるかどうかを確認します。
    /// </summary>
    /// <param name="grid">対象のDataGrid</param>
    /// <returns>編集モードの場合はtrue</returns>
    private static bool IsEditing(this System.Windows.Controls.DataGrid grid)
    {
        // DataGrid.CurrentCellがある場合、そのセルが編集中かどうかを確認
        if (grid.CurrentCell.Column == null) return false;

        var cell = grid.CurrentCell.Column.GetCellContent(grid.CurrentCell.Item);
        if (cell == null) return false;

        // 親のDataGridCellを取得
        var parent = cell;
        while (parent != null && parent is not System.Windows.Controls.DataGridCell)
        {
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent) as FrameworkElement;
        }

        return parent is System.Windows.Controls.DataGridCell dgCell && dgCell.IsEditing;
    }

    #endregion
}
