using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using UniversalSearch.Core;

namespace UniversalSearch.App;

public sealed class PreviewWindow : Window
{
    readonly Grid stage = new();
    readonly TextBlock info = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12) };
    readonly CancellationTokenSource lifetime = new();
    readonly DispatcherTimer timeout = new() { Interval = TimeSpan.FromSeconds(8) };
    MediaElement? media;
    readonly Strings strings;
    public PreviewWindow(Window owner, ResultItem item, MainViewModel vm)
    {
        Owner = owner; strings = vm.L; Title = strings["Preview"];
        Width = Math.Min(620, SystemParameters.WorkArea.Width - 32);
        Height = Math.Min(460, SystemParameters.WorkArea.Height - 32);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize;
        SetResourceReference(BackgroundProperty, "ThemeSurface");
        SetResourceReference(ForegroundProperty, "ThemeText");
        var layout = new DockPanel { Margin = new Thickness(22) };
        var title = new TextBlock { Text = item.Name, FontSize = 20, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        DockPanel.SetDock(title, Dock.Top); layout.Children.Add(title);
        var hint = new TextBlock { Text = strings["PreviewHint"], Margin = new Thickness(0, 12, 0, 0) };
        hint.SetResourceReference(ForegroundProperty, "ThemeMuted");
        DockPanel.SetDock(hint, Dock.Bottom); layout.Children.Add(hint);
        DockPanel.SetDock(info, Dock.Top); layout.Children.Add(info); layout.Children.Add(stage); Content = layout;
        info.Text = strings["PreviewLoading"];
        stage.Children.Add(new Image { Source = item.Icon, Width = 64, Height = 64 });
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Space or Key.Escape) { Close(); e.Handled = true; }
            else if (e.Key == Key.Enter) { Close(); vm.Selected = item; vm.Open.Execute(null); e.Handled = true; }
        };
        Closed += (_, _) => { lifetime.Cancel(); timeout.Stop(); media?.Close(); media = null; stage.Children.Clear(); };
        timeout.Tick += (_, _) => { timeout.Stop(); media?.Close(); stage.Children.Clear(); info.Text += "\n" + strings["PreviewUnsupported"]; };
        Loaded += async (_, _) =>
        {
            if (SystemParameters.ClientAreaAnimation)
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
            await LoadAsync(item);
        };
    }
    async Task LoadAsync(ResultItem item)
    {
        try
        {
            var result = await Task.Run(() =>
            {
                lifetime.Token.ThrowIfCancellationRequested();
                var file = new FileInfo(item.Entry.Path);
                if (!file.Exists) throw new FileNotFoundException();
                var description = $"{item.Detail} · {file.Length / 1024d:N1} KB";
                BitmapImage? bitmap = null;
                var kind = PreviewPolicy.Classify(item.Entry, file.Length);
                if (kind == PreviewKind.Image)
                {
                    using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    var header = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
                    bool landscape = header.PixelWidth >= header.PixelHeight; stream.Position = 0;
                    bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    if (landscape) bitmap.DecodePixelWidth = Math.Min(1000, header.PixelWidth); else bitmap.DecodePixelHeight = Math.Min(1000, header.PixelHeight);
                    bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
                }
                return (description, bitmap, kind);
            }, lifetime.Token);
            if (lifetime.IsCancellationRequested) return;
            info.Text = result.description;
            if (result.bitmap != null) { stage.Children.Clear(); stage.Children.Add(new Image { Source = result.bitmap, Stretch = Stretch.Uniform }); }
            else if (result.kind == PreviewKind.Video)
            {
                media = new MediaElement { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Close, IsMuted = true, Volume = 0, Stretch = Stretch.Uniform };
                media.MediaOpened += (_, _) => { timeout.Stop(); if (!lifetime.IsCancellationRequested && media != null && media.NaturalDuration.HasTimeSpan) { info.Text += " · " + media.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss"); media.Position = TimeSpan.FromMilliseconds(Math.Min(100, media.NaturalDuration.TimeSpan.TotalMilliseconds / 2)); } };
                media.MediaFailed += (_, _) => { timeout.Stop(); if (!lifetime.IsCancellationRequested) { media?.Close(); stage.Children.Clear(); info.Text += "\n" + strings["PreviewUnsupported"]; } };
                stage.Children.Clear(); stage.Children.Add(media); media.Source = new Uri(item.Entry.Path); media.Pause(); timeout.Start();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { if (!lifetime.IsCancellationRequested) info.Text = strings["PreviewUnsupported"]; }
    }
}



