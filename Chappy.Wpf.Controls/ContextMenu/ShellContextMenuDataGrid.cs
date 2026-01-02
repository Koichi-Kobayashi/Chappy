#nullable enable
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace Chappy.Wpf.Controls;

public class ShellContextMenuDataGrid : DataGrid
{
    private ShellContextMenuHost? _host;

    static ShellContextMenuDataGrid()
    {
        // 既定スタイルを使うならここ（不要なら消してOK）
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ShellContextMenuDataGrid),
            new FrameworkPropertyMetadata(typeof(ShellContextMenuDataGrid)));
    }

    public ShellContextMenuDataGrid()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;
    }

    // 背景メニュー用：今表示しているフォルダ
    public string? CurrentFolderPath
    {
        get => (string?)GetValue(CurrentFolderPathProperty);
        set => SetValue(CurrentFolderPathProperty, value);
    }
    public static readonly DependencyProperty CurrentFolderPathProperty =
        DependencyProperty.Register(nameof(CurrentFolderPath), typeof(string),
            typeof(ShellContextMenuDataGrid), new PropertyMetadata(null));

    // 行アイテムからフルパスを取る：既定は "FullPath"
    public string PathPropertyName
    {
        get => (string)GetValue(PathPropertyNameProperty);
        set => SetValue(PathPropertyNameProperty, value);
    }
    public static readonly DependencyProperty PathPropertyNameProperty =
        DependencyProperty.Register(nameof(PathPropertyName), typeof(string),
            typeof(ShellContextMenuDataGrid), new PropertyMetadata("FullPath"));

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Hostは必ず HWND がある状態で作る必要がある
        var window = Window.GetWindow(this);
        if (window == null) return;

        // SourceInitialized 済みである保証：Loaded時点なら通常OK
        // それでも hwnd 0 のケースがあるなら window.SourceInitialized で遅延生成
        if (new WindowInteropHelper(window).Handle == IntPtr.Zero)
        {
            window.SourceInitialized += (_, __) => EnsureHost(window);
        }
        else
        {
            EnsureHost(window);
        }
    }

    private void EnsureHost(Window window)
    {
        if (_host != null) return;
        _host = new ShellContextMenuHost(window);
        window.Closed += (_, __) => { _host?.Dispose(); _host = null; };
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _host?.Dispose();
        _host = null;
    }

    private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_host == null) return;

        var dep = e.OriginalSource as DependencyObject;
        var row = ItemsControl.ContainerFromElement(this, dep) as DataGridRow;

        // ===== 行の上：単体 or 複数 =====
        if (row != null)
        {
            // Explorer互換：未選択行を右クリックしたらその行だけ選択
            if (!row.IsSelected)
            {
                SelectedItems.Clear();
                row.IsSelected = true;
            }
            row.Focus();

            var screen = PointToScreen(e.GetPosition(this));

            // 複数選択
            if (SelectedItems.Count >= 2)
            {
                var paths = SelectedItems
                    .Cast<object>()
                    .Select(TryGetPathFromItem)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Cast<string>()
                    .ToList();

                if (paths.Count > 0)
                {
                    _host.ShowForItems(paths, screen);
                    e.Handled = true;
                    return;
                }
            }

            // 単体
            var one = TryGetPathFromItem(row.Item);
            if (!string.IsNullOrWhiteSpace(one))
            {
                _host.ShowForItem(one!, screen);
                e.Handled = true;
                return;
            }

            return; // パスが取れないなら何もしない
        }

        // ===== 余白：背景メニュー =====
        var folder = ResolveCurrentFolderPath();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            var screen2 = PointToScreen(e.GetPosition(this));
            _host.ShowForFolderBackground(folder!, screen2);
            e.Handled = true;
        }

    }

    private string? TryGetPathFromItem(object? item)
    {
        if (item == null) return null;

        // 1) IDictionary（匿名型/Expando等）対応
        if (item is System.Collections.IDictionary dict && dict.Contains(PathPropertyName))
            return dict[PathPropertyName]?.ToString();

        // 2) リフレクションでプロパティ取得（FullPath 等）
        var prop = item.GetType().GetProperty(PathPropertyName);
        if (prop == null) return null;

        return prop.GetValue(item)?.ToString();
    }

    private string? ResolveCurrentFolderPath()
    {
        if (!string.IsNullOrWhiteSpace(CurrentFolderPath))
            return CurrentFolderPath;

        // ItemsSource 先頭から推測
        var enumerable = ItemsSource as IEnumerable;
        if (enumerable == null) return null;

        foreach (var item in enumerable)
        {
            var path = TryGetPathFromItem(item);
            if (string.IsNullOrWhiteSpace(path)) continue;

            // アイテムがファイルなら親フォルダ、フォルダならそのフォルダ
            if (System.IO.Directory.Exists(path))
                return path;

            if (System.IO.File.Exists(path))
                return System.IO.Path.GetDirectoryName(path);

            // 存在チェックできない場合もとりあえず親を返す（ネットワーク等）
            return System.IO.Path.GetDirectoryName(path);
        }

        return null;
    }
}
