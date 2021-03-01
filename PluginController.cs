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
    public class PluginController 
    {
        private static readonly Lazy<PluginController> instance = new Lazy<PluginController>(() => new PluginController());
        private GlobalSettings globalSettings;

        public List<PluginAction> pluginActions = new List<PluginAction>();
        public AudioManager audioManager = AudioManager.Instance;
        public List<string> blacklist = new List<string>();
        public List<string> whitelist = new List<string>();

        public static PluginController Instance
        {
            get
            {
                return instance.Value;
            }
        }

        private PluginController()
        {
            Logger.Instance.LogMessage(TracingLevel.DEBUG, $"PluginController: {GetHashCode()}");
            GlobalSettingsManager.Instance.OnReceivedGlobalSettings += GlobalSettingsReceived;
            GlobalSettingsManager.Instance.RequestGlobalSettings();
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

        public void AddAction(PluginAction pluginAction)
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
            this.UpdateActions();
        }

        public void RemoveAction(PluginAction pluginAction)
        {
            pluginActions.Remove(pluginAction);
            this.UpdateActions();
        }

        public void UpdateActions()
        {
            pluginActions.ForEach((pluginAction) =>
            {
                pluginAction.UpdateKey();
            });
        }

        public void Dispose()
        {
            GlobalSettingsManager.Instance.OnReceivedGlobalSettings -= GlobalSettingsReceived;
        }
    }
}
