using System.Windows;
using System.Windows.Markup;

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]


// 1つの xmlns に集約
[assembly: XmlnsDefinition(
    "http://schemas.chappy.dev/wpf/controls",
    "Chappy.Wpf.Controls.ColorPicker")]

[assembly: XmlnsDefinition(
    "http://schemas.chappy.dev/wpf/controls",
    "Chappy.Wpf.Controls.ContextMenu")]

[assembly: XmlnsDefinition(
    "http://schemas.chappy.dev/wpf/controls",
    "Chappy.Wpf.Controls.DataGrid")]

[assembly: XmlnsDefinition(
    "http://schemas.chappy.dev/wpf/controls",
    "Chappy.Wpf.Controls.Behaviors")]

// 推奨：XAML での既定 prefix
[assembly: XmlnsPrefix(
    "http://schemas.chappy.dev/wpf/controls",
    "chappy")]

