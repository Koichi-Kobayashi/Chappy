using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Chappy.Wpf.Controls.Util
{
    public static class SliderJumpToClickBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(SliderJumpToClickBehavior),
                new PropertyMetadata(false, OnChanged));

        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

        // ★ どのDPに書き込むか
        public static readonly DependencyProperty TargetPropertyProperty =
            DependencyProperty.RegisterAttached("TargetProperty", typeof(DependencyProperty), typeof(SliderJumpToClickBehavior),
                new PropertyMetadata(null));

        public static void SetTargetProperty(DependencyObject element, DependencyProperty? value) => element.SetValue(TargetPropertyProperty, value);
        public static DependencyProperty? GetTargetProperty(DependencyObject element) => (DependencyProperty?)element.GetValue(TargetPropertyProperty);

        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Slider slider) return;

            if ((bool)e.NewValue)
                slider.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            else
                slider.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        }

        private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Slider slider) return;

            if (FindAncestor<Thumb>(e.OriginalSource as DependencyObject) != null)
                return;

            var p = e.GetPosition(slider);

            const double leftPad = 10.0;
            const double rightPad = 10.0;

            double usable = Math.Max(1, slider.ActualWidth - leftPad - rightPad);
            double x = Math.Max(0, Math.Min(usable, p.X - leftPad));
            double ratio = x / usable;

            if (slider.IsDirectionReversed)
                ratio = 1.0 - ratio;

            double newValue = slider.Minimum + (slider.Maximum - slider.Minimum) * ratio;
            if (slider.SmallChange > 0)
                newValue = Math.Round(newValue / slider.SmallChange) * slider.SmallChange;

            // ★ 直接 TemplatedParent の DP を更新（これが強い）
            var dp = GetTargetProperty(slider);
            if (dp != null && slider.TemplatedParent is DependencyObject parent)
            {
                parent.SetValue(dp, newValue);
            }
            else
            {
                // フォールバック
                slider.Value = newValue;
            }

            e.Handled = true;
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t) return t;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
