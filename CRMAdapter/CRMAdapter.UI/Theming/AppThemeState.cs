// AppThemeState.cs: Manages light/dark MudBlazor themes, typography, and snackbar configuration for the UI.
using System;
using MudBlazor;

namespace CRMAdapter.UI.Theming;

public sealed class AppThemeState
{
    private readonly MudTheme _lightTheme;
    private readonly MudTheme _darkTheme;
    private bool _isDarkMode;

    public AppThemeState()
    {
        _lightTheme = BuildLightTheme();
        _darkTheme = BuildDarkTheme();
        SnackbarConfiguration = CreateSnackbarConfiguration();
    }

    public bool IsDarkMode => _isDarkMode;

    public MudTheme ActiveTheme => _isDarkMode ? _darkTheme : _lightTheme;

    public SnackbarConfiguration SnackbarConfiguration { get; }

    public event Action? OnChange;

    public void SetDarkMode(bool isDark)
    {
        if (_isDarkMode == isDark)
        {
            return;
        }

        _isDarkMode = isDark;
        NotifyStateChanged();
    }

    public void ToggleTheme() => SetDarkMode(!_isDarkMode);

    private void NotifyStateChanged() => OnChange?.Invoke();

    private static MudTheme BuildLightTheme()
    {
        var paletteLight = new PaletteLight
        {
            Primary = "#3661FF",
            Secondary = "#E450A0",
            Tertiary = "#0099FF",
            Info = "#2D9BF0",
            Success = "#3CC796",
            Warning = "#FFB74D",
            Error = "#F87171",
            Background = "#F5F7FB",
            Surface = "#FFFFFF",
            AppbarBackground = "#0B1A36",
            DrawerBackground = "#102047",
            DrawerText = "rgba(255,255,255,0.86)",
            TextPrimary = "rgba(15,23,42,0.92)",
            TextSecondary = "rgba(51,65,85,0.78)",
            Divider = "rgba(148,163,184,0.4)"
        };

        var paletteDark = new PaletteDark
        {
            Primary = "#7CA1FF",
            Secondary = "#F48FB1",
            Tertiary = "#4FC3F7",
            Info = "#55C2FF",
            Success = "#32D296",
            Warning = "#FFCA7A",
            Error = "#FF8A80",
            Background = "#0F172A",
            Surface = "#111C2E",
            AppbarBackground = "#111C2E",
            DrawerBackground = "#0C1424",
            DrawerText = "rgba(255,255,255,0.85)",
            TextPrimary = "rgba(255,255,255,0.92)",
            TextSecondary = "rgba(226,232,240,0.7)",
            Divider = "rgba(94,106,132,0.5)"
        };

        return new MudTheme
        {
            PaletteLight = paletteLight,
            PaletteDark = paletteDark,
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "12px"
            }
        };
    }

    private static MudTheme BuildDarkTheme()
    {
        var lightTheme = BuildLightTheme();
        return new MudTheme
        {
            PaletteDark = lightTheme.PaletteDark,
            LayoutProperties = lightTheme.LayoutProperties
        };
    }

    private static SnackbarConfiguration CreateSnackbarConfiguration()
    {
        return new SnackbarConfiguration
        {
            PositionClass = Defaults.Classes.Position.TopCenter,
            PreventDuplicates = true,
            HideTransitionDuration = 100,
            ShowTransitionDuration = 150,
            VisibleStateDuration = 5000,
            BackgroundBlurred = true
        };
    }
}
