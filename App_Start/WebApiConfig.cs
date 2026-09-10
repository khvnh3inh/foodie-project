using System.Web.Http;
using System.Web.Http.Cors;
using FoodieProject.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FoodieProject.App_Start
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // ── CORS (cho phép frontend gọi API từ domain khác nếu cần) ──
            var cors = new EnableCorsAttribute("*", "*", "*");
            config.EnableCors(cors);

            // ── JWT Authentication Handler ──
            config.MessageHandlers.Add(new JwtAuthHandler());

            // ── Attribute Routing (ưu tiên) ──
            config.MapHttpAttributeRoutes();

            // ── Convention-based Route (fallback) ──
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            // ── JSON Settings ──
            config.Formatters.Remove(config.Formatters.XmlFormatter);

            var json = config.Formatters.JsonFormatter;
            json.SerializerSettings.Formatting = Formatting.Indented;
            json.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            json.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
            json.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
        }
    }
}
