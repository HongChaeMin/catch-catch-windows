// Resolve WPF vs WinForms ambiguities — prefer WPF types
global using Application = System.Windows.Application;
global using Point = System.Windows.Point;
global using Size = System.Windows.Size;
global using Image = System.Windows.Controls.Image;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
global using Cursors = System.Windows.Input.Cursors;
global using Brushes = System.Windows.Media.Brushes;
global using Color = System.Windows.Media.Color;
global using MessageBox = System.Windows.MessageBox;
global using MessageBoxResult = System.Windows.MessageBoxResult;
global using MessageBoxButton = System.Windows.MessageBoxButton;
