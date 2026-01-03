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


    /// <summary>
    /// CutCommand 付属の依存プロパティを識別します。これにより、XAML 内の UI 要素にカットコマンドをバインドできます。
    /// </summary>
    /// <remarks>
    /// このプロパティにより、開発者はUI要素にICommand実装をアタッチでき、
    /// データバインディングを通じてカット操作のカスタム処理が可能になります。コンテキストメニューや
    /// キーボードによるカット操作がビューモデルのコマンドを呼び出す必要があるシナリオで一般的に使用されます。
    /// </remarks>
    public static readonly DependencyProperty CutCommandProperty =
        DependencyProperty.RegisterAttached(
            "CutCommand",
            typeof(ICommand),
            typeof(ShellContextMenuBehavior),
            new PropertyMetadata(null));
    /// <summary>
    /// 指定された依存オブジェクトに関連付けられるCutコマンドを設定します。
    /// </summary>
    /// <param name="d">cutコマンドを設定する依存オブジェクト。null は指定できません。</param>
    /// <param name="v">依存オブジェクトに関連付けるコマンド、またはnullで既存のコマンドをクリアする。</param>
    public static void SetCutCommand(DependencyObject d, ICommand? v) => d.SetValue(CutCommandProperty, v);
    /// <summary>
    /// 指定された依存オブジェクトに関連付けられたCutコマンドを取得します。
    /// </summary>
    /// <param name="d">cutコマンドを設定する依存オブジェクト。null は指定できません。</param>
    /// <returns>Cutコマンドが設定されている場合にそれを表すインスタンス、そうでない場合は、null</returns>
    public static ICommand? GetCutCommand(DependencyObject d) => (ICommand?)d.GetValue(CutCommandProperty);

    /// <summary>
    /// CopyCommand 付属の依存プロパティを識別します。これにより、XAML 内の UI 要素にコピーコマンドをバインドできます。
    /// </summary>
    /// <remarks>
    /// このプロパティにより、開発者はUI要素にICommand実装をアタッチでき、
    /// データバインディングを通じてコピー操作のカスタム処理が可能になります。コンテキストメニューや
    /// キーボードによるコピー操作がビューモデルのコマンドを呼び出す必要があるシナリオで一般的に使用されます。
    /// </remarks>
    public static readonly DependencyProperty CopyCommandProperty =
        DependencyProperty.RegisterAttached(
            "CopyCommand",
            typeof(ICommand),
            typeof(ShellContextMenuBehavior),
            new PropertyMetadata(null));
    /// <summary>
    /// 指定された依存オブジェクトに関連付けられるCopyコマンドを設定します。
    /// </summary>
    /// <param name="d">Copyコマンドを設定する依存オブジェクト。null は指定できません。</param>
    /// <param name="v">依存オブジェクトに関連付けるコマンド、またはnullで既存のコマンドをクリアする。</param>
    public static void SetCopyCommand(DependencyObject d, ICommand? v) => d.SetValue(CopyCommandProperty, v);
    /// <summary>
    /// 指定された依存オブジェクトに関連付けられたCopyコマンドを取得します。
    /// </summary>
    /// <param name="d">Copyコマンドを設定する依存オブジェクト。null は指定できません。</param>
    /// <returns>Copyコマンドが設定されている場合にそれを表すインスタンス、そうでない場合は、null</returns>
    public static ICommand? GetCopyCommand(DependencyObject d) => (ICommand?)d.GetValue(CopyCommandProperty);

    /// <summary>
    /// PasteCommand 付属の依存プロパティを識別します。これにより、XAML 内の UI 要素にペーストコマンドをバインドできます。
    /// </summary>
    /// <remarks>
    /// このプロパティにより、開発者はUI要素にICommand実装をアタッチでき、
    /// データバインディングを通じてペースト操作のカスタム処理が可能になります。コンテキストメニューや
    /// キーボードによるペースト操作がビューモデルのコマンドを呼び出す必要があるシナリオで一般的に使用されます。
    /// </remarks>
    public static readonly DependencyProperty PasteCommandProperty =
        DependencyProperty.RegisterAttached(
            "PasteCommand",
            typeof(ICommand),
            typeof(ShellContextMenuBehavior),
            new PropertyMetadata(null));
    /// <summary>
    /// 指定された依存オブジェクトに関連付けられるPasteコマンドを設定します。
    /// </summary>
    /// <param name="d">Pasteコマンドを設定する依存オブジェクト。null は指定できません。</param>
    /// <param name="v">依存オブジェクトに関連付けるコマンド、またはnullで既存のコマンドをクリアする。</param>
    public static void SetPasteCommand(DependencyObject d, ICommand? v) => d.SetValue(PasteCommandProperty, v);
    /// <summary>
    /// 指定された依存オブジェクトに関連付けられたPasteコマンドを取得します。
    /// </summary>
    /// <param name="d">Pasteコマンドを設定する依存オブジェクト。null は指定できません。</param>
    /// <returns>Pasteコマンドが設定されている場合にそれを表すインスタンス、そうでない場合は、null</returns>
    public static ICommand? GetPasteCommand(DependencyObject d) => (ICommand?)d.GetValue(PasteCommandProperty);

    /// <summary>
    /// RenameCommand 付属の依存プロパティを識別します。これにより、XAML 内の UI 要素に名前変更コマンドをバインドできます。
    /// </summary>
    /// <remarks>
    /// このプロパティにより、開発者はUI要素にICommand実装をアタッチでき、
    /// データバインディングを通じて名前変更操作のカスタム処理が可能になります。コンテキストメニューや
    /// キーボードによる名前変更操作がビューモデルのコマンドを呼び出す必要があるシナリオで一般的に使用されます。
    /// </remarks>
    public static readonly DependencyProperty RenameCommandProperty =
        DependencyProperty.RegisterAttached(
            "RenameCommand",
            typeof(ICommand),
            typeof(ShellContextMenuBehavior),
            new PropertyMetadata(null));
    /// <summary>
    /// 指定された依存オブジェクトに関連付けられるRenameコマンドを設定します。
    /// </summary>
    /// <param name="d">Renameコマンドを設定する依存オブジェクト。null は指定できません。</param>
    /// <param name="v">依存オブジェクトに関連付けるコマンド、またはnullで既存のコマンドをクリアする。</param>
    public static void SetRenameCommand(DependencyObject d, ICommand? v) => d.SetValue(RenameCommandProperty, v);
    /// <summary>
    /// 指定された依存オブジェクトに関連付けられたRenameコマンドを取得します。
    /// </summary>
    /// <param name="d">Renameコマンドを設定する依存オブジェクト。null は指定できません。</param>
    /// <returns>Renameコマンドが設定されている場合にそれを表すインスタンス、そうでない場合は、null</returns>
    public static ICommand? GetRenameCommand(DependencyObject d) => (ICommand?)d.GetValue(RenameCommandProperty);

    /// <summary>
    /// ShareCommand 付属の依存プロパティを識別します。これにより、XAML 内の UI 要素に共有コマンドをバインドできます。
    /// </summary>
    /// <remarks>
    /// このプロパティにより、開発者はUI要素にICommand実装をアタッチでき、
    /// データバインディングを通じて共有操作のカスタム処理が可能になります。コンテキストメニューや
    /// キーボードによる共有操作がビューモデルのコマンドを呼び出す必要があるシナリオで一般的に使用されます。
    /// </remarks>
    public static readonly DependencyProperty ShareCommandProperty =
        DependencyProperty.RegisterAttached(
            "ShareCommand",
            typeof(ICommand),
            typeof(ShellContextMenuBehavior),
            new PropertyMetadata(null));
    /// <summary>
    /// 指定された依存オブジェクトに関連付けられるShareコマンドを設定します。
    /// </summary>
    /// <param name="d">Shareコマンドを設定する依存オブジェクト。null は指定できません。</param>
    /// <param name="v">依存オブジェクトに関連付けるコマンド、またはnullで既存のコマンドをクリアする。</param>
    public static void SetShareCommand(DependencyObject d, ICommand? v) => d.SetValue(ShareCommandProperty, v);
    /// <summary>
    /// 指定された依存オブジェクトに関連付けられたShareコマンドを取得します。
    /// </summary>
    /// <param name="d">Shareコマンドを設定する依存オブジェクト。null は指定できません。</param>
    /// <returns>Shareコマンドが設定されている場合にそれを表すインスタンス、そうでない場合は、null</returns>
    public static ICommand? GetShareCommand(DependencyObject d) => (ICommand?)d.GetValue(ShareCommandProperty);

    /// <summary>
    /// DeleteCommand 付属の依存プロパティを識別します。これにより、XAML 内の UI 要素に削除コマンドをバインドできます。
    /// </summary>
    /// <remarks>
    /// このプロパティにより、開発者はUI要素にICommand実装をアタッチでき、
    /// データバインディングを通じて削除操作のカスタム処理が可能になります。コンテキストメニューや
    /// キーボードによる削除操作がビューモデルのコマンドを呼び出す必要があるシナリオで一般的に使用されます。
    /// </remarks>
    public static readonly DependencyProperty DeleteCommandProperty =
        DependencyProperty.RegisterAttached(
            "DeleteCommand",
            typeof(ICommand),
            typeof(ShellContextMenuBehavior),
            new PropertyMetadata(null));
    /// <summary>
    /// 指定された依存オブジェクトに関連付けられるDeleteコマンドを設定します。
    /// </summary>
    /// <param name="d">Deleteコマンドを設定する依存オブジェクト。null は指定できません。</param>
    /// <param name="v">依存オブジェクトに関連付けるコマンド、またはnullで既存のコマンドをクリアする。</param>
    public static void SetDeleteCommand(DependencyObject d, ICommand? v) => d.SetValue(DeleteCommandProperty, v);
    /// <summary>
    /// 指定された依存オブジェクトに関連付けられたDeleteコマンドを取得します。
    /// </summary>
    /// <param name="d">Deleteコマンドを設定する依存オブジェクト。null は指定できません。</param>
    /// <returns>Deleteコマンドが設定されている場合にそれを表すインスタンス、そうでない場合は、null</returns>
    public static ICommand? GetDeleteCommand(DependencyObject d) => (ICommand?)d.GetValue(DeleteCommandProperty);


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
        // 例：上段の定番操作（あなたのメニュー構成に合わせて配置）
        yield return MI(s, "切り取り", GetCutCommand(grid), paths, gesture: "Ctrl+X");
        yield return MI(s, "コピー", GetCopyCommand(grid), paths, gesture: "Ctrl+C");
        yield return MI(s, "貼り付け", GetPasteCommand(grid), paths, gesture: "Ctrl+V");
        yield return new Separator();

        yield return MI(s, "名前の変更", GetRenameCommand(grid), paths, gesture: "F2");
        yield return MI(s, "共有", GetShareCommand(grid), paths);
        yield return MI(s, "削除", GetDeleteCommand(grid), paths, gesture: "Del");

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
        ICommand? command,
        object? commandParameter = null,
        string? gesture = null,
        bool isChecked = false,
        bool isEnabled = true,
        IEnumerable<object>? children = null)
    {
        // 指定がなければ無効化（表示は残す or 消すは好み）
        var cmd = command;
        var enabled = isEnabled && cmd != null;

        // 実行後に閉じるラッパー（cmd がある時だけ）
        ICommand? wrapped = null;
        if (cmd != null)
        {
            wrapped = new RelayCommand(_ =>
            {
                try
                {
                    if (cmd.CanExecute(commandParameter))
                        cmd.Execute(commandParameter);
                }
                finally
                {
                    s.Menu!.IsOpen = false;
                }
            });
        }

        var mi = new MenuItem
        {
            Header = header,
            Command = wrapped,
            CommandParameter = commandParameter,
            InputGestureText = gesture ?? string.Empty,
            IsChecked = isChecked,
            IsEnabled = enabled
        };

        if (children != null)
            foreach (var c in children) mi.Items.Add(c);

        return mi;
    }

    private static MenuItem MI(
    State s,
    string header,
    string? gesture = null,
    bool isChecked = false,
    bool isEnabled = true,
    IEnumerable<object>? children = null)
    {
        return MI(
            s,
            header,
            command: null,
            commandParameter: null,
            gesture: gesture,
            isChecked: isChecked,
            isEnabled: isEnabled,
            children: children);
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

    private static IReadOnlyList<string> GetSelectedPaths(WpfDataGrid grid)
    {
        var propName = GetPathPropertyName(grid);

        return grid.SelectedItems
            .Cast<object>()
            .Select(o => TryGetPathFromItem(o, propName))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Cast<string>()
            .ToList();
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
