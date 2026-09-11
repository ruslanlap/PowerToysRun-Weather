using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Community.PowerToys.Run.Plugin.Weather.Models
{
    public class OpenMeteoWeatherResponse
    {
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("utc_offset_seconds")]
        public int UtcOffsetSeconds { get; set; }

        [JsonPropertyName("timezone")]
        public string Timezone { get; set; }

        [JsonPropertyName("current")]
        public OpenMeteoCurrent Current { get; set; }
    }

    public class OpenMeteoCurrent
    {
        [JsonPropertyName("time")]
        public string Time { get; set; }

        [JsonPropertyName("temperature_2m")]
        public float Temperature { get; set; }

        [JsonPropertyName("relative_humidity_2m")]
        public int RelativeHumidity { get; set; }

        [JsonPropertyName("apparent_temperature")]
        public float ApparentTemperature { get; set; }

        [JsonPropertyName("weather_code")]
        public int WeatherCode { get; set; }

        [JsonPropertyName("wind_speed_10m")]
        public float WindSpeed { get; set; }

        [JsonPropertyName("wind_direction_10m")]
        public int WindDirection { get; set; }

        [JsonPropertyName("is_day")]
        public int IsDay { get; set; } = 1;
    }

    public class OpenMeteoGeocodingResponse
    {
        [JsonPropertyName("results")]
        public List<OpenMeteoLocationItem> Results { get; set; }
    }

    public class OpenMeteoLocationItem
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("country_code")]
        public string CountryCode { get; set; }

        [JsonPropertyName("admin1")]
        public string Admin1 { get; set; }

        [JsonPropertyName("admin2")]
        public string Admin2 { get; set; }

        [JsonPropertyName("timezone")]
        public string Timezone { get; set; }

        public string DisplayName
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(Name)) parts.Add(Name);
                if (!string.IsNullOrWhiteSpace(Admin1) && !string.Equals(Admin1, Name, StringComparison.OrdinalIgnoreCase)) parts.Add(Admin1);
                if (!string.IsNullOrWhiteSpace(Country)) parts.Add(Country);
                return parts.Count > 0 ? string.Join(", ", parts) : Name;
            }
        }
    }

    public class NominatimResponseItem
    {
        [JsonPropertyName("place_id")]
        public long PlaceId { get; set; }

        [JsonPropertyName("lat")]
        public string Lat { get; set; }

        [JsonPropertyName("lon")]
        public string Lon { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("address")]
        public NominatimAddress Address { get; set; }
    }

    public class NominatimAddress
    {
        [JsonPropertyName("city")]
        public string City { get; set; }

        [JsonPropertyName("town")]
        public string Town { get; set; }

        [JsonPropertyName("village")]
        public string Village { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("country_code")]
        public string CountryCode { get; set; }
    }

    public class PhotonResponse
    {
        [JsonPropertyName("features")]
        public List<PhotonFeature> Features { get; set; }
    }

    public class PhotonFeature
    {
        [JsonPropertyName("geometry")]
        public PhotonGeometry Geometry { get; set; }

        [JsonPropertyName("properties")]
        public PhotonProperties Properties { get; set; }
    }

    public class PhotonGeometry
    {
        [JsonPropertyName("coordinates")]
        public double[] Coordinates { get; set; }
    }

    public class PhotonProperties
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("city")]
        public string City { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("countrycode")]
        public string CountryCode { get; set; }

        [JsonPropertyName("osm_id")]
        public long OsmId { get; set; }
    }

    public class GeocodedLocation
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Country { get; set; }
        public string Timezone { get; set; }
    }
}
