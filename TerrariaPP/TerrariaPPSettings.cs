using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TerrariaPP.Utils;

namespace TerrariaPP
{
    public class TerrariaPPSettings
    {
        [JsonProperty("log_level")]
        [JsonConverter(typeof(StringEnumConverter))]
        public LogLevel LogLevel = LogLevel.INFO;

        [JsonProperty("timeout")]
        public int TimeOut = 1000;
    }
}
