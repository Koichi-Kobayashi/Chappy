using System.Collections.ObjectModel;
using System.Windows.Media;

namespace Chappy.Wpf.Controls.Test
{
    public class MainViewModel
    {
        public MainViewModel()
        {
            Items.Add(new FileEntry
            {
                Name = "新規 テキスト ドキュメント.txt",
                FullPath = @"C:\Users\kobayashi\Downloads\新規 テキスト ドキュメント.txt",
                CurrentFolder = @"C:\Users\kobayashi\Downloads",
                Kind = EntryKind.File
            });
            Items.Add(new FileEntry
            {
                Name = "abc",
                FullPath = @"C:\Users\kobayashi\Downloads\abc",
                CurrentFolder = @"C:\Users\kobayashi\Downloads\abc",
                Kind = EntryKind.Folder
            });
        }

        public Color MyColor { get; set; } = Colors.DeepSkyBlue;

        public ObservableCollection<FileEntry> Items { get; } = new ObservableCollection<FileEntry>();
    }
}
