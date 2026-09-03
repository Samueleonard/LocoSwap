using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;
using LocoSwap.Properties;
using Serilog;

namespace LocoSwap
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Keep the previous session's log so a crash can still be diagnosed after a restart
                if (File.Exists("debug.log"))
                {
                    File.Copy("debug.log", "debug.previous.log", overwrite: true);
                    File.Delete("debug.log");
                }
            }
            catch (Exception)
            {
                Debug.Print("Can not rotate existing log file");
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Debug()
                .WriteTo.File("debug.log",
                    rollingInterval: RollingInterval.Infinite,
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: 20 * 1024 * 1024)
                .CreateLogger();

            Log.Debug("LocoSwap version {0} starting up..", Assembly.GetEntryAssembly().GetName().Version.ToString());

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;

            SetLanguageDictionary();

            ThemeManager.Apply();

            if (!Directory.Exists(Utilities.GetTempDir()))
            {
                Directory.CreateDirectory(Utilities.GetTempDir());
            }

            foreach (string file in Directory.GetFiles(Utilities.GetTempDir()))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            // Per-scenario working folders left behind by an interrupted consist check
            foreach (string dir in Directory.GetDirectories(Utilities.GetTempDir()))
            {
                try { Directory.Delete(dir, true); } catch { /* best effort */ }
            }

            while (Settings.Default.TsPath == "")
            {
                MessageBox.Show(Language.Resources.msg_first_time, Language.Resources.msg_message, MessageBoxButton.OK, MessageBoxImage.Information);
                var selected = Utilities.ChangeTsPath();
                if (!selected)
                {
                    MessageBox.Show(Language.Resources.msg_ts_path_required, Language.Resources.msg_message, MessageBoxButton.OK, MessageBoxImage.Information);
                    Current.Shutdown();
                    return;
                }
            }

            Log.Debug("SwapPreset has {0} items", Settings.Default.Preset.List.Count);

            // Set localisation properly (to display WPF dates in the local format)
            FrameworkElement.LanguageProperty.OverrideMetadata(
              typeof(FrameworkElement),
              new FrameworkPropertyMetadata(
                  XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)
                  )
              );

            new MainWindow().Show();
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            ReportFatalException(e.Exception, "Dispatcher");
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ReportFatalException(e.ExceptionObject as Exception, "AppDomain");
        }

        private static bool _fatalReported;

        private void ReportFatalException(Exception exception, string source)
        {
            if (_fatalReported) return; // avoid a second dialog while we are already tearing down
            _fatalReported = true;

            Log.Fatal(exception, "Unhandled exception ({Source})", source);
            Log.CloseAndFlush();

            try
            {
                string logPath = Path.GetFullPath("debug.log");
                string details = exception == null
                    ? "Unknown error."
                    : exception.GetType().Name + ": " + exception.Message;
                MessageBox.Show(
                    "LocoSwap hit an unexpected error and needs to close.\n\n" +
                    details + "\n\nA log has been saved to:\n" + logPath,
                    Language.Resources.msg_error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch
            {
                // If even the dialog fails there is nothing more we can do
            }

            Current.Shutdown();
        }

        public void SetLanguageDictionary()
        {
            var lang = Settings.Default.Language;
            if (lang == "") lang = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            Log.Debug("Set language to {0}", lang);
            switch (lang)
            {
                case "de":
                    Language.Resources.Culture = new System.Globalization.CultureInfo("de-DE");
                    break;
                case "fr":
                    Language.Resources.Culture = new System.Globalization.CultureInfo("fr-FR");
                    break;
                case "it":
                    Language.Resources.Culture = new System.Globalization.CultureInfo("it-IT");
                    break;
                case "es":
                    Language.Resources.Culture = new System.Globalization.CultureInfo("es-ES");
                    break;
                case "pl":
                    Language.Resources.Culture = new System.Globalization.CultureInfo("pl-PL");
                    break;
                case "nl":
                    Language.Resources.Culture = new System.Globalization.CultureInfo("nl-NL");
                    break;
                case "ru":
                    Language.Resources.Culture = new System.Globalization.CultureInfo("ru-RU");
                    break;
                case "en":
                default:
                    Language.Resources.Culture = new System.Globalization.CultureInfo("en-US");
                    break;
            }

            // Push the new strings into every {loc:Loc ...} binding already on screen
            LocalizationSource.Instance.Refresh();
        }
    }
}
