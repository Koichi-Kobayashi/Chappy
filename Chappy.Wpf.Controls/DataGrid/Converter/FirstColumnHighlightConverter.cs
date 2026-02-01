#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Chappy.Wpf.Controls.DataGrid.Converter;

/// <summary>
/// 1列目選択モードのときに、選択されたセルにハイライト背景を適用するかどうかを判定するコンバーター。
/// </summary>
/// <remarks>
/// MultiBinding で以下の値を渡す：
/// [0] DataGridCell 自身 (RelativeSource Self)
/// [1] IsRowSelectionFirstColumnOnly (bool) - 1列目選択モードかどうか
/// [2] DataGridRow.IsSelected (bool) - 親行が選択されているか
/// 
/// 戻り値: ハイライト背景の Brush（条件を満たさない場合は Transparent）
/// </remarks>
public class FirstColumnHighlightConverter : IMultiValueConverter
{
    public static readonly FirstColumnHighlightConverter Instance = new();

    /// <summary>
    /// ハイライト時に使用するブラシ。XAML で ConverterParameter として渡すか、
    /// このプロパティをオーバーライドするか、静的プロパティを設定する。
    /// </summary>
    public static Brush? DefaultHighlightBrush { get; set; }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // 引数の検証
        if (values.Length < 3)
            return Brushes.Transparent;

        // 値の取得
        var cell = values[0] as DataGridCell;
        var isFirstColumnOnly = values[1] is true;
        var isRowSelected = values[2] is true;

        // 1列目選択モードでない、または行が選択されていない場合は透明
        if (!isFirstColumnOnly || !isRowSelected || cell == null)
            return Brushes.Transparent;

        // 1列目かどうかを判定
        if (!IsFirstVisibleColumn(cell))
            return Brushes.Transparent;

        // ハイライトブラシを返す
        if (parameter is Brush paramBrush)
            return paramBrush;

        return DefaultHighlightBrush ?? Brushes.Transparent;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static bool IsFirstVisibleColumn(DataGridCell cell)
    {
        var column = cell.Column;
        if (column == null)
            return false;

        // 親の DataGrid を取得
        var dataGrid = FindParentDataGrid(cell);
        if (dataGrid == null)
            return false;

        // 可視列の中で最小の DisplayIndex を取得
        var minDisplayIndex = dataGrid.Columns
            .Where(c => c.Visibility == Visibility.Visible)
            .Min(c => (int?)c.DisplayIndex);

        if (minDisplayIndex == null)
            return false;

        return column.DisplayIndex == minDisplayIndex.Value;
    }

    private static System.Windows.Controls.DataGrid? FindParentDataGrid(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is System.Windows.Controls.DataGrid dg)
                return dg;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }
}

/// <summary>
/// 1列目選択モードのとき、行全体の背景色を表示するかどうかを判定するコンバーター。
/// </summary>
/// <remarks>
/// MultiBinding で以下の値を渡す：
/// [0] IsRowSelectionFirstColumnOnly (bool) - 1列目選択モードかどうか
/// [1] DataGridRow.IsSelected (bool) - 行が選択されているか
/// 
/// 戻り値: 行背景の Brush（1列目選択モードの場合は Transparent）
/// </remarks>
public class RowHighlightConverter : IMultiValueConverter
{
    public static readonly RowHighlightConverter Instance = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return Brushes.Transparent;

        var isFirstColumnOnly = values[0] is true;
        var isRowSelected = values[1] is true;

        // 選択されていない場合は透明
        if (!isRowSelected)
            return Brushes.Transparent;

        // 1列目選択モードの場合は行全体のハイライトを無効化
        if (isFirstColumnOnly)
            return Brushes.Transparent;

        // 通常モードの場合はハイライトブラシを返す
        if (parameter is Brush paramBrush)
            return paramBrush;

        return Brushes.Transparent;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
