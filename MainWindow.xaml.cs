using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FontAwesome.Sharp;
using SAP.Models;
using SAP.Services;
using SAP.ViewModels;

namespace SAP;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel => (MainViewModel)DataContext;
    private readonly MediaKeyService _mediaKeys = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        _mediaKeys.PlayPausePressed += () => Dispatcher.Invoke(() => ViewModel.PlayPauseCommand.Execute(null));
        _mediaKeys.NextPressed += () => Dispatcher.Invoke(() => ViewModel.NextCommand.Execute(null));
        _mediaKeys.PreviousPressed += () => Dispatcher.Invoke(() => ViewModel.PreviousCommand.Execute(null));
        _mediaKeys.StopPressed += () => Dispatcher.Invoke(() => ViewModel.StopCommand.Execute(null));
        _mediaKeys.Initialize();
    }

    private void PlaylistItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is Song song)
            ViewModel.DoubleClickSongCommand.Execute(song);
    }

    private void MediaCommandExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command == MediaCommands.Play || e.Command == MediaCommands.Pause)
            ViewModel.PlayPauseCommand.Execute(null);
        else if (e.Command == MediaCommands.NextTrack)
            ViewModel.NextCommand.Execute(null);
        else if (e.Command == MediaCommands.PreviousTrack)
            ViewModel.PreviousCommand.Execute(null);
        else if (e.Command == MediaCommands.Stop)
            ViewModel.StopCommand.Execute(null);
    }

    private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _mediaKeys.Dispose();
        ViewModel.Cleanup();
    }
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

public class BoolToPlayPauseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool show && show ? IconChar.Pause : IconChar.Play;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToColorConverter : IValueConverter
{
    public string ActiveColor { get; set; } = "#1DB954";
    public string InactiveColor { get; set; } = "#bbb";
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(ActiveColor))
                               : new SolidColorBrush((Color)ColorConverter.ConvertFromString(InactiveColor));
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class Base64ImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string dataUri || string.IsNullOrEmpty(dataUri))
            return DependencyProperty.UnsetValue;

        try
        {
            var base64 = dataUri.Contains(',') ? dataUri[(dataUri.IndexOf(',') + 1)..] : dataUri;
            var bytes = System.Convert.FromBase64String(base64);
            using var ms = new System.IO.MemoryStream(bytes);
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = ms;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
