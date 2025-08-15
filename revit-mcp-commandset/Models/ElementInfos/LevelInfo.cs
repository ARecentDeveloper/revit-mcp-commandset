using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.ElementInfos
{
    /// <summary>
    /// Custom class for storing level information
    /// </summary>
    public class LevelInfo
    {
        public int Id { get; set; }
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }
        public double Height { get; set; }
    }
} 