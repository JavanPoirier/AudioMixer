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
        public string VolumeStep { get; set; }

        [JsonProperty(PropertyName = "staticApplications")]
        public List<AudioSessionSetting> StaticApplications { get; set; }

        [JsonProperty(PropertyName = "blacklistApplications")]
        public List<AudioSessionSetting> BlacklistApplications { get; set; }

        [JsonProperty(PropertyName = "blacklistedApplications")]
        public List<AudioSessionSetting> BlacklistedApplications { get; set; }

        [JsonProperty(PropertyName = "whitelistApplications")]
        public List<AudioSessionSetting> WhitelistApplications { get; set; }

        [JsonProperty(PropertyName = "whitelistedApplications")]
        public List<AudioSessionSetting> WhitelistedApplications { get; set; }

        [JsonProperty(PropertyName = "inlineControlsEnabled")]
        public bool InlineControlsEnabled { get; set; }

        [JsonProperty(PropertyName = "inlineControlsTimeout")]
        public int InlineControlsTimeout { get; set; }
    }
}
