#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Chappy.Wpf.Controls.ContextMenu;

public class Win11ContextMenuView : Control
{
    static Win11ContextMenuView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(Win11ContextMenuView),
            new FrameworkPropertyMetadata(typeof(Win11ContextMenuView)));
    }

    public Win11ContextMenuView()
    {
        Items = new ObservableCollection<Win11MenuItemModel>();
    }

    public Win11ContextKind Kind
    {
        get => (Win11ContextKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }
    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(nameof(Kind), typeof(Win11ContextKind),
            typeof(Win11ContextMenuView),
            new PropertyMetadata(Win11ContextKind.File, (_, e) =>
            {
                if (_ is Win11ContextMenuView v) v.RebuildItems();
            }));

    public IReadOnlyList<string> SelectedPaths
    {
        get => (IReadOnlyList<string>)GetValue(SelectedPathsProperty);
        set => SetValue(SelectedPathsProperty, value);
    }
    public static readonly DependencyProperty SelectedPathsProperty =
        DependencyProperty.Register(nameof(SelectedPaths), typeof(IReadOnlyList<string>),
            typeof(Win11ContextMenuView),
            new PropertyMetadata(Array.Empty<string>(), (_, __) =>
            {
                if (_ is Win11ContextMenuView v) v.RebuildItems();
            }));

    // ===== 上段アイコン列（Files風） =====
    public ICommand? CutCommand { get => (ICommand?)GetValue(CutCommandProperty); set => SetValue(CutCommandProperty, value); }
    public static readonly DependencyProperty CutCommandProperty =
        DependencyProperty.Register(nameof(CutCommand), typeof(ICommand), typeof(Win11ContextMenuView), new PropertyMetadata(null));

    public ICommand? CopyCommand { get => (ICommand?)GetValue(CopyCommandProperty); set => SetValue(CopyCommandProperty, value); }
    public static readonly DependencyProperty CopyCommandProperty =
        DependencyProperty.Register(nameof(CopyCommand), typeof(ICommand), typeof(Win11ContextMenuView), new PropertyMetadata(null));

    public ICommand? PasteCommand { get => (ICommand?)GetValue(PasteCommandProperty); set => SetValue(PasteCommandProperty, value); }
    public static readonly DependencyProperty PasteCommandProperty =
        DependencyProperty.Register(nameof(PasteCommand), typeof(ICommand), typeof(Win11ContextMenuView), new PropertyMetadata(null));

    public ICommand? RenameCommand { get => (ICommand?)GetValue(RenameCommandProperty); set => SetValue(RenameCommandProperty, value); }
    public static readonly DependencyProperty RenameCommandProperty =
        DependencyProperty.Register(nameof(RenameCommand), typeof(ICommand), typeof(Win11ContextMenuView), new PropertyMetadata(null));

    public ICommand? ShareCommand { get => (ICommand?)GetValue(ShareCommandProperty); set => SetValue(ShareCommandProperty, value); }
    public static readonly DependencyProperty ShareCommandProperty =
        DependencyProperty.Register(nameof(ShareCommand), typeof(ICommand), typeof(Win11ContextMenuView), new PropertyMetadata(null));

    public ICommand? DeleteCommand { get => (ICommand?)GetValue(DeleteCommandProperty); set => SetValue(DeleteCommandProperty, value); }
    public static readonly DependencyProperty DeleteCommandProperty =
        DependencyProperty.Register(nameof(DeleteCommand), typeof(ICommand), typeof(Win11ContextMenuView), new PropertyMetadata(null));

    public ICommand? PropertiesCommand { get => (ICommand?)GetValue(PropertiesCommandProperty); set => SetValue(PropertiesCommandProperty, value); }
    public static readonly DependencyProperty PropertiesCommandProperty =
        DependencyProperty.Register(nameof(PropertiesCommand), typeof(ICommand), typeof(Win11ContextMenuView), new PropertyMetadata(null));

    // “その他のオプションを確認”＝クラシックへ委譲（必須）
    public ICommand? MoreOptionsCommand { get => (ICommand?)GetValue(MoreOptionsCommandProperty); set => SetValue(MoreOptionsCommandProperty, value); }
    public static readonly DependencyProperty MoreOptionsCommandProperty =
        DependencyProperty.Register(nameof(MoreOptionsCommand), typeof(ICommand), typeof(Win11ContextMenuView), new PropertyMetadata(null));

    public ObservableCollection<Win11MenuItemModel> Items
    {
        get => (ObservableCollection<Win11MenuItemModel>)GetValue(ItemsProperty);
        private set => SetValue(ItemsPropertyKey, value);
    }
    private static readonly DependencyPropertyKey ItemsPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(Items), typeof(ObservableCollection<Win11MenuItemModel>),
            typeof(Win11ContextMenuView), new PropertyMetadata(null));
    public static readonly DependencyProperty ItemsProperty = ItemsPropertyKey.DependencyProperty;

    // ここを “Filesっぽい構成” に寄せてる（3パターン）
    private void RebuildItems()
    {
        Items.Clear();

        if (Kind == Win11ContextKind.Folder)
        {
            Items.Add(new() { Text = "開く", Command = new RelayCommand(_ => RaiseOpen()) });
            Items.Add(new() { Text = "新しいタブで開く", Command = new RelayCommand(_ => RaiseOpenNewTab()) });
            Items.Add(new() { Text = "新しいウィンドウで開く", Gesture = "Alt+Ctrl+Enter", Command = new RelayCommand(_ => RaiseOpenNewWindow()) });
            Items.Add(new() { Text = "新しいペインで開く", Command = new RelayCommand(_ => RaiseOpenNewPane()) });

            Items.Add(Win11MenuItemModel.Sep());

            Items.Add(new() { Text = "アイテムのパスをコピー", Gesture = "Ctrl+Shift+C", Command = new RelayCommand(_ => RaiseCopyPath()) });
            Items.Add(new() { Text = "選択した項目でフォルダーを作成", Command = new RelayCommand(_ => RaiseCreateFolderFromSelection()) });
            Items.Add(new() { Text = "ショートカットの作成", Command = new RelayCommand(_ => RaiseCreateShortcut()) });

            Items.Add(Win11MenuItemModel.Sep());

            Items.Add(new() { Text = "圧縮", HasSubmenu = true, Command = new RelayCommand(_ => RaiseCompress()) });
            Items.Add(new() { Text = "送る", HasSubmenu = true, Command = new RelayCommand(_ => RaiseSendTo()) });

            Items.Add(Win11MenuItemModel.Sep());
            Items.Add(new() { Text = "ターミナルで開く", Gesture = "Ctrl+@", Command = new RelayCommand(_ => RaiseOpenTerminal()) });

            Items.Add(Win11MenuItemModel.Sep());
            Items.Add(new() { Text = "タグを編集", HasSubmenu = true, Command = new RelayCommand(_ => RaiseEditTags()) });

            Items.Add(Win11MenuItemModel.Sep());
            Items.Add(new() { Text = "その他のオプションを確認", HasSubmenu = true, Command = MoreOptionsCommand });
            return;
        }

        if (Kind == Win11ContextKind.File)
        {
            Items.Add(new() { Text = "開く", Command = new RelayCommand(_ => RaiseOpen()) });
            Items.Add(new() { Text = "プログラムから開く", HasSubmenu = true, Command = new RelayCommand(_ => RaiseOpenWith()) });

            Items.Add(Win11MenuItemModel.Sep());

            Items.Add(new() { Text = "ショートカットを貼り付け", Command = new RelayCommand(_ => RaisePinShortcut()) });
            Items.Add(new() { Text = "アイテムのパスをコピー", Gesture = "Ctrl+Shift+C", Command = new RelayCommand(_ => RaiseCopyPath()) });
            Items.Add(new() { Text = "選択した項目でフォルダーを作成", Command = new RelayCommand(_ => RaiseCreateFolderFromSelection()) });
            Items.Add(new() { Text = "ショートカットの作成", Command = new RelayCommand(_ => RaiseCreateShortcut()) });

            Items.Add(Win11MenuItemModel.Sep());

            Items.Add(new() { Text = "圧縮", HasSubmenu = true, Command = new RelayCommand(_ => RaiseCompress()) });
            Items.Add(new() { Text = "送る", HasSubmenu = true, Command = new RelayCommand(_ => RaiseSendTo()) });

            Items.Add(Win11MenuItemModel.Sep());
            Items.Add(new() { Text = "タグを編集", HasSubmenu = true, Command = new RelayCommand(_ => RaiseEditTags()) });

            Items.Add(Win11MenuItemModel.Sep());
            Items.Add(new() { Text = "その他のオプションを確認", HasSubmenu = true, Command = MoreOptionsCommand });
            return;
        }

        // Background
        Items.Add(new() { Text = "ペインを閉じる", Gesture = "Alt+Ctrl+W", Command = new RelayCommand(_ => RaiseClosePane()) });

        Items.Add(Win11MenuItemModel.Sep());

        Items.Add(new() { Text = "レイアウト", HasSubmenu = true, Command = new RelayCommand(_ => RaiseLayout()) });
        Items.Add(new() { Text = "並べ替え", HasSubmenu = true, Command = new RelayCommand(_ => RaiseSort()) });
        Items.Add(new() { Text = "グループで表示", HasSubmenu = true, Command = new RelayCommand(_ => RaiseGroup()) });
        Items.Add(new() { Text = "最新の情報に更新", Gesture = "Ctrl+R", Command = new RelayCommand(_ => RaiseRefresh()) });

        Items.Add(Win11MenuItemModel.Sep());

        Items.Add(new() { Text = "新規作成", HasSubmenu = true, Command = new RelayCommand(_ => RaiseNew()) });
        Items.Add(new() { Text = "ショートカットを貼り付け", Command = new RelayCommand(_ => RaisePinShortcut()) });

        Items.Add(Win11MenuItemModel.Sep());

        Items.Add(new() { Text = "ターミナルで開く", Gesture = "Ctrl+@", Command = new RelayCommand(_ => RaiseOpenTerminal()) });

        Items.Add(Win11MenuItemModel.Sep());
        Items.Add(new() { Text = "その他のオプションを確認", HasSubmenu = true, Command = MoreOptionsCommand });
    }

    // ===== 外に逃がしたいイベント（あなたのアプリ側に接続しやすい） =====
    public event EventHandler? OpenRequested;
    public event EventHandler? OpenNewTabRequested;
    public event EventHandler? OpenNewWindowRequested;
    public event EventHandler? OpenNewPaneRequested;
    public event EventHandler? CopyPathRequested;
    public event EventHandler? RefreshRequested;

    private void RaiseOpen() => OpenRequested?.Invoke(this, EventArgs.Empty);
    private void RaiseOpenNewTab() => OpenNewTabRequested?.Invoke(this, EventArgs.Empty);
    private void RaiseOpenNewWindow() => OpenNewWindowRequested?.Invoke(this, EventArgs.Empty);
    private void RaiseOpenNewPane() => OpenNewPaneRequested?.Invoke(this, EventArgs.Empty);
    private void RaiseCopyPath() => CopyPathRequested?.Invoke(this, EventArgs.Empty);
    private void RaiseRefresh() => RefreshRequested?.Invoke(this, EventArgs.Empty);

    // 今は未実装でもOK（後で実装）
    private void RaiseOpenWith() { }
    private void RaiseCreateFolderFromSelection() { }
    private void RaiseCreateShortcut() { }
    private void RaiseCompress() { }
    private void RaiseSendTo() { }
    private void RaiseOpenTerminal() { }
    private void RaiseEditTags() { }
    private void RaisePinShortcut() { }
    private void RaiseClosePane() { }
    private void RaiseLayout() { }
    private void RaiseSort() { }
    private void RaiseGroup() { }
    private void RaiseNew() { }

    // 内部RelayCommand（同一namespaceに置いてOK）
    private sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _exec;
        public RelayCommand(Action<object?> exec) => _exec = exec;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _exec(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
