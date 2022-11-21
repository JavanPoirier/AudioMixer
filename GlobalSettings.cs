using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioMixer
{
    public class GlobalSettings
    {
        public const string VOLUME_STEP = "10";
        public const int INLINE_CONTROLS_TIMEOUT = 0;
        public static GlobalSettings CreateDefaultSettings()
        {
            GlobalSettings instance = new GlobalSettings
            {
                VolumeStep = VOLUME_STEP,
                StaticApplications = new List<AudioSessionSetting>(),
                BlacklistApplications = new List<AudioSessionSetting>(),
                BlacklistedApplications = new List<AudioSessionSetting>(),
                WhitelistApplications = new List<AudioSessionSetting>(),
                WhitelistedApplications = new List<AudioSessionSetting>(),
                InlineControlsEnabled = true,
                InlineControlsTimeout = INLINE_CONTROLS_TIMEOUT,
            };
            return instance;
        }

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
