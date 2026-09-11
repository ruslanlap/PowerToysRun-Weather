using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Community.PowerToys.Run.Plugin.Weather.Models;

namespace Community.PowerToys.Run.Plugin.Weather.Services
{
    public class OpenMeteoService
    {
        private readonly HttpClient _httpClient;
        private readonly GeocodingService _geocodingService;
        private readonly int _cacheMinutes;
        private readonly ConcurrentDictionary<string, CachedWeatherData> _cache = new ConcurrentDictionary<string, CachedWeatherData>(StringComparer.OrdinalIgnoreCase);

        private const string ForecastBaseUrl = "https://api.open-meteo.com/v1/forecast";

        public OpenMeteoService(HttpClient httpClient, GeocodingService geocodingService, int cacheMinutes = 30)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _geocodingService = geocodingService ?? throw new ArgumentNullException(nameof(geocodingService));
            _cacheMinutes = cacheMinutes;
        }

        public void ClearCache(string location = null)
        {
            if (string.IsNullOrEmpty(location))
                _cache.Clear();
            else
                _cache.TryRemove(location, out _);
        }

        public async Task<WeatherData> GetWeatherForLocationAsync(string location, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(location))
                return null;

            var trimmed = location.Trim();

            // Check cache
            if (_cacheMinutes > 0 && _cache.TryGetValue(trimmed, out var cachedData))
            {
                if (DateTime.Now - cachedData.Timestamp < TimeSpan.FromMinutes(_cacheMinutes))
                    return cachedData.Data;
            }

            try
            {
                var geo = await _geocodingService.ResolveLocationAsync(trimmed, ct);
                if (geo == null)
                {
                    Debug.WriteLine($"Could not resolve coordinates for: {trimmed}");
                    return null;
                }

                var weather = await FetchFromOpenMeteoAsync(geo.Latitude, geo.Longitude, geo.DisplayName ?? geo.Name, ct);
                if (weather != null && _cacheMinutes > 0)
                {
                    _cache[trimmed] = new CachedWeatherData { Data = weather, Timestamp = DateTime.Now };
                }

                return weather;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching weather for {location}: {ex.Message}");
                return null;
            }
        }

        public async Task<WeatherData> GetCurrentLocationWeatherAsync(CancellationToken ct = default)
        {
            try
            {
                var response = await _httpClient.GetAsync("http://ip-api.com/json/", ct);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("status", out var status) && status.GetString() == "success")
                    {
                        var lat = root.GetProperty("lat").GetDouble();
                        var lon = root.GetProperty("lon").GetDouble();
                        var city = root.TryGetProperty("city", out var cityProp) ? cityProp.GetString() : "Current Location";
                        var country = root.TryGetProperty("country", out var countryProp) ? countryProp.GetString() : "";

                        var locationLabel = string.IsNullOrEmpty(country) ? city : $"{city}, {country}";
                        return await FetchFromOpenMeteoAsync(lat, lon, locationLabel, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching current IP location: {ex.Message}");
            }

            // Fallback to Kyiv if IP lookup fails
            return await GetWeatherForLocationAsync("Kyiv", ct);
        }

        public async Task<WeatherData> FetchFromOpenMeteoAsync(double latitude, double longitude, string locationName, CancellationToken ct = default)
        {
            try
            {
                var url = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}?latitude={1}&longitude={2}&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m,wind_direction_10m,is_day&timezone=auto",
                    ForecastBaseUrl,
                    latitude,
                    longitude);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("PowerToys-Run-Weather/1.0");

                var response = await _httpClient.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Open-Meteo returned status {response.StatusCode}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var result = JsonSerializer.Deserialize<OpenMeteoWeatherResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Current == null)
                    return null;

                var isDay = result.Current.IsDay == 1;
                var (condition, iconCode) = MapWmoCode(result.Current.WeatherCode, isDay);

                // Open-Meteo wind_speed_10m default is km/h. Convert to m/s to match ruslanlap's internal unit expectations
                // (ruslanlap converts m/s -> mph if imperial, or displays m/s if metric)
                var windSpeedMs = result.Current.WindSpeed / 3.6f;

                return new WeatherData
                {
                    Location = locationName,
                    Temperature = result.Current.Temperature,
                    FeelsLike = result.Current.ApparentTemperature,
                    Humidity = result.Current.RelativeHumidity,
                    WindSpeed = windSpeedMs,
                    Condition = condition,
                    Description = condition,
                    IconCode = iconCode,
                    TimezoneOffset = result.UtcOffsetSeconds
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Open-Meteo fetch error: {ex.Message}");
                return null;
            }
        }

        public static (string Condition, string IconCode) MapWmoCode(int code, bool isDay)
        {
            var daySuffix = isDay ? "d" : "n";

            return code switch
            {
                0 => ("Clear", $"01{daySuffix}"),
                1 => ("Mainly Clear", $"01{daySuffix}"),
                2 => ("Partly Cloudy", $"02{daySuffix}"),
                3 => ("Overcast", $"04{daySuffix}"),
                45 => ("Fog", $"50{daySuffix}"),
                48 => ("Rime Fog", $"50{daySuffix}"),
                51 => ("Light Drizzle", $"09{daySuffix}"),
                53 => ("Moderate Drizzle", $"09{daySuffix}"),
                55 => ("Dense Drizzle", $"09{daySuffix}"),
                56 => ("Freezing Drizzle", $"09{daySuffix}"),
                57 => ("Dense Freezing Drizzle", $"09{daySuffix}"),
                61 => ("Slight Rain", $"10{daySuffix}"),
                63 => ("Moderate Rain", $"10{daySuffix}"),
                65 => ("Heavy Rain", $"10{daySuffix}"),
                66 => ("Light Freezing Rain", $"10{daySuffix}"),
                67 => ("Heavy Freezing Rain", $"10{daySuffix}"),
                71 => ("Slight Snow", $"13{daySuffix}"),
                73 => ("Moderate Snow", $"13{daySuffix}"),
                75 => ("Heavy Snow", $"13{daySuffix}"),
                77 => ("Snow Grains", $"13{daySuffix}"),
                80 => ("Slight Rain Showers", $"09{daySuffix}"),
                81 => ("Moderate Rain Showers", $"09{daySuffix}"),
                82 => ("Violent Rain Showers", $"09{daySuffix}"),
                85 => ("Slight Snow Showers", $"13{daySuffix}"),
                86 => ("Heavy Snow Showers", $"13{daySuffix}"),
                95 => ("Thunderstorm", $"11{daySuffix}"),
                96 => ("Thunderstorm with Slight Hail", $"11{daySuffix}"),
                99 => ("Thunderstorm with Heavy Hail", $"11{daySuffix}"),
                _ => ("Unknown", $"02{daySuffix}")
            };
        }
    }
}
