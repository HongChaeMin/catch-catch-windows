// Resolve WPF vs WinForms ambiguities — prefer WPF types
global using Application = System.Windows.Application;
global using Point = System.Windows.Point;
global using Size = System.Windows.Size;
global using Image = System.Windows.Controls.Image;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
