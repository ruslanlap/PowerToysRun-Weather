using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Community.PowerToys.Run.Plugin.Weather.Models;

namespace Community.PowerToys.Run.Plugin.Weather.Services
{
    public class GeocodingService
    {
        private readonly HttpClient _httpClient;
        private const string OpenMeteoGeoUrl = "https://geocoding-api.open-meteo.com/v1/search";
        private const string NominatimUrl = "https://nominatim.openstreetmap.org/search";
        private const string PhotonUrl = "https://photon.komoot.io/api/";

        private static readonly Regex UsZipRegex = new Regex(@"^\d{5}(-\d{4})?$", RegexOptions.Compiled);
        private static readonly Regex CanadaPostalRegex = new Regex(@"^[A-Z]\d[A-Z]\s?\d[A-Z]\d$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex UkPostalRegex = new Regex(@"^(([Gg][Ii][Rr]\s?0[Aa]{2})|((([A-Za-z][0-9]{1,2})|(([A-Za-z][A-Ha-hJ-Yj-y][0-9]{1,2})|(([A-Za-z][0-9][A-Za-z])|([A-Za-z][A-Ha-hJ-Yj-y][0-9]?[A-Za-z]))))\s?[0-9][A-Za-z]{2}))$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex InternationalPostalRegex = new Regex(@"^\d{4,6}$", RegexOptions.Compiled);

        public GeocodingService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<GeocodedLocation> ResolveLocationAsync(string query, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                return null;

            var trimmedQuery = query.Trim();

            // 1. If it looks like a postal code, attempt postal code resolution
            if (IsPostalCode(trimmedQuery))
            {
                var postalLocation = await SearchPostalCodeAsync(trimmedQuery, ct);
                if (postalLocation != null)
                    return postalLocation;
            }

            // 2. Try Open-Meteo Geocoding first (fast and keyless)
            var openMeteoResult = await SearchOpenMeteoGeocodingAsync(trimmedQuery, ct);
            if (openMeteoResult != null)
                return openMeteoResult;

            // 3. Fallback: Progressive segment search via Nominatim & Photon
            var fallbackResult = await SearchWithProgressiveFallbackAsync(trimmedQuery, ct);
            if (fallbackResult != null)
                return fallbackResult;

            return null;
        }

        private static bool IsPostalCode(string input)
        {
            return UsZipRegex.IsMatch(input) ||
                   CanadaPostalRegex.IsMatch(input) ||
                   UkPostalRegex.IsMatch(input) ||
                   InternationalPostalRegex.IsMatch(input);
        }

        private async Task<GeocodedLocation> SearchOpenMeteoGeocodingAsync(string query, CancellationToken ct)
        {
            try
            {
                // Open-Meteo geocoding performs best with locality names. If there's a comma (e.g. "Seattle, WA"), extract city
                var cityName = query.Contains(',') ? query.Split(',')[0].Trim() : query;
                var url = $"{OpenMeteoGeoUrl}?name={Uri.EscapeDataString(cityName)}&count=5&language=en&format=json";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("PowerToys-Run-Weather/1.0");

                var response = await _httpClient.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync(ct);
                var result = JsonSerializer.Deserialize<OpenMeteoGeocodingResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Results != null && result.Results.Count > 0)
                {
                    // If user supplied additional comma segments (e.g., country or state), try to find best match
                    var match = SelectBestLocation(query, result.Results);
                    return new GeocodedLocation
                    {
                        Name = match.Name,
                        DisplayName = match.DisplayName,
                        Latitude = match.Latitude,
                        Longitude = match.Longitude,
                        Country = match.Country,
                        Timezone = match.Timezone
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Open-Meteo geocoding error: {ex.Message}");
            }

            return null;
        }

        private static OpenMeteoLocationItem SelectBestLocation(string rawQuery, List<OpenMeteoLocationItem> items)
        {
            if (!rawQuery.Contains(','))
                return items[0];

            var segments = rawQuery.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            if (segments.Count <= 1)
                return items[0];

            var secondary = segments[1];

            var exactMatch = items.FirstOrDefault(i =>
                (!string.IsNullOrEmpty(i.Admin1) && i.Admin1.Equals(secondary, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(i.Country) && i.Country.Equals(secondary, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(i.CountryCode) && i.CountryCode.Equals(secondary, StringComparison.OrdinalIgnoreCase)));

            return exactMatch ?? items[0];
        }

        private async Task<GeocodedLocation> SearchPostalCodeAsync(string postalCode, CancellationToken ct)
        {
            try
            {
                var url = $"{NominatimUrl}?postalcode={Uri.EscapeDataString(postalCode)}&format=json&addressdetails=1&limit=1";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("PowerToys-Run-Weather/1.0 (+https://github.com/BananaOnGitHub/PowerToysRun-Weather)");
                request.Headers.AcceptLanguage.ParseAdd("en");

                var response = await _httpClient.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    var results = JsonSerializer.Deserialize<List<NominatimResponseItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (results != null && results.Count > 0)
                    {
                        var item = results[0];
                        if (double.TryParse(item.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) &&
                            double.TryParse(item.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                        {
                            var placeName = item.Address?.City ?? item.Address?.Town ?? item.Address?.Village ?? item.Name ?? postalCode;
                            return new GeocodedLocation
                            {
                                Name = placeName,
                                DisplayName = $"{placeName} ({postalCode})",
                                Latitude = lat,
                                Longitude = lon,
                                Country = item.Address?.Country
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Nominatim postal lookup error: {ex.Message}");
            }

            return null;
        }

        private async Task<GeocodedLocation> SearchWithProgressiveFallbackAsync(string query, CancellationToken ct)
        {
            var currentQuery = query;

            while (!string.IsNullOrWhiteSpace(currentQuery))
            {
                ct.ThrowIfCancellationRequested();

                // Try Nominatim
                var nominatim = await SearchNominatimAsync(currentQuery, ct);
                if (nominatim != null)
                    return nominatim;

                // Try Photon
                var photon = await SearchPhotonAsync(currentQuery, ct);
                if (photon != null)
                    return photon;

                // Strip trailing comma segment
                var lastComma = currentQuery.LastIndexOf(',');
                if (lastComma <= 0)
                    break;

                currentQuery = currentQuery.Substring(0, lastComma).Trim();
            }

            return null;
        }

        private async Task<GeocodedLocation> SearchNominatimAsync(string query, CancellationToken ct)
        {
            try
            {
                var url = $"{NominatimUrl}?q={Uri.EscapeDataString(query)}&format=json&addressdetails=1&limit=1";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("PowerToys-Run-Weather/1.0 (+https://github.com/BananaOnGitHub/PowerToysRun-Weather)");
                request.Headers.AcceptLanguage.ParseAdd("en");

                var response = await _httpClient.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync(ct);
                var results = JsonSerializer.Deserialize<List<NominatimResponseItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (results != null && results.Count > 0)
                {
                    var item = results[0];
                    if (double.TryParse(item.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) &&
                        double.TryParse(item.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                    {
                        var placeName = item.Address?.City ?? item.Address?.Town ?? item.Address?.Village ?? item.Name ?? query;
                        return new GeocodedLocation
                        {
                            Name = placeName,
                            DisplayName = item.DisplayName ?? placeName,
                            Latitude = lat,
                            Longitude = lon,
                            Country = item.Address?.Country
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Nominatim search error: {ex.Message}");
            }

            return null;
        }

        private async Task<GeocodedLocation> SearchPhotonAsync(string query, CancellationToken ct)
        {
            try
            {
                var url = $"{PhotonUrl}?q={Uri.EscapeDataString(query)}&limit=1";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("PowerToys-Run-Weather/1.0");

                var response = await _httpClient.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync(ct);
                var result = JsonSerializer.Deserialize<PhotonResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Features != null && result.Features.Count > 0)
                {
                    var feat = result.Features[0];
                    var coords = feat.Geometry?.Coordinates;
                    if (coords != null && coords.Length >= 2)
                    {
                        var lon = coords[0];
                        var lat = coords[1];
                        var name = feat.Properties?.Name ?? feat.Properties?.City ?? query;
                        return new GeocodedLocation
                        {
                            Name = name,
                            DisplayName = name,
                            Latitude = lat,
                            Longitude = lon,
                            Country = feat.Properties?.Country
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Photon search error: {ex.Message}");
            }

            return null;
        }
    }
}
