#nullable enable
using System.Collections;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace Chappy.Wpf.Controls.ContextMenu;

public class ShellContextMenuDataGrid : DataGrid
{
    private ShellContextMenuHost? _shellHost;

    private readonly Popup _popup;
    private readonly Win11ContextMenuView _view;

    private Point _lastOpenScreenPoint;

    public ShellContextMenuDataGrid()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;

        _view = new Win11ContextMenuView();
        _popup = new Popup
        {
            AllowsTransparency = true,
            StaysOpen = true,                 // ★ false → true
            Placement = PlacementMode.AbsolutePoint,
            Child = _view
        };
        _popup.PlacementTarget = this;        // ★安定化
    }

    /// <summary>背景（余白）右クリックで使う「現在フォルダ」。無ければ ItemsSource の先頭から推測。</summary>
    public string? CurrentFolderPath
    {
        get => (string?)GetValue(CurrentFolderPathProperty);
        set => SetValue(CurrentFolderPathProperty, value);
    }
    public static readonly DependencyProperty CurrentFolderPathProperty =
        DependencyProperty.Register(nameof(CurrentFolderPath), typeof(string),
            typeof(ShellContextMenuDataGrid), new PropertyMetadata(null));

    /// <summary>行アイテムからパスを取るプロパティ名（既定 FullPath）</summary>
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
        // ShellContextMenuHost は Window の hwnd が必要（SourceInitialized 後）
        var window = Window.GetWindow(this);
        if (window == null) return;

        // Loaded 時点で hwnd が 0 の可能性もあるので保険
        window.SourceInitialized += (_, __) =>
        {
            if (_shellHost == null)
                _shellHost = new ShellContextMenuHost(window);
        };

        // すでに hwnd があるなら即作る
        if (_shellHost == null)
        {
            try { _shellHost = new ShellContextMenuHost(window); }
            catch { /* hwnd 0 の場合は SourceInitialized で作られる */ }
        }

        // コマンド設定（Viewにバインドされる）
        _view.CutCommand = new RelayCommand(_ => Cut());
        _view.CopyCommand = new RelayCommand(_ => Copy());
        _view.DeleteCommand = new RelayCommand(_ => DeleteToRecycleBin());
        _view.PropertiesCommand = new RelayCommand(_ => Properties());

        _view.MoreOptionsCommand = new RelayCommand(_ => ShowClassicMenu());

        if (window != null)
        {
            window.PreviewMouseDown += Window_PreviewMouseDown_ClosePopup;
            window.Deactivated += (_, __) => _popup.IsOpen = false;
        }

    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.PreviewMouseDown -= Window_PreviewMouseDown_ClosePopup;
        }

        _popup.IsOpen = false;
        _shellHost?.Dispose();
        _shellHost = null;
    }

    private void Window_PreviewMouseDown_ClosePopup(object sender, MouseButtonEventArgs e)
    {
        if (!_popup.IsOpen) return;

        // Popup内クリックなら閉じない
        if (_view.IsMouseOver) return;

        _popup.IsOpen = false;
    }

    private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_shellHost == null) return;

        var dep = e.OriginalSource as DependencyObject;
        var row = ItemsControl.ContainerFromElement(this, dep) as DataGridRow;

        var screen = PointToScreen(e.GetPosition(this));
        _lastOpenScreenPoint = screen;

        if (row != null)
        {
            // Explorer互換：右クリックした行が未選択ならその行だけ選択
            if (!row.IsSelected)
            {
                SelectedItems.Clear();
                row.IsSelected = true;
            }
            row.Focus();

            var paths = SelectedItems
                .Cast<object>()
                .Select(TryGetPathFromItem)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Cast<string>()
                .ToList();

            _view.IsBackground = false;
            _view.SelectedPaths = paths;

            e.Handled = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                OpenPopupAt(screen);
            }), DispatcherPriority.Input);

            return;
        }

        // 余白：背景メニュー
        _view.IsBackground = true;
        _view.SelectedPaths = Array.Empty<string>();

        OpenPopupAt(screen);
        e.Handled = true;
    }

    private void OpenPopupAt(Point screen)
    {
        _popup.HorizontalOffset = screen.X;
        _popup.VerticalOffset = screen.Y;
        _popup.IsOpen = true;
    }

    private void ShowClassicMenu()
    {
        _popup.IsOpen = false;

        if (_shellHost == null) return;

        if (_view.IsBackground)
        {
            var folder = ResolveCurrentFolderPath();
            if (!string.IsNullOrWhiteSpace(folder))
                _shellHost.ShowForFolderBackground(folder!, _lastOpenScreenPoint);

            return;
        }

        var paths = _view.SelectedPaths ?? Array.Empty<string>();
        if (paths.Count == 1)
        {
            _shellHost.ShowForItem(paths[0], _lastOpenScreenPoint);
        }
        else if (paths.Count >= 2)
        {
            _shellHost.ShowForItems(paths, _lastOpenScreenPoint);
        }
    }

    private string? ResolveCurrentFolderPath()
    {
        if (!string.IsNullOrWhiteSpace(CurrentFolderPath))
            return CurrentFolderPath;

        // ItemsSource 先頭から推測
        if (ItemsSource is not IEnumerable enumerable) return null;

        foreach (var item in enumerable)
        {
            var path = TryGetPathFromItem(item);
            if (string.IsNullOrWhiteSpace(path)) continue;

            if (Directory.Exists(path)) return path;
            return Path.GetDirectoryName(path);
        }

        return null;
    }

    private string? TryGetPathFromItem(object? item)
    {
        if (item == null) return null;

        // IDictionary 対応（Expando等）
        if (item is IDictionary dict && dict.Contains(PathPropertyName))
            return dict[PathPropertyName]?.ToString();

        var prop = item.GetType().GetProperty(PathPropertyName);
        return prop?.GetValue(item)?.ToString();
    }

    // ======= 自前実装（最小：Cut/Copy/Delete/Properties） =======

    private IReadOnlyList<string> GetValidSelectedPaths()
    {
        var paths = _view.SelectedPaths ?? Array.Empty<string>();
        return paths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
    }

    private void Copy()
    {
        var paths = GetValidSelectedPaths();
        if (paths.Count == 0) return;

        var dropList = new System.Collections.Specialized.StringCollection();
        foreach (var p in paths) dropList.Add(p);

        Clipboard.SetFileDropList(dropList);
        _popup.IsOpen = false;
    }

    private void Cut()
    {
        var paths = GetValidSelectedPaths();
        if (paths.Count == 0) return;

        var dropList = new System.Collections.Specialized.StringCollection();
        foreach (var p in paths) dropList.Add(p);

        Clipboard.SetFileDropList(dropList);

        // Move を示す DropEffect（Explorer互換で使われがち）
        var data = Clipboard.GetDataObject();
        if (data is DataObject dobj)
        {
            dobj.SetData("Preferred DropEffect", new MemoryStream(new byte[] { 2, 0, 0, 0 }));
            Clipboard.SetDataObject(dobj, true);
        }

        _popup.IsOpen = false;
    }

    private void DeleteToRecycleBin()
    {
        var paths = GetValidSelectedPaths();
        if (paths.Count == 0) return;

        // WinForms依存ゼロ（Microsoft.VisualBasic を使う）
        // ※ 参照が無い場合：プロジェクトに Microsoft.VisualBasic を追加
        try
        {
            foreach (var p in paths)
            {
                if (Directory.Exists(p))
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                        p,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
                else if (File.Exists(p))
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                        p,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
            }
        }
        catch { }

        _popup.IsOpen = false;
    }

    private void Properties()
    {
        var paths = GetValidSelectedPaths();
        if (paths.Count != 1) return;

        // “プロパティ”はクラシックに委譲してもOK。ここは最小なのでクラシックへ寄せる。
        _popup.IsOpen = false;
        _shellHost?.ShowForItem(paths[0], _lastOpenScreenPoint);
    }
}
