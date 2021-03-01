using BarRaider.SdTools;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioMixer
{
    public class PluginSettings
    {
        public static PluginSettings CreateDefaultSettings()
        {
            PluginSettings instance = new PluginSettings();
            // instance.OutputFileName = String.Empty;
            // instance.InputString = String.Empty;
            return instance;
        }

        //[FilenameProperty]
        //[JsonProperty(PropertyName = "outputFileName")]
        //public string OutputFileName { get; set; }

        //[JsonProperty(PropertyName = "inputString")]
        //public string InputString { get; set; }
    }
}
