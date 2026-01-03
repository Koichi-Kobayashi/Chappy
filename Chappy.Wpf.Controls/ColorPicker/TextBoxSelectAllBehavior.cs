using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Chappy.Wpf.Controls.ColorPicker;

/// <summary>
/// TextBoxにフォーカス時またはクリック時に全選択機能を追加するビヘイビア
/// ReadOnlyでも動作する
/// </summary>
public static class TextBoxSelectAllBehavior
{
    /// <summary>
    /// フォーカス時に全選択するかどうかを制御する依存プロパティ
    /// </summary>
    public static readonly DependencyProperty SelectAllOnFocusProperty =
        DependencyProperty.RegisterAttached(
            "SelectAllOnFocus",
            typeof(bool),
            typeof(TextBoxSelectAllBehavior),
            new PropertyMetadata(false, OnSelectAllOnFocusChanged));

    /// <summary>
    /// 指定された要素にフォーカス時全選択機能を有効にする
    /// </summary>
    /// <param name="element">対象の要素</param>
    /// <param name="value">有効にする場合はtrue</param>
    public static void SetSelectAllOnFocus(DependencyObject element, bool value)
        => element.SetValue(SelectAllOnFocusProperty, value);

    /// <summary>
    /// 指定された要素のフォーカス時全選択機能の有効/無効状態を取得する
    /// </summary>
    /// <param name="element">対象の要素</param>
    /// <returns>有効な場合はtrue</returns>
    public static bool GetSelectAllOnFocus(DependencyObject element)
        => (bool)element.GetValue(SelectAllOnFocusProperty);

    /// <summary>
    /// SelectAllOnFocusPropertyの変更時に呼ばれるコールバック
    /// </summary>
    /// <param name="d">変更された依存オブジェクト</param>
    /// <param name="e">変更イベントの引数</param>
    private static void OnSelectAllOnFocusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb) return;

        if ((bool)e.NewValue)
        {
            tb.GotKeyboardFocus += OnGotKeyboardFocus;
            tb.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        }
        else
        {
            tb.GotKeyboardFocus -= OnGotKeyboardFocus;
            tb.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        }
    }

    /// <summary>
    /// キーボードフォーカスが取得された時のイベントハンドラ
    /// </summary>
    /// <param name="sender">イベント送信元</param>
    /// <param name="e">キーボードフォーカスイベントの引数</param>
    private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.SelectAll();
        }
    }

    /// <summary>
    /// マウス左ボタン押下時のイベントハンドラ
    /// フォーカスがない場合はフォーカスを付けて全選択する
    /// </summary>
    /// <param name="sender">イベント送信元</param>
    /// <param name="e">マウスボタンイベントの引数</param>
    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox tb) return;

        // クリックした瞬間にフォーカスがない場合はフォーカスを付けて全選択
        if (!tb.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            tb.Focus();
            tb.SelectAll();
        }
    }
}
