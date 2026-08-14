using System.Windows;
using Lunaris.Core.Interfaces;
using Lunaris.Core.Models;
using Lunaris.Infrastructure.Windows;

namespace Lunaris.Core.Services;

/// <summary>Swaps the active theme resource dictionary.</summary>
public sealed class ThemeService : IThemeService
{
    private const string ThemeDictionaryPrefix = "pack://application:,,,/Lunaris;component/UI/Themes/";

    private ResourceDictionary? _themeDictionary;

    public void Apply(AppSettings settings) => Apply(settings.Theme);

    public void Apply(string themeName)
    {
        var app = Application.Current;
        if (app is null)
            return;

        var isDark = themeName switch
        {
            "Dark" => true,
            "Light" => false,
            _ => IsSystemUsingDarkTheme(),
        };

        var dictName = isDark ? "Dark.xaml" : "Light.xaml";
        var source = new Uri(ThemeDictionaryPrefix + dictName, UriKind.Absolute);

        if (_themeDictionary is not null && _themeDictionary.Source == source)
            return;

        var dictionaries = app.Resources.MergedDictionaries;

        // Never remove Base.xaml: it holds all shared styles. Only swap the theme palette.
        if (_themeDictionary is not null)
            dictionaries.Remove(_themeDictionary);

        _themeDictionary = new ResourceDictionary { Source = source };
        dictionaries.Insert(0, _themeDictionary);
    }

    public bool IsSystemUsingDarkTheme() => SystemThemeDetector.IsDarkThemeActive();
}