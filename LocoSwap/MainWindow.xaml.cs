using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using LocoSwap.Properties;
using Serilog;

namespace LocoSwap
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<Route> Routes { get; } = new ObservableCollection<Route>();
        public ObservableCollection<Scenario> Scenarios { get; } = new ObservableCollection<Scenario>();
        public string WindowTitle { get; set; } = "LocoSwap";

        private int _busyCount;
        public bool IsBusy => _busyCount > 0;

        private void EnterBusy()
        {
            _busyCount++;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
        }

        private void ExitBusy()
        {
            if (_busyCount > 0) _busyCount--;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
        }
        public MainWindow()
        {
            InitializeComponent();
            ThemeManager.Watch(this);
            UpdateColumnVisibility();
            RestoreWindowPlacement();

            DataContext = this;
            WindowTitle = "LocoSwap " + Assembly.GetEntryAssembly().GetName().Version.ToString();

            FileSystemWatcher watcher = new FileSystemWatcher(Path.Combine(Properties.Settings.Default.TsPath, "Content"));

            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite;

            watcher.Changed += OnSDBCacheUpdate;
            watcher.Created += OnSDBCacheUpdate;

            watcher.Filter = "SDBCache.bin";
            watcher.IncludeSubdirectories = false;
            watcher.EnableRaisingEvents = true;

            Loaded += On_MainWindow_Loaded;
        }

        private bool _initialLoadDone;

        private async void On_MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialLoadDone) return;
            _initialLoadDone = true;

            // Add filter to RouteList
            CollectionView RouteListview = (CollectionView)CollectionViewSource.GetDefaultView(RouteList.ItemsSource);
            RouteListview.Filter = RouteFilter;

            // Add filter to ScenarioList
            CollectionView ScenarioListview = (CollectionView)CollectionViewSource.GetDefaultView(ScenarioList.ItemsSource);
            ScenarioListview.Filter = ScenarioFilter;

            await LoadRoutesAsync();

            // Asynchronously read the scenario DB to populate the Scenario completion status
            ReadScenarioDb();
        }

        /// <summary>
        /// (Re)enumerate the routes off the UI thread. Also used after a language change, since
        /// route and scenario display names are resolved in the currently selected language.
        /// </summary>
        private async Task LoadRoutesAsync()
        {
            Route[] routes;
            EnterBusy();
            try
            {
                routes = await Task.Run(Route.ListAllRoutes);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to enumerate routes");
                MessageBox.Show(
                    "LocoSwap could not read the Train Simulator routes folder.\n\n" + ex.Message +
                    "\n\nCheck the Train Simulator path in settings. Details are in debug.log.",
                    LocoSwap.Language.Resources.msg_error, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            finally
            {
                ExitBusy();
            }

            string previouslySelectedId = (RouteList.SelectedItem as Route)?.Id ?? Settings.Default.LastRouteId;

            foreach (Route existing in Routes)
            {
                existing.PropertyChanged -= Route_PropertyChanged;
            }
            Routes.Clear();
            Scenarios.Clear();

            foreach (Route route in routes)
            {
                route.PropertyChanged += Route_PropertyChanged;
                Routes.Add(route);
            }

            // Reselect the route that was active before
            if (!string.IsNullOrEmpty(previouslySelectedId))
            {
                Route previous = Routes.FirstOrDefault(r => r.Id == previouslySelectedId);
                if (previous != null) RouteList.SelectedItem = previous;
            }

            // A populated routes folder that yields nothing almost always means a wrong TS path
            bool routesFolderHasContent = Directory.Exists(Route.GetRoutesDirectory())
                && Directory.EnumerateDirectories(Route.GetRoutesDirectory()).GetEnumerator().MoveNext();
            if (routes.Length == 0 && routesFolderHasContent)
            {
                MessageBox.Show(
                    "No routes could be loaded, although the routes folder is not empty. " +
                    "Some routes may be corrupt, or the Train Simulator path may be wrong. See debug.log for details.",
                    LocoSwap.Language.Resources.msg_error, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RestoreWindowPlacement()
        {
            string saved = Settings.Default.MainWindowPlacement;
            if (string.IsNullOrWhiteSpace(saved)) return;

            string[] parts = saved.Split(';');
            if (parts.Length != 5) return;
            if (!(double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double left)
                & double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double top)
                & double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double width)
                & double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double height)))
            {
                return;
            }
            if (width < 300 || height < 200) return;

            // Ignore a saved rectangle that no longer lands on any monitor
            var virtualScreen = new Rect(
                SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            if (!virtualScreen.IntersectsWith(new Rect(left, top, width, height))) return;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
            Width = width;
            Height = height;
            if (parts[4] == "Maximized") WindowState = WindowState.Maximized;
        }

        private void SaveWindowPlacement()
        {
            Rect bounds = WindowState == WindowState.Normal
                ? new Rect(Left, Top, Width, Height)
                : RestoreBounds;
            Settings.Default.MainWindowPlacement = string.Join(";",
                bounds.Left.ToString(CultureInfo.InvariantCulture),
                bounds.Top.ToString(CultureInfo.InvariantCulture),
                bounds.Width.ToString(CultureInfo.InvariantCulture),
                bounds.Height.ToString(CultureInfo.InvariantCulture),
                WindowState == WindowState.Maximized ? "Maximized" : "Normal");
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                SaveWindowPlacement();
                Settings.Default.LastRouteId = (RouteList.SelectedItem as Route)?.Id ?? "";
                Settings.Default.Save();
            }
            catch (Exception ex)
            {
                Log.Debug("Could not persist window state: {0}", ex.Message);
            }
            base.OnClosing(e);
        }

        private void Route_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsFavorite")
            {
                Route route = sender as Route;
                var favorite = Properties.Settings.Default.FavoriteRoutes;
                if (route.IsFavorite)
                {
                    Log.Debug("Adding {0} to favorite..", route.Name);
                    favorite.Add(route.Id);
                }
                else
                {
                    Log.Debug("Removing {0} from favorite..", route.Name);
                    favorite.Remove(route.Id);
                }
                Properties.Settings.Default.Save();
            }
        }

        private void RouteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RouteList.SelectedItem != null)
            {
                Refresh_Scenario_List();
            }
            else
            {
                Scenarios.Clear();
            }
        }

        private CancellationTokenSource _consistCheckCts;

        private async void Refresh_Scenario_List()
        {
            // Abandon any consist check still running for the previously selected route
            _consistCheckCts?.Cancel();

            Scenarios.Clear();
            Route route = (Route)RouteList.SelectedItem;
            if (route == null) return;

            List<Scenario> scenarios;
            EnterBusy();
            try
            {
                scenarios = await Task.Run(() => BuildScenarioList(route));
            }
            catch (Exception e)
            {
                Log.Error("Failed to list scenarios for route {0}: {1}", route.Id, e.Message);
                return;
            }
            finally
            {
                ExitBusy();
            }

            // The selection may have moved on while we were loading
            if (!ReferenceEquals(RouteList.SelectedItem, route)) return;

            foreach (Scenario scenario in scenarios)
            {
                Scenarios.Add(scenario);
            }
            CollectionViewSource.GetDefaultView(ScenarioList.ItemsSource)?.Refresh();

            if (Settings.Default.CheckScenarioConsists)
            {
                await CheckAllConsistsAsync(scenarios, route);
            }
        }

        /// <summary>
        /// Fill in every scenario's consist status dot. serz + parse runs in parallel across
        /// scenarios (each is an independent process), cached results come back instantly, and
        /// each row's dot updates as soon as its own check finishes.
        /// </summary>
        private async Task CheckAllConsistsAsync(List<Scenario> scenarios, Route route)
        {
            var cts = new CancellationTokenSource();
            _consistCheckCts = cts;

            EnterBusy();
            try
            {
                int degree = Math.Clamp(Environment.ProcessorCount - 1, 1, 8);
                await Task.Run(() => Parallel.ForEach(
                    scenarios,
                    new ParallelOptions { MaxDegreeOfParallelism = degree, CancellationToken = cts.Token },
                    scenario =>
                    {
                        scenario.CheckConsists();
                        Dispatcher.BeginInvoke(new Action(scenario.NotifyConsistCheckComplete));
                    }));
            }
            catch (OperationCanceledException)
            {
                // Route selection moved on; nothing to report
            }
            catch (Exception e)
            {
                Log.Error("Consist check failed for route {0}: {1}", route.Id, e.Message);
            }
            finally
            {
                ExitBusy();
                ScenarioConsistCache.Flush();
                if (ReferenceEquals(_consistCheckCts, cts)) _consistCheckCts = null;
                cts.Dispose();
            }
        }

        internal static List<Scenario> BuildScenarioList(Route route)
        {
            var scenarios = new List<Scenario>();
            string routeDirectory = Route.GetRouteDirectory(route.Id);
            string scenariosDirectory = Scenario.GetScenariosDirectory(route.Id);

            if (Directory.Exists(scenariosDirectory))
            {
                string[] scenarioDirectories = Directory.GetDirectories(scenariosDirectory);
                foreach (string directory in scenarioDirectories)
                {
                    string id = new DirectoryInfo(directory).Name;
                    string xmlPath = Path.Combine(directory, "ScenarioProperties.xml");
                    string xmlPathIfArchived = Path.Combine(directory, "ScenarioPropertiesLocoSwapOff.xml");
                    string binPath = Path.Combine(directory, "Scenario.bin");
                    if (!File.Exists(binPath) || (!File.Exists(xmlPath) && !File.Exists(xmlPathIfArchived))) continue;
                    scenarios.Add(new Scenario(route, id, ""));
                }
            }
            string[] allowedExtensions = new[] { ".ap", ".ap.LSoff" };
            string[] apFiles = Directory.GetFiles(routeDirectory, "*", SearchOption.TopDirectoryOnly).Where(file => allowedExtensions.Any(file.EndsWith)).ToArray();
            var scenarioPropFileRegex = new Regex(@"^(Scenarios/([a-f\d\-]{36})/)ScenarioProperties\.xml$");
            foreach (string apPath in apFiles)
            {
                try
                {
                    IReadOnlyList<string> entryNames = ApArchiveIndex.GetEntryNames(apPath);
                    foreach (string name in entryNames)
                    {
                        Match match = scenarioPropFileRegex.Match(name);
                        if (!match.Success) continue;

                        bool hasScenarioBin = entryNames.Contains(match.Groups[1].Value + "Scenario.bin");
                        bool alreadyUnpacked = File.Exists(
                            Path.Combine(routeDirectory, "Scenarios", match.Groups[2].Value, "ScenarioProperties.xml"));
                        if (hasScenarioBin && !alreadyUnpacked)
                        {
                            scenarios.Add(new Scenario(route, match.Groups[2].Value, apPath));
                        }
                        else if (!hasScenarioBin)
                        {
                            // Template/Quick Drive scenarios ship only ScenarioProperties.xml with no
                            // editable Scenario.bin - skip them (trying to open one used to crash).
                            Log.Debug("Skipping scenario {0} in {1}: no Scenario.bin", match.Groups[2].Value, Path.GetFileName(apPath));
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Couldn't read " + apPath + " for scenarios, " + e.Message);
                }
            }
            return scenarios;
        }

        private void EditScenarioButton_Click(object sender, RoutedEventArgs e)
        {
            string routeId = ((Route)RouteList.SelectedItem).Id;
            Scenario scenario = (Scenario)ScenarioList.SelectedItem;
            new ScenarioEditWindow(routeId, scenario).Show();
        }

        private void OpenScenarioDirButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (Scenario scenario in ScenarioList.SelectedItems)
            {
                if (scenario.ApFileName == "")
                {
                    Process.Start(new ProcessStartInfo(scenario.ScenarioDirectory) { UseShellExecute = true });
                }
            }
        }

        private void ScanAllConsistsButton_Click(object sender, RoutedEventArgs e)
        {
            new ConsistScanWindow { Owner = this }.Show();
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            bool previousCheckScenarioConsistsValue = Settings.Default.CheckScenarioConsists;
            string previousLanguage = Settings.Default.Language;

            new SettingsWindow { Owner = this }.ShowDialog();

            if (previousCheckScenarioConsistsValue != Settings.Default.CheckScenarioConsists)
            {
                UpdateColumnVisibility();
            }

            // Route/scenario display names are language-specific - re-read them
            if (previousLanguage != Settings.Default.Language)
            {
                await LoadRoutesAsync();
            }
        }

        private void ScenarioList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dataContext = ((FrameworkElement)e.OriginalSource).DataContext;
            if (dataContext is Scenario)
            {
                if (RouteList.SelectedItem == null) return;
                string routeId = ((Route)RouteList.SelectedItem).Id;
                new ScenarioEditWindow(routeId, (Scenario)dataContext).Show();
            }
        }

        private void Delete_Scenarios_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult msgResult = MessageBox.Show(LocoSwap.Language.Resources.scenario_delete_prompt_message, LocoSwap.Language.Resources.scenario_delete_prompt_title, MessageBoxButton.YesNoCancel);
            if (msgResult == MessageBoxResult.Yes)
            {
                foreach (Scenario scenario in ScenarioList.SelectedItems)
                {
                    scenario.Delete();
                }
                if (RouteList.SelectedItem != null)
                {
                    Refresh_Scenario_List();
                }
            }
        }

        private void ToggleArchiveRoutes_Click(object sender, RoutedEventArgs e)
        {
            if (ScenarioDb.dbState != ScenarioDb.DBState.Loaded)
            {
                MessageBoxResult msgResult = MessageBox.Show(LocoSwap.Language.Resources.archive_without_db_loaded_prompt_message, LocoSwap.Language.Resources.archive_without_db_loaded_prompt_title, MessageBoxButton.YesNoCancel);
                if (msgResult != MessageBoxResult.Yes)
                {
                    return;
                }
            }
            foreach (Route route in RouteList.SelectedItems)
            {
                route.ToggleArchive();
            }
        }

        private void ArchiveAllButSelectedRoutes_Click(object sender, RoutedEventArgs e)
        {
            foreach (Route route in Routes)
            {
                if (!route.IsArchived && !RouteList.SelectedItems.Contains(route) && !(Properties.Settings.Default.DoNotAutoArchiveWorkshopRoutes && route.IsWorkshop)
                    ||
                    route.IsArchived && RouteList.SelectedItems.Contains(route))
                {
                    route.ToggleArchive();
                }
            }
        }

        private bool RouteFilter(object item)
        {
            if (string.IsNullOrEmpty(RouteFilterTextbox.Text))
                return true;

            Route candidateRoute = item as Route;

            string[] filteredProperties = {
                candidateRoute.Id,
                candidateRoute.Name
            };

            return RouteFilterTextbox.Text.Split(' ').All(
                filterToken => filteredProperties.Where(
                    prop => prop?.IndexOf(filterToken, StringComparison.OrdinalIgnoreCase) >= 0).ToArray().Length > 0
                );
        }

        private bool ScenarioFilter(object item)
        {
            Scenario candidateScenario = item as Scenario;

            // Hide played scenarios ?
            if (HidePlayedScenariosCheckbox.IsChecked == true && (
                candidateScenario.Completion == ScenarioDb.ScenarioCompletion.CompletedSuccessfully ||
                candidateScenario.Completion == ScenarioDb.ScenarioCompletion.CompletedFailed)
                )
            {
                return false;
            }

            // Textual filter
            if (string.IsNullOrEmpty(ScenarioFilterTextbox.Text))
                return true;

            string[] filteredProperties = {
                candidateScenario.Id,
                candidateScenario.Name,
                candidateScenario.Description,
                candidateScenario.PlayerTrainName,
                candidateScenario.Author
            };

            return ScenarioFilterTextbox.Text.Split(' ').All(
                filterToken => filteredProperties.Where(
                    prop => prop?.IndexOf(filterToken, StringComparison.OrdinalIgnoreCase) >= 0).ToArray().Length > 0
                );
        }

        public async void ReadScenarioDb()
        {
            Task readDbTask = Task.Run(() =>
            {
                ScenarioDb.ParseScenarioDb();
            });

            Log.Debug("ReadScenarioDb is about to invoke parallel read");

            await Task.WhenAll(readDbTask);

            Log.Debug("SDB is read, refreshing scenarios list");

            // Refresh scenario list with completion
            // Use Dispatcher to update UI on the main (UI) thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                CollectionViewSource.GetDefaultView(ScenarioList.ItemsSource).Refresh();
            });
        }

        private void OnSDBCacheUpdate(object sender, FileSystemEventArgs e)
        {
            FileInfo sdbCacheFileInfo = new FileInfo(Path.Combine(Properties.Settings.Default.TsPath, "Content", "SDBCache.bin"));
            Log.Information("SDBCache.bin updated Event=Changed ! Size = " + sdbCacheFileInfo.Length);

            // When TS rewrites the SDBCache, a first event will be triggered while the file is at 0 byte: ignore
            if (sdbCacheFileInfo.Length > 0)
            {
                ReadScenarioDb();
            }
        }

        public void RouteFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(RouteList.ItemsSource).Refresh();
        }

        private void EmptyRouteFilter_Click(object sender, RoutedEventArgs e)
        {
            RouteFilterTextbox.Text = "";
        }

        public void ScenarioFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(ScenarioList.ItemsSource).Refresh();
        }
        private void EmptyScenarioFilter_Click(object sender, RoutedEventArgs e)
        {
            ScenarioFilterTextbox.Text = "";
        }

        public void HidePlayedScenario_CheckboxChanged(object sender, RoutedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(ScenarioList.ItemsSource).Refresh();
        }

        public void OpenManual_Click(object sender, RoutedEventArgs e)
        {
            Utilities.OpenManual();
        }

        private void UpdateColumnVisibility()
        {
            if (Settings.Default.CheckScenarioConsists)
            {
                AddColumnIfNotExists(CheckScenarioConsists);
            }
            else
            {
                RemoveColumnIfExists(CheckScenarioConsists);
            }
        }

        private void AddColumnIfNotExists(GridViewColumn column)
        {
            GridView gridView = ScenarioList.View as GridView;
            if (gridView != null && !gridView.Columns.Contains(column))
            {
                gridView.Columns.Insert(0, column);
            }
            if (RouteList.SelectedItem != null)
            {
                Refresh_Scenario_List();
            }
        }

        private void RemoveColumnIfExists(GridViewColumn column)
        {
            GridView gridView = ScenarioList.View as GridView;
            if (gridView != null && gridView.Columns.Contains(column))
            {
                gridView.Columns.Remove(column);
            }
            if (RouteList.SelectedItem != null)
            {
                Refresh_Scenario_List();
            }
        }
    }
}
