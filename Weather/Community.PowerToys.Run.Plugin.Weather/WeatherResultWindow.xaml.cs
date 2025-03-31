using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.IO;

namespace Community.PowerToys.Run.Plugin.Weather
{
    public partial class WeatherResultWindow : Window
    {
        public WeatherResultWindow()
        {
            InitializeComponent();
        }

        public void SetWeatherData(string title, string details, string iconPath = null)
        {
            // Parse the details text
            string[] lines = details.Split('\n');

            TitleTextBlock.Text = title;

            if (lines.Length > 0)
                ConditionTextBlock.Text = lines[0]; // First line is condition

            if (lines.Length > 1)
                TemperatureTextBlock.Text = lines[1]; // Temperature

            if (lines.Length > 2)
                FeelsLikeTextBlock.Text = lines[2]; // Feels like

            if (lines.Length > 3)
                HumidityTextBlock.Text = lines[3]; // Humidity

            if (lines.Length > 4)
                WindTextBlock.Text = lines[4]; // Wind

            if (lines.Length > 5)
                TimeTextBlock.Text = lines[5]; // Local time

            // Set weather icon if provided
            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
            {
                try
                {
                    WeatherIcon.Source = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading weather icon: {ex.Message}");
                }
            }
            else
            {
                // Use default icon or hide the image
                WeatherIcon.Visibility = Visibility.Collapsed;
            }
        }
    }
}