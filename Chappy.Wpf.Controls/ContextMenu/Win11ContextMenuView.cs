#nullable enable
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Chappy.Wpf.Controls.ContextMenu
{
    /// <summary>
    /// Win11風メニューの「見た目」コントロール。
    /// PopupのChildとして使う。TemplateはResourceDictionaryで与える。
    /// </summary>
    public class Win11ContextMenuView : Control
    {
        // 背景メニューか？
        public bool IsBackground
        {
            get => (bool)GetValue(IsBackgroundProperty);
            set => SetValue(IsBackgroundProperty, value);
        }
        public static readonly DependencyProperty IsBackgroundProperty =
            DependencyProperty.Register(nameof(IsBackground), typeof(bool),
                typeof(Win11ContextMenuView), new PropertyMetadata(false));

        // 選択パス（単体/複数）
        public IReadOnlyList<string> SelectedPaths
        {
            get => (IReadOnlyList<string>)GetValue(SelectedPathsProperty);
            set => SetValue(SelectedPathsProperty, value);
        }
        public static readonly DependencyProperty SelectedPathsProperty =
            DependencyProperty.Register(nameof(SelectedPaths), typeof(IReadOnlyList<string>),
                typeof(Win11ContextMenuView), new PropertyMetadata(Array.Empty<string>()));

        // ===== Commands（テンプレから Binding する） =====
        public ICommand CutCommand
        {
            get => (ICommand)GetValue(CutCommandProperty);
            set => SetValue(CutCommandProperty, value);
        }
        public static readonly DependencyProperty CutCommandProperty =
            DependencyProperty.Register(nameof(CutCommand), typeof(ICommand),
                typeof(Win11ContextMenuView), new PropertyMetadata(null));

        public ICommand CopyCommand
        {
            get => (ICommand)GetValue(CopyCommandProperty);
            set => SetValue(CopyCommandProperty, value);
        }
        public static readonly DependencyProperty CopyCommandProperty =
            DependencyProperty.Register(nameof(CopyCommand), typeof(ICommand),
                typeof(Win11ContextMenuView), new PropertyMetadata(null));

        public ICommand DeleteCommand
        {
            get => (ICommand)GetValue(DeleteCommandProperty);
            set => SetValue(DeleteCommandProperty, value);
        }
        public static readonly DependencyProperty DeleteCommandProperty =
            DependencyProperty.Register(nameof(DeleteCommand), typeof(ICommand),
                typeof(Win11ContextMenuView), new PropertyMetadata(null));

        public ICommand PropertiesCommand
        {
            get => (ICommand)GetValue(PropertiesCommandProperty);
            set => SetValue(PropertiesCommandProperty, value);
        }
        public static readonly DependencyProperty PropertiesCommandProperty =
            DependencyProperty.Register(nameof(PropertiesCommand), typeof(ICommand),
                typeof(Win11ContextMenuView), new PropertyMetadata(null));

        public ICommand MoreOptionsCommand
        {
            get => (ICommand)GetValue(MoreOptionsCommandProperty);
            set => SetValue(MoreOptionsCommandProperty, value);
        }
        public static readonly DependencyProperty MoreOptionsCommandProperty =
            DependencyProperty.Register(nameof(MoreOptionsCommand), typeof(ICommand),
                typeof(Win11ContextMenuView), new PropertyMetadata(null));
    }

    /// <summary>軽量 ICommand</summary>
    internal sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
