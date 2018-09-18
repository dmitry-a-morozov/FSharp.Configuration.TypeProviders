module Tests.Json

open Xunit
open FSharp.Configuration.TypeProviders
open Microsoft.Extensions.Configuration

type AppSettings = JsonConfiguration<"appsettings.json">

let config = ConfigurationBuilder().AddJsonFile(__SOURCE_DIRECTORY__ + @"\appsettings.json").Build()

[<Fact>]
let path() =
    Assert.Equal("Section1", AppSettings.Section1.Path)
    Assert.Equal("Section1:Key11", AppSettings.Section1.Key11.Path)
    Assert.Equal("Section1:Key12", AppSettings.Section1.Key12.Path)
    Assert.Equal("Section1:SubSection11", AppSettings.Section1.SubSection11.Path)
    Assert.Equal("Section1:SubSection11:SubKey11", AppSettings.Section1.SubSection11.SubKey11.Path)

[<Fact>]
let values() =
    Assert.Equal<string>(
        config.GetValue(AppSettings.Section1.Key11.Path), 
        AppSettings.Section1.Key11.Value
    )
    Assert.Equal<string>(
        config.GetValue(AppSettings.Section1.Key12.Path), 
        AppSettings.Section1.Key12.Value
    )
    Assert.Equal<string>(
        config.GetValue(AppSettings.Section1.SubSection11.SubKey11.Path), 
        AppSettings.Section1.SubSection11.SubKey11.Value
    )
    Assert.Equal<string>(
        config.GetValue(AppSettings.Section2.Key21.Path), 
        AppSettings.Section2.Key21.Value
    )

[<Fact>]
let typed() =
    let appSettings = config.Get<AppSettings.InstanceType>()
    Assert.NotNull(appSettings)
    Assert.Equal<string>(
        config.GetValue(AppSettings.Section1.Key11.Path),
        appSettings.Section1.Key11
    )
    Assert.Equal<string>(
        config.GetValue(AppSettings.Section1.Key12.Path), 
        appSettings.Section1.Key12.Value
    )
    Assert.Equal<string>(
        config.GetValue(AppSettings.Section1.SubSection11.SubKey11.Path), 
        appSettings.Section1.SubSection11.SubKey11.Value
    )
    Assert.Equal<string>(
        config.GetValue(AppSettings.Section2.Key21.Path), 
        appSettings.Section2.Key21.Value
    )
