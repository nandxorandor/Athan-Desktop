// The tray icon forces a reference to Windows Forms, which brings a second set
// of types with the same names as WPF's. Rather than qualify every use, pin the
// clashing names to the WPF ones here - this is a WPF app, and Forms is reached
// through the "Forms." alias in App.xaml.cs where the tray icon is built.

global using Application = System.Windows.Application;
global using MessageBox = System.Windows.MessageBox;
global using Button = System.Windows.Controls.Button;
global using ComboBox = System.Windows.Controls.ComboBox;
global using ListBox = System.Windows.Controls.ListBox;
global using CheckBox = System.Windows.Controls.CheckBox;
global using TextBox = System.Windows.Controls.TextBox;
global using Label = System.Windows.Controls.Label;
global using Orientation = System.Windows.Controls.Orientation;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
global using VerticalAlignment = System.Windows.VerticalAlignment;
global using Cursors = System.Windows.Input.Cursors;
global using Cursor = System.Windows.Input.Cursor;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
global using Clipboard = System.Windows.Clipboard;
global using DataFormats = System.Windows.DataFormats;
global using DragEventArgs = System.Windows.DragEventArgs;
global using Point = System.Windows.Point;
global using Size = System.Windows.Size;
global using Brush = System.Windows.Media.Brush;
global using Brushes = System.Windows.Media.Brushes;
global using Color = System.Windows.Media.Color;
global using FontFamily = System.Windows.Media.FontFamily;
global using Pen = System.Windows.Media.Pen;
global using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
global using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
