using System.Web.Script.Serialization;

namespace ChalakNotifier
{
    /// <summary>JSON سازگار با .NET 4.0 (بدون پکیج خارجی)</summary>
    internal static class Json
    {
        public static T Parse<T>(string text)
        {
            return new JavaScriptSerializer().Deserialize<T>(text);
        }

        public static string Write(object value)
        {
            return new JavaScriptSerializer().Serialize(value);
        }
    }
}
