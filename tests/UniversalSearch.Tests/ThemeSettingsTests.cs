using System.Text.Json;
using UniversalSearch.Core;
using UniversalSearch.Infrastructure;
using Xunit;

public sealed class ThemeSettingsTests
{
    [Fact]
    public void LegacySettingsKeepLightThemeAndExistingPreferences()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("""{"Language":"tr","Roots":["C:\\Work"],"BrowserHistory":false,"Hotkey":"Ctrl+Shift+F"}""")!;
        Assert.Equal("light", settings.Theme);
        Assert.Equal("C:\\Work", Assert.Single(settings.Roots));
        Assert.Equal("Ctrl+Shift+F", settings.Hotkey);
        Assert.False(settings.BrowserHistory);
    }

    [Fact]
    public void ThemeSurvivesConfigurationReloadWithoutChangingSources()
    {
        var directory = Path.Combine(Path.GetTempPath(), "UniversalSearch-theme-" + Guid.NewGuid());
        try
        {
            var config = new Configuration(directory);
            config.Save(new AppSettings { Theme = "dark", Roots = ["C:\\Work"], Exclusions = ["C:\\Work\\cache"] });
            var loaded = new Configuration(directory).Settings;
            Assert.Equal("dark", loaded.Theme);
            Assert.Equal("C:\\Work", Assert.Single(loaded.Roots));
            Assert.Equal("C:\\Work\\cache", Assert.Single(loaded.Exclusions));
        }
        finally { Directory.Delete(directory, true); }
    }
}
