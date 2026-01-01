using Chappy.Wpf.Controls.ContextMenu;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Chappy.Wpf.Controls.Test
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ShellContextMenuHost _shellMenu;
        private string _currentFolderPath = @"C:\Users\kobayashi\Desktop"; // 仮

        public MainWindow()
        {
            InitializeComponent();

            SourceInitialized += (_, __) =>
            {
                _shellMenu = new ShellContextMenuHost(this);
            };

            Closed += (_, __) => _shellMenu?.Dispose();
        }

        private void Row_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_shellMenu == null) return;

            if (sender is DataGridRow row)
            {
                row.IsSelected = true;
                row.Focus();

                if (row.Item is FileEntry item)
                {
                    var screen = row.PointToScreen(e.GetPosition(row));

                    // 行（ファイル/フォルダ）＝アイテムメニュー
                    _shellMenu.ShowForItem(item.FullPath, screen);

                    e.Handled = true;
                }
            }
        }

        private void FileGrid_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_shellMenu == null) return;


            var grid = (DataGrid)sender;

            // 右クリック位置の “行” を確実に拾う（FindAncestorより堅い）
            var dep = e.OriginalSource as DependencyObject;
            var row = ItemsControl.ContainerFromElement(grid, dep) as DataGridRow;

            if (row != null)
            {
                // 行の上：アイテムメニュー（ファイル/フォルダで自動的に変わる）
                row.IsSelected = true;
                row.Focus();

                if (row.Item is FileEntry item)
                {
                    var screen = grid.PointToScreen(e.GetPosition(grid));
                    _shellMenu.ShowForItem(item.FullPath, screen);
                    e.Handled = true;
                }
                return;
            }

            // 余白：背景メニュー（表示/並べ替え/新規作成…）
            e.Handled = true;

            var screen2 = grid.PointToScreen(e.GetPosition(grid));
            var folderPath = @"C:\Users\kobayashi\Desktop"; // ← “今表示中のフォルダ” に差し替え
            _shellMenu.ShowForFolderBackground(folderPath, screen2);
        }

        static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T t) return t;
                d = System.Windows.Media.VisualTreeHelper.GetParent(d);
            }
            return null;
        }

    }
}