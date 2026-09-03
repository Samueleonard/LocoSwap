using System;
using System.Windows;
using Serilog;
using Wpf.Ui.Appearance;
using LocoSwap.Properties;

namespace LocoSwap
{
    /// <summary>
    /// Applies the WPF-UI application theme from <see cref="Settings.Theme"/>
    /// ("System" / "Light" / "Dark") and keeps "System" following the OS setting.
    /// </summary>
    public static class ThemeManager
    {
        /// <summary>Apply the currently configured theme. Safe to call repeatedly.</summary>
        public static void Apply()
        {
            try
            {
                switch (Settings.Default.Theme)
                {
                    case "Light":
                        ApplicationThemeManager.Apply(ApplicationTheme.Light);
                        break;
                    case "Dark":
                        ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                        break;
                    default:
                        ApplicationThemeManager.ApplySystemTheme();
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not apply theme {Theme}", Settings.Default.Theme);
            }
        }

        /// <summary>
        /// Called once from the main window so "System" mode reacts to a live OS theme change.
        /// </summary>
        public static void Watch(Window window)
        {
            try
            {
                SystemThemeWatcher.Watch(window);
            }
            catch (Exception ex)
            {
                Log.Debug("SystemThemeWatcher.Watch failed: {Message}", ex.Message);
            }
        }
    }
}
