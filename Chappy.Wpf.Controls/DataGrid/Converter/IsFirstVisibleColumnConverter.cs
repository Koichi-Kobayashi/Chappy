#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Chappy.Wpf.Controls.DataGrid.Converter;

/// <summary>
/// DataGridCell が最初の可視列（DisplayIndex が最小）かどうかを判定するコンバーター。
/// DataGridCell を引数として受け取り、bool を返す。
/// </summary>
public class IsFirstVisibleColumnConverter : IValueConverter
{
    public static readonly IsFirstVisibleColumnConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DataGridCell cell)
            return false;

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

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static System.Windows.Controls.DataGrid? FindParentDataGrid(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is System.Windows.Controls.DataGrid dg)
                return dg;
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }
        return null;
    }
}
