
using System.Text.Json.Serialization;

namespace Shared
{
    [JsonSerializable(typeof(AppSettings))]
    [JsonSerializable(typeof(List<Browser>))]
    [JsonSerializable(typeof(Browser))]
    [JsonSerializable(typeof(List<BrowserProfile>))]
    [JsonSerializable(typeof(BrowserProfile))]
    public partial class AppJsonSerializerContext : JsonSerializerContext
    {
    }
}