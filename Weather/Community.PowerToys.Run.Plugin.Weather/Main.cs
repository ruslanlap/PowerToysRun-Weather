using ManagedCommon;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Net.Http;
using System.Diagnostics;
using Wox.Plugin;
using Microsoft.PowerToys.Settings.UI.Library;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.IO;
using Community.PowerToys.Run.Plugin.Weather.Models;
using Community.PowerToys.Run.Plugin.Weather.Services;

namespace Community.PowerToys.Run.Plugin.Weather
{
    public class Main : IPlugin, IContextMenu, IDisposable, IPluginI18n, ISettingProvider 
    {
        public static string PluginID => "CA864C186BD14B47A41C18BD24F5F4FA";
        public string Name => Properties.Resources.plugin_name;
        public string Description => Properties.Resources.plugin_description;

        private PluginInitContext Context { get; set; }
        private string IconPath { get; set; }
        private bool Disposed { get; set; }

        private readonly HttpClient _httpClient;
        private WeatherSettings _settings;
        private GeocodingService _geocodingService;
        private OpenMeteoService _weatherService;

        public Main()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _settings = new WeatherSettings();
            _geocodingService = new GeocodingService(_httpClient);
            _weatherService = new OpenMeteoService(_httpClient, _geocodingService, _settings.CacheMinutes);
        }

        // Clears the weather cache
        public void ClearWeatherCache()
        {
            _weatherService?.ClearCache();
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            var searchTerm = query?.Search?.Trim() ?? string.Empty;

            try
            {
                if (string.IsNullOrEmpty(searchTerm))
                {
                    // Use favorite locations if available
                    if (_settings.FavoriteLocations != null && _settings.FavoriteLocations.Count > 0)
                    {
                        foreach (var location in _settings.FavoriteLocations)
                        {
                            var weatherTask = Task.Run(async () => await _weatherService.GetWeatherForLocationAsync(location));
                            if (weatherTask.Wait(TimeSpan.FromSeconds(3)))
                            {
                                var weather = weatherTask.Result;
                                if (weather != null)
                                {
                                    results.Add(CreateWeatherResult(weather));
                                }
                            }
                        }

                        // Fallback to default location if no favorite weather data is retrieved
                        if (results.Count == 0 && !string.IsNullOrEmpty(_settings.DefaultLocation))
                        {
                            var weatherTask = Task.Run(async () => await _weatherService.GetWeatherForLocationAsync(_settings.DefaultLocation));
                            if (weatherTask.Wait(TimeSpan.FromSeconds(3)))
                            {
                                var weather = weatherTask.Result;
                                if (weather != null)
                                {
                                    results.Add(CreateWeatherResult(weather));
                                }
                            }
                        }

                        // Fallback to geolocation if still no results
                        if (results.Count == 0)
                        {
                            var weatherTask = Task.Run(async () => await _weatherService.GetCurrentLocationWeatherAsync());
                            if (weatherTask.Wait(TimeSpan.FromSeconds(3)))
                            {
                                var weather = weatherTask.Result;
                                if (weather != null)
                                {
                                    results.Add(CreateWeatherResult(weather));
                                }
                            }
                        }

                        return results;
                    }
                    else if (!string.IsNullOrEmpty(_settings.DefaultLocation))
                    {
                        var weatherTask = Task.Run(async () => await _weatherService.GetWeatherForLocationAsync(_settings.DefaultLocation));
                        if (weatherTask.Wait(TimeSpan.FromSeconds(3)))
                        {
                            var weather = weatherTask.Result;
                            if (weather != null)
                            {
                                results.Add(CreateWeatherResult(weather));
                                return results;
                            }
                        }
                    }

                    // Use geolocation if no favorites or default location
                    var currentWeatherTask = Task.Run(async () => await _weatherService.GetCurrentLocationWeatherAsync());
                    if (currentWeatherTask.Wait(TimeSpan.FromSeconds(5)))
                    {
                        var currentWeather = currentWeatherTask.Result;
                        if (currentWeather != null)
                        {
                            results.Add(CreateWeatherResult(currentWeather));
                        }
                    }

                    return results;
                }
                else
                {
                    var weatherTask = Task.Run(async () => await _weatherService.GetWeatherForLocationAsync(searchTerm));
                    if (weatherTask.Wait(TimeSpan.FromSeconds(5)))
                    {
                        var weather = weatherTask.Result;
                        if (weather != null)
                        {
                            results.Add(CreateWeatherResult(weather));
                        }
                        else
                        {
                            results.Add(new Result
                            {
                                Title = string.Format(Properties.Resources.plugin_search_failed, searchTerm),
                                SubTitle = "Location not found. Try a different city or postal code",
                                IcoPath = IconPath
                            });
                        }
                    }
                    else
                    {
                        results.Add(new Result
                        {
                            Title = "Request timed out",
                            SubTitle = "Weather information could not be retrieved. Check your internet connection",
                            IcoPath = IconPath
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                results.Add(new Result
                {
                    Title = Properties.Resources.plugin_error_occurred,
                    SubTitle = $"Error: {ex.Message}",
                    IcoPath = IconPath,
                    ContextData = searchTerm
                });
            }

            return results;
        }

        private Result CreateWeatherResult(WeatherData weather)
        {
            var temperatureUnit = _settings.UseCelsius ? "°C" : "°F";
            var temperature = _settings.UseCelsius ? weather.Temperature : CelsiusToFahrenheit(weather.Temperature);
            var feelsLike = _settings.UseCelsius ? weather.FeelsLike : CelsiusToFahrenheit(weather.FeelsLike);
            var windSpeed = _settings.UseCelsius ? weather.WindSpeed : (weather.WindSpeed * 2.237f);

            var iconFile = $"{weather.IconCode}_t@4x.png";
            var iconRelativePath = Path.Combine("Images", "CONDITIONS", iconFile);
            var pluginDir = Context?.CurrentPluginMetadata?.PluginDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
            var fullIconPath = Path.Combine(pluginDir, iconRelativePath);
            var iconPathToUse = File.Exists(fullIconPath) ? iconRelativePath : (IconPath ?? "Images/weather.light.png");

            // Calculate local time
            var localTime = DateTime.UtcNow.AddSeconds(weather.TimezoneOffset);
            return new Result
            {
                Title = $"{weather.Location} | {temperature:F1}{temperatureUnit} - {weather.Condition}",
                SubTitle = 
                    $"   ┌─🌡 {Properties.Resources.plugin_feels_like}: {feelsLike:F1}{temperatureUnit} {GetFeelsLikeEmoji(weather.FeelsLike)}\n" +
                    $"   ├─ 💧 {Properties.Resources.plugin_humidity}: {weather.Humidity}%\n" +
                    $"   ├─ 🌬 {Properties.Resources.plugin_wind_speed}: {windSpeed:F1} {(_settings.UseCelsius ? "m/s" : "mph")}\n" +
                    $"   └─ 🕒 Local time: {localTime:HH:mm}",
                IcoPath = iconPathToUse,
                Action = _ =>
                {
                    var weatherWindow = new WeatherResultWindow();
                    weatherWindow.SetWeatherData(
                        $"{weather.Location} | {temperature:F1}{temperatureUnit}",
                        $"{weather.Condition}\n" +
                        $"Temperature: {temperature:F1}{temperatureUnit}\n" +
                        $"Feels like: {feelsLike:F1}{temperatureUnit}\n" +
                        $"Humidity: {weather.Humidity}%\n" +
                        $"Wind: {windSpeed:F1} {(_settings.UseCelsius ? "m/s" : "mph")}\n" +
                        $"Local time: {localTime:HH:mm}",
                        fullIconPath,
                        weather.FeelsLike
                    );
                    weatherWindow.Show();
                    return true;
                },
                ContextData = weather,
                Score = 100
            };
        }

        private float CelsiusToFahrenheit(float celsius) => (celsius * 9 / 5) + 32;

        private string GetFeelsLikeEmoji(float feelsLikeCelsius)
        {
            if (feelsLikeCelsius < -20) return "🥶❄️";
            if (feelsLikeCelsius < -10) return "🧣🧤";
            if (feelsLikeCelsius < 0) return "🧥🌬️";
            if (feelsLikeCelsius < 10) return "🌫️🍃";
            if (feelsLikeCelsius < 20) return "😊🍂";
            if (feelsLikeCelsius < 25) return "😎🌤️";
            if (feelsLikeCelsius < 30) return "🥵☀️";
            return "🫠🔥";
        }

        public void Init(PluginInitContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());
            _geocodingService = new GeocodingService(_httpClient);
            _weatherService = new OpenMeteoService(_httpClient, _geocodingService, _settings.CacheMinutes);
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            var menuItems = new List<ContextMenuResult>();

            if (selectedResult.ContextData is WeatherData weather)
            {
                menuItems.Add(new ContextMenuResult
                {
                    PluginName = Name,
                    Title = Properties.Resources.plugin_copy_to_clipboard,
                    FontFamily = "Segoe MDL2 Assets",
                    Glyph = "\xE8C8",
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control,
                    Action = _ =>
                    {
                        var temperatureUnit = _settings.UseCelsius ? "°C" : "°F";
                        var temperature = _settings.UseCelsius ? weather.Temperature : CelsiusToFahrenheit(weather.Temperature);
                        var weatherInfo = $"{weather.Location}: {temperature:F1}{temperatureUnit}, {weather.Condition}";
                        Clipboard.SetDataObject(weatherInfo);
                        return true;
                    }
                });

                var isFavorite = _settings.FavoriteLocations.Contains(weather.Location);
                menuItems.Add(new ContextMenuResult
                {
                    PluginName = Name,
                    Title = isFavorite ? Properties.Resources.plugin_remove_favorite : Properties.Resources.plugin_add_favorite,
                    FontFamily = "Segoe MDL2 Assets",
                    Glyph = isFavorite ? "\xEB52" : "\xE734",
                    Action = _ =>
                    {
                        if (isFavorite)
                            _settings.FavoriteLocations.Remove(weather.Location);
                        else
                            _settings.FavoriteLocations.Add(weather.Location);
                        return true;
                    }
                });

                menuItems.Add(new ContextMenuResult
                {
                    PluginName = Name,
                    Title = Properties.Resources.plugin_view_forecast,
                    FontFamily = "Segoe MDL2 Assets",
                    Glyph = "\xE774",
                    Action = _ =>
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = $"https://www.google.com/search?q={Uri.EscapeDataString(weather.Location + " weather forecast")}",
                            UseShellExecute = true
                        });
                        return true;
                    }
                });

                // Refresh weather data
                menuItems.Add(new ContextMenuResult
                {
                    PluginName = Name,
                    Title = "Refresh Weather Data",
                    FontFamily = "Segoe MDL2 Assets",
                    Glyph = "\xE72C",
                    Action = _ =>
                    {
                        _weatherService.ClearCache(weather.Location);
                        return false;
                    }
                });
            }
            else if (selectedResult.ContextData is string searchTerm)
            {
                menuItems.Add(new ContextMenuResult
                {
                    PluginName = Name,
                    Title = Properties.Resources.plugin_copy_to_clipboard,
                    FontFamily = "Segoe MDL2 Assets",
                    Glyph = "\xE8C8",
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control,
                    Action = _ =>
                    {
                        Clipboard.SetDataObject(searchTerm);
                        return true;
                    }
                });
            }

            return menuItems;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed || !disposing)
                return;

            if (Context?.API != null)
                Context.API.ThemeChanged -= OnThemeChanged;
            _httpClient?.Dispose();
            Disposed = true;
        }

        private void UpdateIconPath(Theme theme)
        {
            IconPath = theme == Theme.Light || theme == Theme.HighContrastWhite
                ? "Images/weather.light.png"
                : "Images/weather.dark.png";
        }

        private void OnThemeChanged(Theme currentTheme, Theme newTheme) => UpdateIconPath(newTheme);

        public string GetTranslatedPluginTitle() => Name;
        public string GetTranslatedPluginDescription() => Description;
        public Control CreateSettingPanel() => new WeatherSettingsControl(_settings, this);

        public void SaveSettings()
        {
            var settings = new PowerLauncherPluginSettings { AdditionalOptions = AdditionalOptions.ToList() };
            UpdateSettings(settings);
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            if (settings == null)
                return;

            _settings.DefaultLocation = settings.AdditionalOptions?.FirstOrDefault(x => x.Key == "DefaultLocation")?.TextValue ?? "";

            var cacheString = settings.AdditionalOptions?.FirstOrDefault(x => x.Key == "CacheMinutes")?.TextValue ?? "30";
            if (int.TryParse(cacheString, out int cacheMinutes))
            {
                _settings.CacheMinutes = cacheMinutes;
            }

            var useCelsiusOption = settings.AdditionalOptions?.FirstOrDefault(x => x.Key == "UseCelsius");
            if (useCelsiusOption != null && useCelsiusOption.Value is object value)
            {
                if (bool.TryParse(value.ToString(), out bool useCelsius))
                {
                    _settings.UseCelsius = useCelsius;
                }
            }

            _weatherService = new OpenMeteoService(_httpClient, _geocodingService, _settings.CacheMinutes);
        }

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>
        {
            new PluginAdditionalOption
            {
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                Key = "DefaultLocation",
                DisplayLabel = "Default Location",
                DisplayDescription = "Enter your default location for weather updates",
                TextBoxMaxLength = 100,
                TextValue = _settings.DefaultLocation
            },
            new PluginAdditionalOption
            {
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                Key = "UseCelsius",
                DisplayLabel = "Use Celsius",
                DisplayDescription = "Display temperatures in Celsius if checked",
                Value = _settings.UseCelsius
            },
            new PluginAdditionalOption
            {
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                Key = "CacheMinutes",
                DisplayLabel = "Cache Duration (minutes)",
                DisplayDescription = "Duration to cache weather data (0 to disable)",
                TextBoxMaxLength = 3,
                TextValue = _settings.CacheMinutes.ToString()
            }
        };
    }

    public class WeatherResultItem : Control
    {
        static WeatherResultItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(WeatherResultItem),
                new FrameworkPropertyMetadata(typeof(WeatherResultItem)));
        }

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(WeatherResultItem), new PropertyMetadata(string.Empty));

        public string SubTitle
        {
            get { return (string)GetValue(SubTitleProperty); }
            set { SetValue(SubTitleProperty, value); }
        }
        public static readonly DependencyProperty SubTitleProperty =
            DependencyProperty.Register("SubTitle", typeof(string), typeof(WeatherResultItem), new PropertyMetadata(string.Empty));
    }

    public class WeatherSettings
    {
        public string DefaultLocation { get; set; } = string.Empty;
        public bool UseCelsius { get; set; } = true;
        public List<string> FavoriteLocations { get; set; } = new List<string>();
        public int CacheMinutes { get; set; } = 30;
    }

    public class CachedWeatherData
    {
        public WeatherData Data { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class WeatherData
    {
        public int TimezoneOffset { get; set; }
        public string Location { get; set; }
        public float Temperature { get; set; }
        public float FeelsLike { get; set; }
        public int Humidity { get; set; }
        public float WindSpeed { get; set; }
        public string Condition { get; set; }
        public string Description { get; set; }
        public string IconCode { get; set; }
    }

    public partial class WeatherSettingsControl : UserControl
    {
        private readonly WeatherSettings _settings;
        private readonly Main _main;

        private RadioButton CelsiusRadioButton;
        private RadioButton FahrenheitRadioButton;
        private TextBox DefaultLocationTextBox;
        private TextBox CacheDurationTextBox;
        private ListBox FavoriteLocationsListBox;
        private Button AddFavoriteButton;
        private Button RemoveFavoriteButton;
        private Button SaveButton;
        private Button ClearCacheButton;

        public WeatherSettingsControl(WeatherSettings settings, Main main)
        {
            _settings = settings;
            _main = main;
            InitializeComponent();

            CelsiusRadioButton.IsChecked = _settings.UseCelsius;
            FahrenheitRadioButton.IsChecked = !_settings.UseCelsius;
            DefaultLocationTextBox.Text = _settings.DefaultLocation;
            CacheDurationTextBox.Text = _settings.CacheMinutes.ToString();

            foreach (var location in _settings.FavoriteLocations)
            {
                FavoriteLocationsListBox.Items.Add(location);
            }

            AddFavoriteButton.Click += AddFavoriteButton_Click;
            RemoveFavoriteButton.Click += RemoveFavoriteButton_Click;
            SaveButton.Click += SaveButton_Click;
            ClearCacheButton.Click += ClearCacheButton_Click;
        }

        private void InitializeComponent()
        {
            Grid grid = new Grid();
            Content = grid;

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            int currentRow = 0;

            Label defaultLocLabel = new Label { Content = "Default Location:", Margin = new Thickness(5) };
            Grid.SetRow(defaultLocLabel, currentRow++);
            grid.Children.Add(defaultLocLabel);

            DefaultLocationTextBox = new TextBox { Margin = new Thickness(5) };
            Grid.SetRow(DefaultLocationTextBox, currentRow++);
            grid.Children.Add(DefaultLocationTextBox);

            Label cacheLabel = new Label { Content = "Cache Duration (minutes):", Margin = new Thickness(5) };
            Grid.SetRow(cacheLabel, currentRow++);
            grid.Children.Add(cacheLabel);

            Grid cacheGrid = new Grid();
            cacheGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cacheGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(cacheGrid, currentRow++);
            grid.Children.Add(cacheGrid);

            CacheDurationTextBox = new TextBox { Margin = new Thickness(5) };
            Grid.SetColumn(CacheDurationTextBox, 0);
            cacheGrid.Children.Add(CacheDurationTextBox);

            ClearCacheButton = new Button { Content = "Clear Cache", Margin = new Thickness(5) };
            Grid.SetColumn(ClearCacheButton, 1);
            cacheGrid.Children.Add(ClearCacheButton);

            GroupBox tempUnitGroup = new GroupBox { Header = "Temperature Unit", Margin = new Thickness(5) };
            Grid.SetRow(tempUnitGroup, currentRow++);
            grid.Children.Add(tempUnitGroup);

            StackPanel tempPanel = new StackPanel { Orientation = Orientation.Horizontal };
            tempUnitGroup.Content = tempPanel;

            CelsiusRadioButton = new RadioButton { Content = "Celsius (°C)", Margin = new Thickness(5) };
            tempPanel.Children.Add(CelsiusRadioButton);

            FahrenheitRadioButton = new RadioButton { Content = "Fahrenheit (°F)", Margin = new Thickness(5) };
            tempPanel.Children.Add(FahrenheitRadioButton);

            GroupBox favoritesGroup = new GroupBox { Header = "Favorite Locations", Margin = new Thickness(5) };
            Grid.SetRow(favoritesGroup, currentRow++);
            grid.Children.Add(favoritesGroup);

            Grid favGrid = new Grid();
            favoritesGroup.Content = favGrid;

            favGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            favGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            FavoriteLocationsListBox = new ListBox { Margin = new Thickness(5) };
            Grid.SetRow(FavoriteLocationsListBox, 0);
            favGrid.Children.Add(FavoriteLocationsListBox);

            StackPanel btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
            Grid.SetRow(btnPanel, 1);
            favGrid.Children.Add(btnPanel);

            AddFavoriteButton = new Button { Content = "Add Current Location", Margin = new Thickness(5) };
            btnPanel.Children.Add(AddFavoriteButton);

            RemoveFavoriteButton = new Button { Content = "Remove Selected", Margin = new Thickness(5) };
            btnPanel.Children.Add(RemoveFavoriteButton);

            SaveButton = new Button
            {
                Content = "Save Settings",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = System.Windows.Media.Brushes.LightBlue
            };
            Grid.SetRow(SaveButton, currentRow++);
            grid.Children.Add(SaveButton);
        }

        private void AddFavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            string location = DefaultLocationTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(location) && !FavoriteLocationsListBox.Items.Contains(location))
            {
                FavoriteLocationsListBox.Items.Add(location);
            }
        }

        private void RemoveFavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (FavoriteLocationsListBox.SelectedItem != null)
            {
                FavoriteLocationsListBox.Items.Remove(FavoriteLocationsListBox.SelectedItem);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(CacheDurationTextBox.Text, out int cacheMinutes) || cacheMinutes < 0)
            {
                MessageBox.Show("Please enter a valid cache duration (0 or positive number)", "Validation Error");
                return;
            }

            _settings.UseCelsius = CelsiusRadioButton.IsChecked.GetValueOrDefault(true);
            _settings.DefaultLocation = DefaultLocationTextBox.Text.Trim();
            _settings.CacheMinutes = cacheMinutes;
            _settings.FavoriteLocations.Clear();
            foreach (var item in FavoriteLocationsListBox.Items)
            {
                _settings.FavoriteLocations.Add(item.ToString());
            }

            _main.SaveSettings();
            MessageBox.Show("Settings saved successfully!", "Weather Plugin");
        }

        private void ClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            _main.ClearWeatherCache();
            MessageBox.Show("Weather cache cleared", "Weather Plugin");
        }
    }
}