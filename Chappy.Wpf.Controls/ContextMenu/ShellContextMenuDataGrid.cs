#nullable enable
using System.Collections;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Chappy.Wpf.Controls.ContextMenu;

public class ShellContextMenuDataGrid : Chappy.Wpf.Controls.DataGrid.BoxSelectDataGrid
{
    private ShellContextMenuHost? _shellHost;
    private System.Windows.Controls.ContextMenu? _menu;

    private Point _lastOpenScreenPoint;

    public ShellContextMenuDataGrid()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        PreviewMouseRightButtonDown += OnRightButtonDown_SelectOnly;
        PreviewMouseRightButtonUp += OnRightButtonUp_OpenMenu;
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window == null) return;

        window.SourceInitialized += (_, __) => _shellHost ??= new ShellContextMenuHost(window);
        _shellHost ??= new ShellContextMenuHost(window);

        _menu ??= new System.Windows.Controls.ContextMenu();

        // Style をリソースから適用
        if (TryFindResource("Files.ContextMenuStyle") is Style style)
            _menu.Style = style;

        // 右クリック位置で出すための設定（ContextMenuService）
        ContextMenuService.SetPlacementTarget(_menu, this);
        ContextMenuService.SetPlacement(_menu, PlacementMode.AbsolutePoint);

        // 上段コマンド束を Tag に入れる
        _menu.Tag = new FilesMenuCommands
        {
            Cut = new RelayCommand(_ => { _menu.IsOpen = false; /* TODO */ }),
            Copy = new RelayCommand(_ => { _menu.IsOpen = false; /* TODO */ }),
            Paste = new RelayCommand(_ => { _menu.IsOpen = false; /* TODO */ }),
            Rename = new RelayCommand(_ => { _menu.IsOpen = false; /* TODO */ }),
            Share = new RelayCommand(_ => { _menu.IsOpen = false; /* TODO */ }),
            Delete = new RelayCommand(_ => { _menu.IsOpen = false; /* TODO */ }),
        };

    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_menu != null) _menu.IsOpen = false;
        _shellHost?.Dispose();
        _shellHost = null;
    }

    private void OnRightButtonDown_SelectOnly(object sender, MouseButtonEventArgs e)
    {
        // base.OnPreviewMouseRightButtonDown(e); ← いらない（イベントハンドラ内で呼ぶ意味が薄い）

        if (_menu == null) return;

        var row = FindAncestorRow(e.OriginalSource as DependencyObject);
        if (row == null) return;

        var item = row.Item;

        if (SelectedItems.Count > 1 && SelectedItems.Contains(item))
            return;

        SelectedItems.Clear();
        row.IsSelected = true;
        row.Focus();

        // ★ここで e.Handled = true にしない（メニュー表示の流れを壊しやすい）
    }

    private void OnRightButtonUp_OpenMenu(object sender, MouseButtonEventArgs e)
    {
        if (_menu == null) return;

        var pos = e.GetPosition(this);
        _lastOpenScreenPoint = PointToScreen(pos);

        var row = FindAncestorRow(e.OriginalSource as DependencyObject);

        IReadOnlyList<string> paths;
        Win11ContextKind kind;

        if (row != null)
        {
            paths = SelectedItems.Cast<object>()
                .Select(TryGetPathFromItem)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Cast<string>()
                .ToList();

            kind = ResolveKindFromSelection(paths);
        }
        else
        {
            kind = Win11ContextKind.Background;
            paths = Array.Empty<string>();
        }

        RebuildMenuItems(kind, paths);

        // ★座標ズレ対策：MousePoint を使う（これが強い）
        _menu.PlacementTarget = this;
        _menu.Placement = PlacementMode.MousePoint;
        _menu.IsOpen = true;

        e.Handled = true; // ここで止めるのはOK（あなたがメニューを出したので）
    }
    private void OnPreviewMouseRightButtonUp_OpenMenu(object sender, MouseButtonEventArgs e)
    {
        if (_menu == null) return;

        var pos = e.GetPosition(this);
        _lastOpenScreenPoint = PointToScreen(pos);

        var row = FindAncestorRow(e.OriginalSource as DependencyObject);

        IReadOnlyList<string> paths;
        Win11ContextKind kind;

        if (row != null)
        {
            paths = SelectedItems.Cast<object>()
                .Select(TryGetPathFromItem)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Cast<string>()
                .ToList();

            kind = ResolveKindFromSelection(paths);
        }
        else
        {
            kind = Win11ContextKind.Background;
            paths = Array.Empty<string>();
        }

        RebuildMenuItems(kind, paths);

        // ★座標ズレ対策：MousePoint を使う（これが強い）
        _menu.PlacementTarget = this;
        _menu.Placement = PlacementMode.MousePoint;
        _menu.IsOpen = true;

        e.Handled = true; // ここで止めるのはOK（あなたがメニューを出したので）
    }

    private static DataGridRow? FindAncestorRow(DependencyObject? d)
    {
        while (d != null)
        {
            if (d is DataGridRow row) return row;

            // Visual だけだと null になりがちなので Logical も見る
            var parent = LogicalTreeHelper.GetParent(d);
            if (parent != null)
            {
                d = parent;
                continue;
            }

            // FrameworkContentElement(例: Run) の親辿り
            if (d is FrameworkContentElement fce && fce.Parent is DependencyObject p2)
            {
                d = p2;
                continue;
            }

            d = VisualTreeHelper.GetParent(d);
        }
        return null;
    }

    private void RebuildMenuItems(Win11ContextKind kind, IReadOnlyList<string> paths)
    {
        if (_menu == null) return;

        _menu.Items.Clear();
        foreach (var obj in BuildFilesLikeMenuObjects(kind, paths))
            _menu.Items.Add(obj);
    }

    private Win11ContextKind ResolveKindFromSelection(IReadOnlyList<string> paths)
    {
        if (paths.Count == 1 && Directory.Exists(paths[0])) return Win11ContextKind.Folder;
        return Win11ContextKind.File;
    }

    private IEnumerable<object> BuildFilesLikeMenuObjects(Win11ContextKind kind, IReadOnlyList<string> paths)
    {
        // ここで “Filesに寄せたメニュー構成” を作る
        // ContextMenuResource.xaml 側は ItemContainerStyle を設定しない設計なので、
        // ここでは必ず MenuItem / Separator の実体を生成して返す。

        if (kind == Win11ContextKind.Background)
        {
            // ペインを閉じる
            yield return MI("ペインを閉じる", new RelayCommand(_ => { _menu!.IsOpen = false; }), gesture: "Alt+Ctrl+W");
            yield return new Separator();

            // レイアウト（サブメニュー）
            yield return MakeLayoutSubmenuItem();
            // 並べ替え（サブメニュー）
            yield return MakeSortSubmenuItem();
            // グループで表示（サブメニュー：例）
            yield return MakeGroupSubmenuItem();

            yield return MI("最新の情報に更新", new RelayCommand(_ => { _menu!.IsOpen = false; /* TODO */ }), gesture: "Ctrl+R");

            yield return new Separator();
            yield return MI("新規作成", new RelayCommand(_ => { /* TODO */ }));
            yield return MI("ショートカットを貼り付け", new RelayCommand(_ => { /* TODO */ _menu!.IsOpen = false; }));

            yield return new Separator();
            yield return MI("ターミナルで開く", new RelayCommand(_ => { _menu!.IsOpen = false; /* TODO */ }), gesture: "Ctrl+@");

            yield return new Separator();
            yield return MI("その他のオプションを確認", new RelayCommand(_ => ShowClassicBackground()));
            yield break;
        }

        // Folder / File 共通の例（ここはあなたのアプリ都合に合わせて増やす）
        yield return MI("開く", new RelayCommand(_ => { _menu!.IsOpen = false; /* TODO */ }));
        if (kind == Win11ContextKind.Folder)
        {
            yield return MI("新しいタブで開く", new RelayCommand(_ => { _menu!.IsOpen = false; /* TODO */ }));
            yield return MI("新しいウィンドウで開く", new RelayCommand(_ => { _menu!.IsOpen = false; /* TODO */ }), gesture: "Alt+Ctrl+Enter");
        }
        else
        {
            yield return MI("プログラムから開く", new RelayCommand(_ => { _menu!.IsOpen = false; /* TODO */ }));
        }

        yield return new Separator();
        yield return MI("アイテムのパスをコピー", new RelayCommand(_ => { _menu!.IsOpen = false; /* TODO */ }), gesture: "Ctrl+Shift+C");

        yield return new Separator();
        yield return MI("圧縮", new RelayCommand(_ => { _menu!.IsOpen = false; /* TODO */ }));
        yield return MI("送る", new RelayCommand(_ => { _menu!.IsOpen = false; /* TODO */ }));

        yield return new Separator();
        yield return MI("その他のオプションを確認", new RelayCommand(_ => ShowClassicForSelection(paths)));
    }

    private static MenuItem MI(string header, ICommand? command = null, string? gesture = null, bool isChecked = false, bool isEnabled = true, IEnumerable<object>? children = null)
    {
        var mi = new MenuItem
        {
            Header = header,
            Command = command,
            InputGestureText = gesture ?? string.Empty,
            IsChecked = isChecked,
            IsEnabled = isEnabled,
        };

        if (children != null)
        {
            foreach (var c in children)
                mi.Items.Add(c);
        }

        return mi;
    }

    private MenuItem MakeLayoutSubmenuItem()
    {
        return MI("レイアウト", children: new object[]
        {
            MI("詳細", new RelayCommand(_ => { _menu!.IsOpen = false; }), "Ctrl+Shift+1"),
            MI("一覧", new RelayCommand(_ => { _menu!.IsOpen = false; }), "Ctrl+Shift+2"),
            MI("カード", new RelayCommand(_ => { _menu!.IsOpen = false; }), "Ctrl+Shift+3"),
            MI("アイコン", new RelayCommand(_ => { _menu!.IsOpen = false; }), "Ctrl+Shift+4"),
            MI("カラム", new RelayCommand(_ => { _menu!.IsOpen = false; }), "Ctrl+Shift+5"),
            new Separator(),
            MI("レイアウトを自動的に選択", new RelayCommand(_ => { _menu!.IsOpen = false; }), "Ctrl+Shift+6", isChecked: true),
        });
    }

    private MenuItem MakeSortSubmenuItem()
    {
        return MI("並べ替え", children: new object[]
        {
            MI("名前", new RelayCommand(_ => { _menu!.IsOpen = false; }), isChecked: true),
            MI("更新日時", new RelayCommand(_ => { _menu!.IsOpen = false; })),
            MI("作成日時", new RelayCommand(_ => { _menu!.IsOpen = false; })),
            MI("種類", new RelayCommand(_ => { _menu!.IsOpen = false; })),
            MI("サイズ", new RelayCommand(_ => { _menu!.IsOpen = false; })),
            MI("タグ", new RelayCommand(_ => { _menu!.IsOpen = false; })),
            new Separator(),
            MI("昇順", new RelayCommand(_ => { _menu!.IsOpen = false; }), isChecked: true),
            MI("降順", new RelayCommand(_ => { _menu!.IsOpen = false; })),
        });
    }

    private MenuItem MakeGroupSubmenuItem()
    {
        return MI("グループで表示", children: new object[]
        {
            MI("なし", new RelayCommand(_ => { _menu!.IsOpen = false; }), isChecked: true),
            MI("名前", new RelayCommand(_ => { _menu!.IsOpen = false; })),
            MI("更新日時", new RelayCommand(_ => { _menu!.IsOpen = false; })),
            MI("作成日時", new RelayCommand(_ => { _menu!.IsOpen = false; })),
            MI("種類", new RelayCommand(_ => { _menu!.IsOpen = false; })),
            MI("サイズ", new RelayCommand(_ => { _menu!.IsOpen = false; })),
            MI("タグ", new RelayCommand(_ => { _menu!.IsOpen = false; })),
            new Separator(),
            MI("昇順", new RelayCommand(_ => { _menu!.IsOpen = false; }), isChecked: true),
            MI("降順", new RelayCommand(_ => { _menu!.IsOpen = false; }), isEnabled: false),
        });
    }

    private void ShowClassicForSelection(IReadOnlyList<string> paths)
    {
        _menu!.IsOpen = false;
        if (_shellHost == null) return;

        if (paths.Count == 1) _shellHost.ShowForItem(paths[0], _lastOpenScreenPoint);
        else if (paths.Count >= 2) _shellHost.ShowForItems(paths, _lastOpenScreenPoint);
    }

    private void ShowClassicBackground()
    {
        _menu!.IsOpen = false;
        if (_shellHost == null) return;

        var folder = ResolveCurrentFolderPath();
        if (!string.IsNullOrWhiteSpace(folder))
            _shellHost.ShowForFolderBackground(folder!, _lastOpenScreenPoint);
    }

    private string? ResolveCurrentFolderPath()
    {
        if (!string.IsNullOrWhiteSpace(CurrentFolderPath))
            return CurrentFolderPath;

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
