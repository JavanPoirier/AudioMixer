using AudioMixer.Actions;
using Newtonsoft.Json;
using System.Collections.Generic;
using static AudioMixer.ApplicationAction;

namespace AudioMixer
{
    public class GlobalSettings
    {
        public const string DEFAULT_VOLUME_STEP = "10";
        public const double DEFAULT_INLINE_CONTROLS_HOLD_DURATION = 200;
        public const int DEFAULT_INLINE_CONTROLS_TIMEOUT = 0;

        // NOTE: Some local action settings are stored here for easy access and management. 
        // Having considered putting them in their respective management classes, the benefit of global state change events when editing these may be worth. i.e Another action may benefit from knowing when the other action has changed the setting.
        public GlobalSettings() {
            StaticApplications = new List<AudioSessionSetting>();
            //StaticApplicationsSelector = new List<AudioSessionSetting>();
            BlacklistedApplications = new List<AudioSessionSetting>();
            //BlacklistedApplicationsSelector = new List<AudioSessionSetting>();
            WhitelistedApplications = new List<AudioSessionSetting>();
            //WhitelistedApplicationsSelector = new List<AudioSessionSetting>();
            InlineControlsEnabled = true;
            InlineControlsHoldDuration = DEFAULT_INLINE_CONTROLS_HOLD_DURATION;
            InlineControlsTimeout = DEFAULT_INLINE_CONTROLS_TIMEOUT;

            GlobalVolumeStepLock = true;
            GlobalVolumeStep = DEFAULT_VOLUME_STEP;

            StaticOutputDevices = new List<OutputDeviceSetting>();
            //StaticOutputDevicesSelector = new List<OutputDeviceSetting>();
            BlacklistedOutputDevices = new List<OutputDeviceSetting>();
            //BlacklistedOutputDevicesSelector = new List<OutputDeviceSetting>();
            WhitelistedOutputDevices = new List<OutputDeviceSetting>();
            //WhitelistedOutputDevicesSelector = new List<OutputDeviceSetting>();
        }

        public virtual GlobalSettings CreateInstance(string UUID)
        {
            return new GlobalSettings { UUID = UUID };
        }

        [JsonProperty(PropertyName = "uuid")]
        public string UUID { get; set; }

        // --- Application action

        // NOTE: Not considered global state, however allows for easy checks and management.
        [JsonProperty(PropertyName = "staticApplications")]
        public List<AudioSessionSetting> StaticApplications { get; set; }

        [JsonProperty(PropertyName = "staticApplicationsSelector")]
        public List<AudioSessionSetting> StaticApplicationsSelector { get; set; }

        [JsonProperty(PropertyName = "blacklistedApplications")]
        public List<AudioSessionSetting> BlacklistedApplications { get; set; }

        [JsonProperty(PropertyName = "blacklistedApplicationsSelector")]
        public List<AudioSessionSetting> BlacklistedApplicationsSelector { get; set; }

        [JsonProperty(PropertyName = "whitelistedApplications")]
        public List<AudioSessionSetting> WhitelistedApplications { get; set; }

        [JsonProperty(PropertyName = "whitelistedApplicationsSelector")]
        public List<AudioSessionSetting> WhitelistedApplicationsSelector { get; set; }

        [JsonProperty(PropertyName = "inlineControlsEnabled")]
        public bool InlineControlsEnabled { get; set; } = true;

        [JsonProperty(PropertyName = "inlineControlsHoldDuration")]
        public double InlineControlsHoldDuration { get; set; }

        [JsonProperty(PropertyName = "inlineControlsTimeout")]
        public int InlineControlsTimeout { get; set; }

        // --- Volume action

        [JsonProperty(PropertyName = "globalVolumeStepLock")]
        public bool GlobalVolumeStepLock { get; set; } = true;

        [JsonProperty(PropertyName = "globalVolumeStep")]
        public string GlobalVolumeStep { get; set; }

        // --- Output device action

        // NOTE: Not considered global state, however allows for easy checks and management.
        [JsonProperty(PropertyName = "staticOutputDevices")]
        public List<OutputDeviceSetting> StaticOutputDevices { get; set; }

        [JsonProperty(PropertyName = "staticOutputDevicesSelector")]
        public List<OutputDeviceSetting> StaticOutputDevicesSelector { get; set; }

        [JsonProperty(PropertyName = "blacklistedOutputDevices")]
        public List<OutputDeviceSetting> BlacklistedOutputDevices { get; set; }

        [JsonProperty(PropertyName = "blacklistedOutputDevicesSelector")]
        public List<OutputDeviceSetting> BlacklistedOutputDevicesSelector { get; set; }

        [JsonProperty(PropertyName = "whitelistedOutputDevices")]
        public List<OutputDeviceSetting> WhitelistedOutputDevices { get; set; }

        [JsonProperty(PropertyName = "whitelistedOutputDevicesSelector")]
        public List<OutputDeviceSetting> WhitelistedOutputDevicesSelector { get; set; }


    }
}
