using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
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

        public System.Func<IList, System.Windows.IDataObject?> BuildDragPayload => items =>
        {
            var paths = items.Cast<FileEntry>()
                             .Select(x => x.FullPath)
                             .ToArray();

            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, paths);
            return data;
        };

        public System.Action<System.Windows.IDataObject, object?> OnDrop => (data, target) =>
        {
            if (!data.GetDataPresent(DataFormats.FileDrop)) return;

            var files = (string[])data.GetData(DataFormats.FileDrop)!;
            var targetItem = target as FileEntry;

            // 移動・並び替え・タグ付け等
            System.Diagnostics.Debug.WriteLine($"OnDrop: {files.Length} files dropped on {targetItem?.Name ?? "null"}");
        };
    }
}
