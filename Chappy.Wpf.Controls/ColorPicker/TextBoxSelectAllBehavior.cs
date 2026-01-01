using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Chappy.Wpf.Controls.ColorPicker
{
    /// <summary>
    /// TextBox: クリック or フォーカスで全選択（ReadOnlyでもOK）
    /// </summary>
    public static class TextBoxSelectAllBehavior
    {
        public static readonly DependencyProperty SelectAllOnFocusProperty =
            DependencyProperty.RegisterAttached(
                "SelectAllOnFocus",
                typeof(bool),
                typeof(TextBoxSelectAllBehavior),
                new PropertyMetadata(false, OnSelectAllOnFocusChanged));

        public static void SetSelectAllOnFocus(DependencyObject element, bool value)
            => element.SetValue(SelectAllOnFocusProperty, value);

        public static bool GetSelectAllOnFocus(DependencyObject element)
            => (bool)element.GetValue(SelectAllOnFocusProperty);

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

        private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.SelectAll();
            }
        }

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
}
