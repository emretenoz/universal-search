using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using UniversalSearch.Core;

namespace UniversalSearch.App;

public sealed class SettingsWindow : Window
{
    readonly App app;
    readonly Strings l;
    readonly ObservableCollection<string> roots;
    readonly ObservableCollection<string> exclusions;
    readonly CheckBox browser = new(), startup = new();
    readonly TextBox hotkey = new(), engines = new();
    readonly ContentControl pageHost = new();
    readonly Dictionary<string, StackPanel> pages = new();
    readonly Dictionary<string, Button> navButtons = new();
    readonly Dictionary<string, Border> themeCards = new();
    StackPanel body = new();
    string selectedTheme, selectedLanguage;
    bool committed;

    public SettingsWindow(App app)
    {
        this.app = app; l = app.VM.L;
        var settings = app.Config.Settings;
        selectedTheme = settings.Theme == "dark" ? "dark" : "light"; selectedLanguage = settings.Language;
        roots = new(settings.Roots); exclusions = new(settings.Exclusions);
        Style = (Style)FindResource(typeof(Window)); Title = "Universal Search · " + l["Settings"];
        Width = Math.Min(960, SystemParameters.WorkArea.Width - 40); Height = Math.Min(720, SystemParameters.WorkArea.Height - 40);
        MinWidth = 680; MinHeight = 500; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        Closed += (_, _) => { if (!committed) ThemeManager.Apply(app.Config.Settings.Theme); };
        var frame = new Grid(); frame.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(208) }); frame.ColumnDefinitions.Add(new ColumnDefinition()); Content = frame;
        var rail = new DockPanel { Margin = new Thickness(16,24,16,20) };
        var railSurface = new Border { Child = rail, BorderThickness = new Thickness(0,0,1,0) }; Paint(railSurface, Border.BackgroundProperty,"Surface"); Paint(railSurface,Border.BorderBrushProperty,"Stroke"); frame.Children.Add(railSurface);
        var brand = new StackPanel { Margin = new Thickness(12,0,0,26) }; brand.Children.Add(new TextBlock { Text="Universal", FontSize=24, FontWeight=FontWeights.SemiBold });
        var caption=Text("SEARCH",11); caption.Margin=new Thickness(1,5,0,0); brand.Children.Add(caption); DockPanel.SetDock(brand,Dock.Top);rail.Children.Add(brand);
        var version=Text("Universal Search  0.5.0",11);version.Margin=new Thickness(12,20,0,0);DockPanel.SetDock(version,Dock.Bottom);rail.Children.Add(version);
        var navigation=new StackPanel();rail.Children.Add(navigation);
        foreach(var entry in new[]{("General","\uE713"),("Locations","\uE8B7"),("WebSettings","\uE774"),("Privacy","\uE72E")})
        {
            var key=entry.Item1;var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(new TextBlock{Text=entry.Item2,FontFamily=new FontFamily("Segoe MDL2 Assets"),Margin=new Thickness(0,0,12,0),VerticalAlignment=VerticalAlignment.Center});row.Children.Add(new TextBlock{Text=l[key],VerticalAlignment=VerticalAlignment.Center});
            var button=new Button{Content=row,HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(14,13,14,13),Margin=new Thickness(0,3,0,3)};
            System.Windows.Automation.AutomationProperties.SetName(button,l[key]); button.Click+=(_,_)=>ShowPage(key); navigation.Children.Add(button);navButtons[key]=button;
        }
        var right=new DockPanel();Grid.SetColumn(right,1);frame.Children.Add(right);
        var footer=new Border{Padding=new Thickness(24,14,24,14),BorderThickness=new Thickness(0,1,0,0)};Paint(footer,Border.BackgroundProperty,"Surface");Paint(footer,Border.BorderBrushProperty,"Stroke");DockPanel.SetDock(footer,Dock.Bottom);right.Children.Add(footer);
        var footerGrid=new Grid();footerGrid.ColumnDefinitions.Add(new ColumnDefinition());footerGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});footer.Child=footerGrid;
        var hint=Text(l["SettingsDraft"],12);hint.VerticalAlignment=VerticalAlignment.Center;hint.MaxWidth=220;footerGrid.Children.Add(hint);
        var actions=new StackPanel{Orientation=Orientation.Horizontal};Grid.SetColumn(actions,1);footerGrid.Children.Add(actions);
        actions.Children.Add(Button(l["Cancel"],()=>{Close();return Task.CompletedTask;}));
        var saveButton=Button(l["SaveChanges"],Save);Paint(saveButton,Control.BackgroundProperty,"Selected");actions.Children.Add(saveButton);
        right.Children.Add(new ScrollViewer{Content=pageHost,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});

        Page("General",l["GeneralIntro"]);
        Label(l["Appearance"]);var choices=new Grid();choices.ColumnDefinitions.Add(new ColumnDefinition());choices.ColumnDefinitions.Add(new ColumnDefinition());body.Children.Add(choices);
        int column=0;
        foreach(var theme in new[]{"light","dark"})
        {
            var name=theme;var sample=new StackPanel();
            var preview=new Border{Background=(Brush)new BrushConverter().ConvertFromString(theme=="dark"?"#22252D":"#F1F5FA")!,CornerRadius=new CornerRadius(10),Padding=new Thickness(14),Height=92};
            var mini=new StackPanel();mini.Children.Add(new Border{Height=16,CornerRadius=new CornerRadius(8),Background=(Brush)new BrushConverter().ConvertFromString(theme=="dark"?"#485368":"#D2DEEF")!,Margin=new Thickness(0,4,0,12)});mini.Children.Add(new Border{Height=8,Width=76,HorizontalAlignment=HorizontalAlignment.Left,CornerRadius=new CornerRadius(4),Background=(Brush)new BrushConverter().ConvertFromString(theme=="dark"?"#77859E":"#B4C3D9")!});preview.Child=mini;sample.Children.Add(preview);
            sample.Children.Add(new TextBlock{Text=l[theme=="dark"?"DarkTheme":"LightTheme"],FontWeight=FontWeights.SemiBold,Margin=new Thickness(3,12,0,3)});
            var choice=new Button{Content=sample,HorizontalContentAlignment=HorizontalAlignment.Stretch,Padding=new Thickness(10),Margin=new Thickness(0,0,12,0)};
            var card=new Border{Child=choice,BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(13),Margin=new Thickness(0,0,4,0)};themeCards[theme]=card;Grid.SetColumn(card,column++);choices.Children.Add(card);
            System.Windows.Automation.AutomationProperties.SetName(choice,l[theme=="dark"?"DarkTheme":"LightTheme"]); choice.Click+=(_,_)=>{selectedTheme=name;ThemeManager.Apply(name);UpdateThemeCards();};
        }
        Label(l["Hotkey"]);hotkey.Text=settings.Hotkey;body.Children.Add(hotkey);
        Label(l["Language"]);var languages=new StackPanel{Orientation=Orientation.Horizontal};body.Children.Add(languages);
        foreach(var option in new[]{("tr","Türkçe"),("en","English")}){var code=option.Item1;var radio=new RadioButton{Content=option.Item2,GroupName="Language",IsChecked=selectedLanguage==code,Margin=new Thickness(0,6,24,10)};Paint(radio,Control.ForegroundProperty,"Text");radio.Checked+=(_,_)=>selectedLanguage=code;languages.Children.Add(radio);}
        startup.Content=l["Startup"];startup.IsChecked=settings.StartWithWindows;body.Children.Add(startup);

        Page("Locations",l["LocationsIntro"]);
        var progress=Text("",13);Paint(progress,TextBlock.ForegroundProperty,"Accent");progress.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding(nameof(MainViewModel.Status)){Source=app.VM});body.Children.Add(progress);
        Label(l["QuickFolders"]);var shortcuts=new WrapPanel();body.Children.Add(shortcuts);
        foreach(var item in new[]{("Desktop",Environment.SpecialFolder.DesktopDirectory),("Documents",Environment.SpecialFolder.MyDocuments),("Pictures",Environment.SpecialFolder.MyPictures)}){var path=Environment.GetFolderPath(item.Item2);if(Directory.Exists(path))shortcuts.Children.Add(Button(l[item.Item1],()=>{AddRoot(path);return Task.CompletedTask;}));}
        foreach(var drive in DriveInfo.GetDrives().Where(d=>d.DriveType==DriveType.Fixed)){var path=drive.Name;shortcuts.Children.Add(Button(path,()=>{AddRoot(path);return Task.CompletedTask;}));}
        Label(l["Roots"]);FolderList(roots);Label(l["Exclusions"]);FolderList(exclusions);body.Children.Add(Text(l["DriveHint"],12));

        Page("WebSettings",l["WebIntro"]);Label(l["WebEngines"]);engines.Text=string.Join(Environment.NewLine,settings.WebEngines.Select(e=>$"{e.Keyword} | {e.Name} | {e.Url}"));engines.AcceptsReturn=true;engines.MinHeight=170;engines.TextWrapping=TextWrapping.Wrap;engines.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;body.Children.Add(engines);body.Children.Add(Text(l["WebExample"],12));

        Page("Privacy",l["PrivacyIntro"]);Label(l["BrowserSection"]);browser.Content=new TextBlock{Text=l["Browser"],TextWrapping=TextWrapping.Wrap};browser.IsChecked=settings.BrowserHistory;body.Children.Add(browser);
        Label(l["LocalData"]);body.Children.Add(Text(l["LocalDataIntro"],13));
        var cleanup=new WrapPanel{Margin=new Thickness(0,12,0,18)};cleanup.Children.Add(Button(l["ClearHistory"],async()=>await app.ClearAsync(false)));cleanup.Children.Add(Button(l["ClearIndex"],async()=>await app.ClearAsync(true)));body.Children.Add(cleanup);
        body.Children.Add(Text(app.Config.DirectoryPath,11));UpdateThemeCards();ShowPage("General");
    }
    static void Paint(FrameworkElement target, DependencyProperty property,string key)=>target.SetResourceReference(property,"Theme"+key);
    TextBlock Text(string text,double size) { var label = new TextBlock { Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,6) }; Paint(label,TextBlock.ForegroundProperty,"Muted"); return label; }
    void Page(string key,string intro)
    {
        var page=new StackPanel{Margin=new Thickness(30,28,30,26)};pages[key]=page;
        page.Children.Add(new TextBlock{Text=l[key],FontSize=28,FontWeight=FontWeights.SemiBold});var description=Text(intro,13);description.Margin=new Thickness(0,9,0,24);page.Children.Add(description);
        body=new StackPanel{Margin=new Thickness(22,10,22,22)};
        var card=new Border{Child=body,CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1)};Paint(card,Border.BackgroundProperty,"Surface");Paint(card,Border.BorderBrushProperty,"Stroke");page.Children.Add(card);
    }
    void ShowPage(string key)
    {
        pageHost.Content=pages[key];
        foreach(var pair in navButtons)Paint(pair.Value,Control.BackgroundProperty,pair.Key==key?"Selected":"Surface");
        if(IsLoaded&&SystemParameters.ClientAreaAnimation)pageHost.BeginAnimation(OpacityProperty,new DoubleAnimation(.5,1,TimeSpan.FromMilliseconds(130)));
    }
    void UpdateThemeCards(){foreach(var pair in themeCards)Paint(pair.Value,Border.BorderBrushProperty,pair.Key==selectedTheme?"Accent":"Stroke");}
    void AddRoot(string path){if(!roots.Contains(path,StringComparer.OrdinalIgnoreCase))roots.Add(path);}
    void Label(string text)=>body.Children.Add(new TextBlock{Text=text,FontSize=13,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,18,0,10),TextWrapping=TextWrapping.Wrap});
    Button Button(string text,Func<Task> action)
    {
        var button=new Button{Content=text};button.Click+=async(_,_)=>{IsEnabled=false;try{await action();}catch(Exception ex){app.Config.Log(ex);MessageBox.Show(l["Error"]);}finally{IsEnabled=true;}};return button;
    }
    void FolderList(ObservableCollection<string> items)
    {
        var list=new ListBox{ItemsSource=items,Height=100};Paint(list,Control.BackgroundProperty,"Input");body.Children.Add(list);
        var actions=new WrapPanel();actions.Children.Add(Button(l["Add"],()=>{var dialog=new OpenFolderDialog{Multiselect=true};if(dialog.ShowDialog(this)==true)foreach(var path in dialog.FolderNames)if(!items.Contains(path,StringComparer.OrdinalIgnoreCase))items.Add(path);return Task.CompletedTask;}));actions.Children.Add(Button(l["Remove"],()=>{if(list.SelectedItem is string path)items.Remove(path);return Task.CompletedTask;}));body.Children.Add(actions);
    }
    async Task Save()
    {
        var webEngines = new List<WebEngine>();
        foreach (var line in engines.Text.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('|',3,StringSplitOptions.TrimEntries);
            if (parts.Length != 3 || parts[0].Length == 0 || parts[0].Any(char.IsWhiteSpace) || parts[1].Length == 0 || !parts[2].Contains("{query}") || !Uri.TryCreate(parts[2].Replace("{query}","test"),UriKind.Absolute,out var uri) || uri.Scheme != "https" || webEngines.Any(e => e.Keyword.Equals(parts[0],StringComparison.OrdinalIgnoreCase))) { MessageBox.Show(l["InvalidSettings"]); return; }
            webEngines.Add(new(parts[0],parts[1],parts[2]));
        }
        if (!HotkeyService.Parse(hotkey.Text,out _,out _) || roots.Concat(exclusions).Any(p => !Path.IsPathFullyQualified(p))) { MessageBox.Show(l["InvalidSettings"]); return; }
        var settings = new AppSettings { Theme = selectedTheme, Language = selectedLanguage, Roots = roots.Select(Path.GetFullPath).ToList(), Exclusions = exclusions.Select(Path.GetFullPath).ToList(), BrowserHistory = browser.IsChecked == true, StartWithWindows = startup.IsChecked == true, Hotkey = hotkey.Text, WebEngines = webEngines };
        if (await app.ApplySettingsAsync(settings)) { committed = true; Close(); }
    }
}










