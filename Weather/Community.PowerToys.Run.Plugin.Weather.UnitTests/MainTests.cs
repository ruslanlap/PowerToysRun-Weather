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
        public void MapWmoCode_should_map_rain_and_snow()
        {
            var (rainCondition, rainIcon) = OpenMeteoService.MapWmoCode(61, true);
            var (snowCondition, snowIcon) = OpenMeteoService.MapWmoCode(71, true);

            Assert.AreEqual("010d".Substring(1), rainIcon); // 10d
            Assert.AreEqual("13d", snowIcon);
        }
    }
}
