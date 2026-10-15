using UniversalSearch.Core;
using UniversalSearch.Infrastructure;
using Xunit;
public class ReleaseRegressionTests
{
    [Theory]
    [InlineData(0,5,true,true,0)]
    [InlineData(0,5,false,true,4)]
    [InlineData(0,5,true,false,1)]
    [InlineData(4,5,true,false,4)]
    [InlineData(0,5,false,false,0)]
    [InlineData(-1,0,true,true,-1)]
    public void Navigation(int selected,int count,bool down,bool fromSearch,int expected)
        => Assert.Equal(expected,ResultNavigation.Next(selected,count,down,fromSearch));
    [Fact] public void NullSettingsCollectionsDoNotBreakStartup()
    {
        var dir=Path.Combine(Path.GetTempPath(),"UniversalSearch-config-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        try {
            File.WriteAllText(Path.Combine(dir,"settings.json"),"""{"Roots":null,"Exclusions":[null,""],"WebEngines":[null],"Hotkey":null}""");
            var config=new Configuration(dir);
            Assert.Empty(config.Settings.Roots);Assert.Empty(config.Settings.Exclusions);Assert.Empty(config.Settings.WebEngines);
            Assert.Equal("Alt+Space",config.Settings.Hotkey);
        } finally {Directory.Delete(dir,true);}
    }
}
