using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Wox.Plugin;
using Community.PowerToys.Run.Plugin.Weather.Services;

namespace Community.PowerToys.Run.Plugin.Weather.UnitTests
{
    [TestClass]
    public class MainTests
    {
        private Main main;

        [TestInitialize]
        public void TestInitialize()
        {
            main = new Main();
        }

        [TestMethod]
        public void Query_should_return_results()
        {
            var results = main.Query(new("search"));

            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0);
            Assert.IsNotNull(results.First());
        }

        [TestMethod]
        public void LoadContextMenus_should_return_results()
        {
            var results = main.LoadContextMenus(new Result { ContextData = "search" });

            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0);
            Assert.IsNotNull(results.First());
        }

        [TestMethod]
        public void MapWmoCode_should_map_clear_sky()
        {
            var (conditionDay, iconDay) = OpenMeteoService.MapWmoCode(0, true);
            var (conditionNight, iconNight) = OpenMeteoService.MapWmoCode(0, false);

            Assert.AreEqual("Clear", conditionDay);
            Assert.AreEqual("01d", iconDay);
            Assert.AreEqual("01n", iconNight);
        }



        [TestMethod]
        public void CreateWeatherResult_should_keep_pretty_unicode_layout()
        {
            var weather = new WeatherData
            {
                Location = "Kyiv",
                Temperature = 21.3f,
                FeelsLike = 19.1f,
                Humidity = 64,
                WindSpeed = 3.5f,
                Condition = "Clear",
                Description = "clear sky",
                IconCode = "01d",
                TimezoneOffset = 7200,
            };

            var method = typeof(Main).GetMethod("CreateWeatherResult", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            var result = (Result)method.Invoke(main, new object[] { weather });

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Title.Contains("Kyiv |"));
            Assert.IsTrue(result.Title.Contains("- Clear"));
            Assert.IsTrue(result.SubTitle.Contains("┌─🌡"));
            Assert.IsTrue(result.SubTitle.Contains("├─ 💧"));
            Assert.IsTrue(result.SubTitle.Contains("├─ 🌬"));
            Assert.IsTrue(result.SubTitle.Contains("└─ 🕒"));
        }

        [TestMethod]
        public void CreateWeatherResult_should_keep_feels_like_emoji()
        {
            var weather = new WeatherData
            {
                Location = "Kyiv",
                Temperature = -22.0f,
                FeelsLike = -25.0f,
                Humidity = 70,
                WindSpeed = 5.0f,
                Condition = "Snow",
                Description = "snow",
                IconCode = "13d",
                TimezoneOffset = 7200,
            };

            var method = typeof(Main).GetMethod("CreateWeatherResult", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            var result = (Result)method.Invoke(main, new object[] { weather });

            Assert.IsNotNull(result);
            Assert.IsTrue(result.SubTitle.Contains("🥶❄️"));
        }

        [TestMethod]
        public void MapWmoCode_should_map_rain_and_snow()
        {
            var (rainCondition, rainIcon) = OpenMeteoService.MapWmoCode(61, true);
            var (snowCondition, snowIcon) = OpenMeteoService.MapWmoCode(71, true);

            Assert.AreEqual("010d".Substring(1), rainIcon); // 10d
            Assert.AreEqual("13d", snowIcon);
        }
    }
}
