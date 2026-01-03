#nullable enable
using System.Windows.Input;

namespace Chappy.Wpf.Controls.ContextMenu;

public class FilesLikeContextMenu : System.Windows.Controls.ContextMenu   // ★ 完全修飾名
{
    public ICommand? CutCommand { get; set; }
    public ICommand? CopyCommand { get; set; }
    public ICommand? PasteCommand { get; set; }
    public ICommand? RenameCommand { get; set; }
    public ICommand? ShareCommand { get; set; }
    public ICommand? DeleteCommand { get; set; }
}
