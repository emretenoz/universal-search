using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace UniversalSearch.App;
public sealed class BindingProxy : Freezable
{
    protected override Freezable CreateInstanceCore() => new BindingProxy();
    public object Data { get => GetValue(DataProperty); set => SetValue(DataProperty,value); }
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy));
}
public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent(); Style = (Style)FindResource(typeof(Window)); DataContext = vm;
        vm.PropertyChanged += (_, _) => UpdateLayoutState();
        vm.Results.CollectionChanged += (_, _) => UpdateLayoutState();
        UpdateLayoutState();
        Loaded += (_, _) => Reveal();
        Closing += (_, e) => { if (!((App)Application.Current).Quitting) { e.Cancel = true; Hide(); } };
    }
    public void Reveal()
    {
        var area = SystemParameters.WorkArea; Left = area.Left + (area.Width - Width) / 2; Top = area.Top + Math.Min(90, area.Height * .1);
        Width = Math.Min(720, area.Width - 32);
        Left = area.Left + (area.Width - Width) / 2;
        MaxHeight = Math.Max(100, area.Height - (Top - area.Top) - 20);
        UpdateLayoutState();
        Show(); Activate(); SearchBox.Focus(); SearchBox.SelectAll();
        if (SystemParameters.ClientAreaAnimation)
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170)));
            Arrival.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation(-8, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
    }
    bool IsExpanded => DataContext is MainViewModel vm && (!string.IsNullOrWhiteSpace(vm.Query) || vm.FilterKey != "All");
    double resultHeight = -1;
    void UpdateLayoutState()
    {
        if (DataContext is not MainViewModel vm) return;
        var expanded = IsExpanded;
        FilterStrip.Visibility = Visibility.Collapsed;
        ResultsPanel.Visibility = Footer.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        bool showCard = expanded || !string.IsNullOrEmpty(vm.Notice);
        if (showCard && DetailsCard.Visibility != Visibility.Visible && IsLoaded && SystemParameters.ClientAreaAnimation)
            DetailsCard.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
        DetailsCard.Visibility = showCard ? Visibility.Visible : Visibility.Collapsed;
        var targetHeight = Math.Min(300, Math.Max(64, vm.Results.Count * 64));
        if ((!vm.Busy || resultHeight < 0) && resultHeight != targetHeight)
        {
            var from = ResultsPanel.ActualHeight;
            resultHeight = targetHeight;
            ResultsPanel.BeginAnimation(HeightProperty, null);
            ResultsPanel.Height = targetHeight;
            if (IsLoaded && expanded && SystemParameters.ClientAreaAnimation)
                ResultsPanel.BeginAnimation(HeightProperty, new DoubleAnimation(from, targetHeight, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
        IndexStatus.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        StatusPanel.Visibility = expanded || !string.IsNullOrEmpty(vm.Notice) ? Visibility.Visible : Visibility.Collapsed;
    }
    void OrbEnter(object sender, MouseEventArgs e) => AnimateOrb(sender, 1.06);
    void OrbLeave(object sender, MouseEventArgs e) => AnimateOrb(sender, 1);
    static void AnimateOrb(object sender, double scale)
    {
        if (!SystemParameters.ClientAreaAnimation || sender is not FrameworkElement element) return;
        if (element.RenderTransform is not System.Windows.Media.ScaleTransform transform)
        {
            transform = new System.Windows.Media.ScaleTransform(1, 1);
            element.RenderTransform = transform;
            element.RenderTransformOrigin = new Point(.5, .5);
        }
        var animation = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(130)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, animation);
    }
    void HideWindow(object sender, RoutedEventArgs e) => Hide();
    void ClearSearch(object sender, RoutedEventArgs e)
    {
        ((MainViewModel)DataContext).Query = "";
        SearchBox.Focus();
    }
    void DragHeader(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left && e.OriginalSource is not System.Windows.Controls.TextBox) DragMove(); }
    void OpenResult(object sender, MouseButtonEventArgs e) { if (ResultList.SelectedItem != null) ((MainViewModel)DataContext).Open.Execute(null); }
    void OnKeyDown(object sender, KeyEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
        if (e.Key == Key.Escape) { Hide(); e.Handled = true; }
        else if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.ButtonBase) return;
        else if (e.Key == Key.Space && ResultList.IsKeyboardFocusWithin && vm.Selected is { Entry.Kind: UniversalSearch.Core.EntryKind.File } selected) { e.Handled = true; if (!e.IsRepeat) new PreviewWindow(this, selected, vm).ShowDialog(); }
        else if (e.Key == Key.Enter) { if (IsExpanded) vm.Open.Execute(null); e.Handled = true; }
        else if (e.Key is Key.Down or Key.Up)
        {
            int i = UniversalSearch.Core.ResultNavigation.Next(ResultList.SelectedIndex, vm.Results.Count, e.Key == Key.Down, SearchBox.IsKeyboardFocusWithin);
            if (IsExpanded && vm.Results.Count > 0) { ResultList.SelectedIndex = Math.Clamp(i,0,vm.Results.Count-1); ResultList.ScrollIntoView(ResultList.SelectedItem); ResultList.UpdateLayout(); (ResultList.ItemContainerGenerator.ContainerFromItem(ResultList.SelectedItem) as System.Windows.Controls.ListBoxItem)?.Focus(); }
            e.Handled = true;
        }
    }
}







