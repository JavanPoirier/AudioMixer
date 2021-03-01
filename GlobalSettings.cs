using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioMixer
{
    class GlobalSettings
    {
        [JsonProperty(PropertyName = "myFirstField")]
        public String MyFirstField { get; set; }

        [JsonProperty(PropertyName = "mySecondFile")]
        public bool MySecondField { get; set; }

        [JsonProperty(PropertyName = "keys")]
        public string[] Keys { get; set; }
    }
}
