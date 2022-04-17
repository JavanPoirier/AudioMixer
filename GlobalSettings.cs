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
        [JsonProperty(PropertyName = "volumeStep")]
        public float VolumeStep { get; set; } = 10F;

        [JsonProperty(PropertyName = "blacklist")]
        public string[] Blacklist { get; set; }

        [JsonProperty(PropertyName = "whitelist")]
        public string[] Whitelist { get; set; }
    }
}
