using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AudioMixer.ApplicationAction;

namespace AudioMixer.Actions
{
    public class VolumeSettings : GlobalSettings
    {
        public VolumeSettings() : base()
        {
            LocalVolumeStep = DEFAULT_VOLUME_STEP;
        }

        public override GlobalSettings CreateInstance(string UUID)
        {
            return new VolumeSettings { UUID = UUID };
        }

        [JsonProperty(PropertyName = "localVolumeStep")]
        public string LocalVolumeStep { get; set; }
    }
}
