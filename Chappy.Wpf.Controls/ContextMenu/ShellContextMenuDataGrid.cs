#nullable enable
using System.Collections;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

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
            StaysOpen = true,                  // ★ 重要：押してる間だけ問題を根治
            Placement = PlacementMode.AbsolutePoint,
            Child = _view
        };
        _popup.PlacementTarget = this;
    }

    public string? CurrentFolderPath
    {
        get => (string?)GetValue(CurrentFolderPathProperty);
        set => SetValue(CurrentFolderPathProperty, value);
    }
    public static readonly DependencyProperty CurrentFolderPathProperty =
        DependencyProperty.Register(nameof(CurrentFolderPath), typeof(string),
            typeof(ShellContextMenuDataGrid), new PropertyMetadata(null));

    public string PathPropertyName
    {
        get => (string)GetValue(PathPropertyNameProperty);
        set => SetValue(PathPropertyNameProperty, value);
    }
    public static readonly DependencyProperty PathPropertyNameProperty =
        DependencyProperty.Register(nameof(PathPropertyName), typeof(string),
            typeof(ShellContextMenuDataGrid), new PropertyMetadata("FullPath"));

    private Window? _window;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _window = Window.GetWindow(this);
        if (_window == null) return;

        _window.SourceInitialized += (_, __) =>
        {
            _shellHost ??= new ShellContextMenuHost(_window);
        };
        _shellHost ??= new ShellContextMenuHost(_window);

        // 外側クリックで閉じる
        _window.PreviewMouseDown += Window_PreviewMouseDown_ClosePopup;
        _window.Deactivated += (_, __) => _popup.IsOpen = false;

        // View コマンド（最小：実行は “その他” へ逃がしてもOK）
        _view.MoreOptionsCommand = new RelayCommand(_ => ShowClassicMenu());

        // まずは Filesっぽい“見た目”重視：上段は未実装でもOK（必要なら後で実装）
        _view.CutCommand = new RelayCommand(_ => { _popup.IsOpen = false; /* TODO */ });
        _view.CopyCommand = new RelayCommand(_ => { _popup.IsOpen = false; /* TODO */ });
        _view.PasteCommand = new RelayCommand(_ => { _popup.IsOpen = false; /* TODO */ });
        _view.RenameCommand = new RelayCommand(_ => { _popup.IsOpen = false; /* TODO */ });
        _view.ShareCommand = new RelayCommand(_ => { _popup.IsOpen = false; /* TODO */ });
        _view.DeleteCommand = new RelayCommand(_ => { _popup.IsOpen = false; /* TODO */ });
        _view.PropertiesCommand = new RelayCommand(_ => { _popup.IsOpen = false; ShowClassicMenu(); });

        // “開く”系はあなたのアプリ動作に繋ぎやすいようイベントで逃がしてある
        _view.OpenRequested += (_, __) => { _popup.IsOpen = false; /* TODO: open */ };
        _view.OpenNewTabRequested += (_, __) => { _popup.IsOpen = false; /* TODO */ };
        _view.OpenNewWindowRequested += (_, __) => { _popup.IsOpen = false; /* TODO */ };
        _view.OpenNewPaneRequested += (_, __) => { _popup.IsOpen = false; /* TODO */ };
        _view.CopyPathRequested += (_, __) => { _popup.IsOpen = false; /* TODO */ };
        _view.RefreshRequested += (_, __) => { _popup.IsOpen = false; /* TODO */ };
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _popup.IsOpen = false;

        if (_window != null)
        {
            _window.PreviewMouseDown -= Window_PreviewMouseDown_ClosePopup;
            _window = null;
        }

        _shellHost?.Dispose();
        _shellHost = null;
    }

    private void Window_PreviewMouseDown_ClosePopup(object sender, MouseButtonEventArgs e)
    {
        if (!_popup.IsOpen) return;
        if (_view.IsMouseOver) return; // Popup内は閉じない
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

            var kind = ResolveKindFromSelection(paths);
            _view.Kind = kind;
            _view.SelectedPaths = paths;

            OpenPopupAt(screen);
            e.Handled = true;
            return;
        }

        // 余白＝Background
        _view.Kind = Win11ContextKind.Background;
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

        if (_view.Kind == Win11ContextKind.Background)
        {
            var folder = ResolveCurrentFolderPath();
            if (!string.IsNullOrWhiteSpace(folder))
                _shellHost.ShowForFolderBackground(folder!, _lastOpenScreenPoint);
            return;
        }

        var paths = _view.SelectedPaths ?? Array.Empty<string>();
        if (paths.Count == 1)
            _shellHost.ShowForItem(paths[0], _lastOpenScreenPoint);
        else if (paths.Count >= 2)
            _shellHost.ShowForItems(paths, _lastOpenScreenPoint);
    }

    private Win11ContextKind ResolveKindFromSelection(IReadOnlyList<string> paths)
    {
        // Files風：フォルダ右クリック / ファイル右クリックを分ける
        if (paths.Count == 1)
        {
            var p = paths[0];
            if (Directory.Exists(p)) return Win11ContextKind.Folder;
            return Win11ContextKind.File;
        }
        // 複数は基本「ファイル扱い」に寄せ（必要なら後でフォルダ混在も判定）
        return Win11ContextKind.File;
    }

    private string? ResolveCurrentFolderPath()
    {
        if (!string.IsNullOrWhiteSpace(CurrentFolderPath))
            return CurrentFolderPath;

        // ItemsSource先頭から推測
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

        if (item is IDictionary dict && dict.Contains(PathPropertyName))
            return dict[PathPropertyName]?.ToString();

        var prop = item.GetType().GetProperty(PathPropertyName);
        return prop?.GetValue(item)?.ToString();
    }

}
