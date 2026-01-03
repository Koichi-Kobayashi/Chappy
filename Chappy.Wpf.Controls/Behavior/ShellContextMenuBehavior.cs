#nullable enable
using Chappy.Wpf.Controls.ContextMenu;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

// WPFのDataGrid型と、あなたの namespace Chappy.Wpf.Controls.DataGrid の衝突回避
using WpfDataGrid = System.Windows.Controls.DataGrid;

namespace Chappy.Wpf.Controls.Behaviors;

public static class ShellContextMenuBehavior
{
    // =========================
    // IsEnabled
    // =========================
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ShellContextMenuBehavior),
            new PropertyMetadata(false, OnChanged));

    public static void SetIsEnabled(DependencyObject d, bool v) => d.SetValue(IsEnabledProperty, v);
    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);

    // =========================
    // CurrentFolderPath (Optional)
    // =========================
    public static readonly DependencyProperty CurrentFolderPathProperty =
        DependencyProperty.RegisterAttached(
            "CurrentFolderPath",
            typeof(string),
            typeof(ShellContextMenuBehavior),
            new PropertyMetadata(null));

    public static void SetCurrentFolderPath(DependencyObject d, string? v) => d.SetValue(CurrentFolderPathProperty, v);
    public static string? GetCurrentFolderPath(DependencyObject d) => (string?)d.GetValue(CurrentFolderPathProperty);

    // =========================
    // PathPropertyName
    // =========================
    public static readonly DependencyProperty PathPropertyNameProperty =
        DependencyProperty.RegisterAttached(
            "PathPropertyName",
            typeof(string),
            typeof(ShellContextMenuBehavior),
            new PropertyMetadata("FullPath"));

    public static void SetPathPropertyName(DependencyObject d, string v) => d.SetValue(PathPropertyNameProperty, v);
    public static string GetPathPropertyName(DependencyObject d) => (string)d.GetValue(PathPropertyNameProperty);

    // =========================
    // 内部状態（DataGridごと）
    // =========================
    private sealed class State
    {
        public ShellContextMenuHost? Host;
        public System.Windows.Controls.ContextMenu? Menu;
        public Point LastOpenScreenPoint;
    }

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(State),
            typeof(ShellContextMenuBehavior),
            new PropertyMetadata(null));

    private static State GetState(WpfDataGrid g)
    {
        if (g.GetValue(StateProperty) is not State s)
        {
            s = new State();
            g.SetValue(StateProperty, s);
        }
        return s;
    }

    // =========================
    // attach/detach
    // =========================
    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not WpfDataGrid grid) return;

        if ((bool)e.NewValue)
        {
            grid.Loaded += OnLoaded;
            grid.Unloaded += OnUnloaded;

            // Down：選択だけ
            grid.PreviewMouseRightButtonDown += OnRightDown_SelectOnly;
            // Up：メニューを開く（安定）
            grid.PreviewMouseRightButtonUp += OnRightUp_OpenMenu;
        }
        else
        {
            grid.Loaded -= OnLoaded;
            grid.Unloaded -= OnUnloaded;

            grid.PreviewMouseRightButtonDown -= OnRightDown_SelectOnly;
            grid.PreviewMouseRightButtonUp -= OnRightUp_OpenMenu;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfDataGrid grid) return;

        var s = GetState(grid);

        var window = Window.GetWindow(grid);
        if (window == null) return;

        s.Host ??= new ShellContextMenuHost(window);

        s.Menu ??= new System.Windows.Controls.ContextMenu();

        // Style をリソースから適用
        if (grid.TryFindResource("Files.ContextMenuStyle") is Style style)
            s.Menu.Style = style;

        // 右クリック位置で出す（DPI/座標ズレに強い）
        s.Menu.PlacementTarget = grid;
        s.Menu.Placement = PlacementMode.MousePoint;

        // 上段コマンド束を Tag に入れる（既存通り）
        s.Menu.Tag = new FilesMenuCommands
        {
            Cut = new RelayCommand(_ => { s.Menu!.IsOpen = false; /* TODO */ }),
            Copy = new RelayCommand(_ => { s.Menu!.IsOpen = false; /* TODO */ }),
            Paste = new RelayCommand(_ => { s.Menu!.IsOpen = false; /* TODO */ }),
            Rename = new RelayCommand(_ => { s.Menu!.IsOpen = false; /* TODO */ }),
            Share = new RelayCommand(_ => { s.Menu!.IsOpen = false; /* TODO */ }),
            Delete = new RelayCommand(_ => { s.Menu!.IsOpen = false; /* TODO */ }),
        };
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfDataGrid grid) return;

        var s = GetState(grid);
        if (s.Menu != null) s.Menu.IsOpen = false;

        s.Host?.Dispose();
        s.Host = null;
    }

    // =========================
    // 右クリック：Downは選択だけ（Handledしない）
    // =========================
    private static void OnRightDown_SelectOnly(object sender, MouseButtonEventArgs e)
    {
        if (sender is not WpfDataGrid grid) return;
        var s = GetState(grid);
        if (s.Menu == null) return;

        var row = FindAncestorRow(e.OriginalSource as DependencyObject);
        if (row == null) return;

        var item = row.Item;

        // 複数選択中で、その中を右クリックしたら維持（Explorer風）
        if (grid.SelectedItems.Count > 1 && grid.SelectedItems.Contains(item))
            return;

        grid.SelectedItems.Clear();
        row.IsSelected = true;
        row.Focus();
    }

    // =========================
    // 右クリック：Upでメニューを開く（安定）
    // =========================
    private static void OnRightUp_OpenMenu(object sender, MouseButtonEventArgs e)
    {
        if (sender is not WpfDataGrid grid) return;
        var s = GetState(grid);
        if (s.Menu == null) return;

        s.LastOpenScreenPoint = grid.PointToScreen(e.GetPosition(grid));

        var row = FindAncestorRow(e.OriginalSource as DependencyObject);

        IReadOnlyList<string> paths;
        Win11ContextKind kind;

        if (row != null)
        {
            paths = grid.SelectedItems
                .Cast<object>()
                .Select(o => TryGetPathFromItem(o, GetPathPropertyName(grid)))
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

        RebuildMenuItems(grid, s, kind, paths);

        // MousePoint表示（座標ズレに強い）
        s.Menu.PlacementTarget = grid;
        s.Menu.Placement = PlacementMode.MousePoint;
        s.Menu.IsOpen = true;

        e.Handled = true;
    }

    // =========================
    // Row探索（Visual/Logical/Run対応）
    // =========================
    private static DataGridRow? FindAncestorRow(DependencyObject? d)
    {
        while (d != null)
        {
            if (d is DataGridRow row) return row;

            var parent = LogicalTreeHelper.GetParent(d);
            if (parent != null)
            {
                d = parent;
                continue;
            }

            if (d is FrameworkContentElement fce && fce.Parent is DependencyObject p2)
            {
                d = p2;
                continue;
            }

            d = VisualTreeHelper.GetParent(d);
        }
        return null;
    }

    // =========================
    // ここから「既存メニュー移植」
    // =========================
    private static void RebuildMenuItems(WpfDataGrid grid, State s, Win11ContextKind kind, IReadOnlyList<string> paths)
    {
        if (s.Menu == null) return;

        s.Menu.Items.Clear();
        foreach (var obj in BuildFilesLikeMenuObjects(grid, s, kind, paths))
            s.Menu.Items.Add(obj);
    }

    private static Win11ContextKind ResolveKindFromSelection(IReadOnlyList<string> paths)
    {
        if (paths.Count == 1 && Directory.Exists(paths[0])) return Win11ContextKind.Folder;
        return Win11ContextKind.File;
    }

    private static IEnumerable<object> BuildFilesLikeMenuObjects(WpfDataGrid grid, State s, Win11ContextKind kind, IReadOnlyList<string> paths)
    {
        // ShellContextMenuDataGrid の BuildFilesLikeMenuObjects をそのまま移植
        if (kind == Win11ContextKind.Background)
        {
            yield return MI(s, "ペインを閉じる", new RelayCommand(_ => { s.Menu!.IsOpen = false; }), gesture: "Alt+Ctrl+W");
            yield return new Separator();

            yield return MakeLayoutSubmenuItem(s);
            yield return MakeSortSubmenuItem(s);
            yield return MakeGroupSubmenuItem(s);

            yield return MI(s, "最新の情報に更新", new RelayCommand(_ => { s.Menu!.IsOpen = false; /* TODO */ }), gesture: "Ctrl+R");

            yield return new Separator();
            yield return MI(s, "新規作成", new RelayCommand(_ => { /* TODO */ }));
            yield return MI(s, "ショートカットを貼り付け", new RelayCommand(_ => { /* TODO */ s.Menu!.IsOpen = false; }));

            yield return new Separator();
            yield return MI(s, "ターミナルで開く", new RelayCommand(_ => { s.Menu!.IsOpen = false; /* TODO */ }), gesture: "Ctrl+@");

            yield return new Separator();
            yield return MI(s, "その他のオプションを確認", new RelayCommand(_ => ShowClassicBackground(grid, s)));
            yield break;
        }

        // Folder / File 共通
        yield return MI(s, "開く", new RelayCommand(_ => { s.Menu!.IsOpen = false; /* TODO */ }));
        if (kind == Win11ContextKind.Folder)
        {
            yield return MI(s, "新しいタブで開く", new RelayCommand(_ => { s.Menu!.IsOpen = false; /* TODO */ }));
            yield return MI(s, "新しいウィンドウで開く", new RelayCommand(_ => { s.Menu!.IsOpen = false; /* TODO */ }), gesture: "Alt+Ctrl+Enter");
        }
        else
        {
            yield return MI(s, "プログラムから開く", new RelayCommand(_ => { s.Menu!.IsOpen = false; /* TODO */ }));
        }

        yield return new Separator();
        yield return MI(s, "アイテムのパスをコピー", new RelayCommand(_ => { s.Menu!.IsOpen = false; /* TODO */ }), gesture: "Ctrl+Shift+C");

        yield return new Separator();
        yield return MI(s, "圧縮", new RelayCommand(_ => { s.Menu!.IsOpen = false; /* TODO */ }));
        yield return MI(s, "送る", new RelayCommand(_ => { s.Menu!.IsOpen = false; /* TODO */ }));

        yield return new Separator();
        yield return MI(s, "その他のオプションを確認", new RelayCommand(_ => ShowClassicForSelection(grid, s, paths)));
    }

    private static MenuItem MI(
        State s,
        string header,
        ICommand? command = null,
        string? gesture = null,
        bool isChecked = false,
        bool isEnabled = true,
        IEnumerable<object>? children = null)
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

    private static MenuItem MakeLayoutSubmenuItem(State s)
    {
        return MI(s, "レイアウト", children: new object[]
        {
            MI(s, "詳細",  new RelayCommand(_ => { s.Menu!.IsOpen = false; }), "Ctrl+Shift+1"),
            MI(s, "一覧",  new RelayCommand(_ => { s.Menu!.IsOpen = false; }), "Ctrl+Shift+2"),
            MI(s, "カード",new RelayCommand(_ => { s.Menu!.IsOpen = false; }), "Ctrl+Shift+3"),
            MI(s, "アイコン", new RelayCommand(_ => { s.Menu!.IsOpen = false; }), "Ctrl+Shift+4"),
            MI(s, "カラム", new RelayCommand(_ => { s.Menu!.IsOpen = false; }), "Ctrl+Shift+5"),
            new Separator(),
            MI(s, "レイアウトを自動的に選択", new RelayCommand(_ => { s.Menu!.IsOpen = false; }), "Ctrl+Shift+6", isChecked: true),
        });
    }

    private static MenuItem MakeSortSubmenuItem(State s)
    {
        return MI(s, "並べ替え", children: new object[]
        {
            MI(s, "名前",   new RelayCommand(_ => { s.Menu!.IsOpen = false; }), isChecked: true),
            MI(s, "更新日時", new RelayCommand(_ => { s.Menu!.IsOpen = false; })),
            MI(s, "作成日時", new RelayCommand(_ => { s.Menu!.IsOpen = false; })),
            MI(s, "種類",   new RelayCommand(_ => { s.Menu!.IsOpen = false; })),
            MI(s, "サイズ", new RelayCommand(_ => { s.Menu!.IsOpen = false; })),
            MI(s, "タグ",   new RelayCommand(_ => { s.Menu!.IsOpen = false; })),
            new Separator(),
            MI(s, "昇順",   new RelayCommand(_ => { s.Menu!.IsOpen = false; }), isChecked: true),
            MI(s, "降順",   new RelayCommand(_ => { s.Menu!.IsOpen = false; })),
        });
    }

    private static MenuItem MakeGroupSubmenuItem(State s)
    {
        return MI(s, "グループで表示", children: new object[]
        {
            MI(s, "なし",   new RelayCommand(_ => { s.Menu!.IsOpen = false; }), isChecked: true),
            MI(s, "名前",   new RelayCommand(_ => { s.Menu!.IsOpen = false; })),
            MI(s, "更新日時", new RelayCommand(_ => { s.Menu!.IsOpen = false; })),
            MI(s, "作成日時", new RelayCommand(_ => { s.Menu!.IsOpen = false; })),
            MI(s, "種類",   new RelayCommand(_ => { s.Menu!.IsOpen = false; })),
            MI(s, "サイズ", new RelayCommand(_ => { s.Menu!.IsOpen = false; })),
            MI(s, "タグ",   new RelayCommand(_ => { s.Menu!.IsOpen = false; })),
            new Separator(),
            MI(s, "昇順",   new RelayCommand(_ => { s.Menu!.IsOpen = false; }), isChecked: true),
            MI(s, "降順",   new RelayCommand(_ => { s.Menu!.IsOpen = false; }), isEnabled: false),
        });
    }

    private static void ShowClassicForSelection(WpfDataGrid grid, State s, IReadOnlyList<string> paths)
    {
        s.Menu!.IsOpen = false;
        if (s.Host == null) return;

        if (paths.Count == 1) s.Host.ShowForItem(paths[0], s.LastOpenScreenPoint);
        else if (paths.Count >= 2) s.Host.ShowForItems(paths, s.LastOpenScreenPoint);
    }

    private static void ShowClassicBackground(WpfDataGrid grid, State s)
    {
        s.Menu!.IsOpen = false;
        if (s.Host == null) return;

        var folder = ResolveCurrentFolderPath(grid);
        if (!string.IsNullOrWhiteSpace(folder))
            s.Host.ShowForFolderBackground(folder!, s.LastOpenScreenPoint);
    }

    private static string? ResolveCurrentFolderPath(WpfDataGrid grid)
    {
        var fromAttached = GetCurrentFolderPath(grid);
        if (!string.IsNullOrWhiteSpace(fromAttached))
            return fromAttached;

        if (grid.ItemsSource is not IEnumerable enumerable) return null;

        var propName = GetPathPropertyName(grid);

        foreach (var item in enumerable)
        {
            var path = TryGetPathFromItem(item, propName);
            if (string.IsNullOrWhiteSpace(path)) continue;

            if (Directory.Exists(path)) return path;
            return Path.GetDirectoryName(path);
        }
        return null;
    }

    private static string? TryGetPathFromItem(object? item, string propName)
    {
        if (item == null) return null;

        if (item is IDictionary dict && dict.Contains(propName))
            return dict[propName]?.ToString();

        var prop = item.GetType().GetProperty(propName);
        return prop?.GetValue(item)?.ToString();
    }
}
