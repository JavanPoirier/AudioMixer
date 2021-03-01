using BarRaider.SdTools;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AudioMixer
{
    public sealed class PluginController 
    {
        private static readonly PluginController instance = new PluginController();

        private GlobalSettings globalSettings;
        private List<PluginAction> pluginActions = new List<PluginAction>();
        
        public AudioManager audioManager = AudioManager.Instance;
        public List<string> blacklist = new List<string>();
        public List<string> whitelist = new List<string>();

        static PluginController()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"PluginController");
        }

        private PluginController()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"PluginController: {GetHashCode()}");
            GlobalSettingsManager.Instance.OnReceivedGlobalSettings += GlobalSettingsReceived;
            GlobalSettingsManager.Instance.RequestGlobalSettings();
        }

        public static PluginController Instance
        {
            get
            {
                return instance;
            }
        }

        private void GlobalSettingsReceived(object sender, ReceivedGlobalSettingsPayload globalSettingsPayload)
        {
            // Global Settings exist
            if (globalSettingsPayload?.Settings != null && globalSettingsPayload.Settings.Count > 0)
            {
                globalSettings = globalSettingsPayload.Settings.ToObject<GlobalSettings>();

                // global now has all the settings
                // Console.Writeline(global.MyFirstField);

            }
            else // Global settings do not exist, create new one and SAVE it
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"No global settings found, creating new object");
                globalSettings = new GlobalSettings();
                SetGlobalSettings();
            }
        }
        private void SetGlobalSettings()
        {
            pluginActions.FirstOrDefault().connection.SetGlobalSettingsAsync(JObject.FromObject(globalSettings));
        }

        public int AddAction(PluginAction pluginAction)
        {
            // Find where the key should live.
            var newPosValue = pluginActions.Count();
            var posValue = Int16.Parse(pluginAction.keyCoordinates.Row.ToString() + pluginAction.keyCoordinates.Column.ToString());
            foreach (var _pluginAction in pluginActions.Select((value, index) => new { value, index }))
            {
                var _posValue = Int16.Parse(_pluginAction.value.keyCoordinates.Row.ToString() + _pluginAction.value.keyCoordinates.Column.ToString());
                if (posValue <= _posValue)
                {
                    newPosValue = _pluginAction.index;
                }
            }
            pluginActions.Insert(newPosValue, pluginAction);
            return newPosValue;
        }

        public void RemoveAction(PluginAction pluginAction)
        {
            pluginActions.Remove(pluginAction);
        }

        public void Dispose()
        {
            GlobalSettingsManager.Instance.OnReceivedGlobalSettings -= GlobalSettingsReceived;
        }
    }
}
