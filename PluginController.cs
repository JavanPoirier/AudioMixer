using BarRaider.SdTools;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AudioMixer
{
    public class PluginController 
    {
        private static readonly Lazy<PluginController> instance = new Lazy<PluginController>(() => new PluginController());
        private GlobalSettings globalSettings;

        public AudioManager audioManager;
        public List<PluginAction> pluginActions = new List<PluginAction>();
        public List<string> blacklist = new List<string>();
        public List<string> whitelist = new List<string>();
        private PluginAction selectedAction;

        public PluginAction SelectedAction
        {
            get { return selectedAction; }
            set
            {
                if (value == selectedAction)
                {
                    selectedAction.setSelected(false);
                    selectedAction = null;
                } else
                {
                    // Reset previous selected action
                    if (selectedAction != null) selectedAction.setSelected(false);

                    selectedAction = value;
                    if (selectedAction != null) selectedAction.setSelected(true);
                }
             
                SetActionControls();
            }
        }

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

            audioManager = new AudioManager(this);
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
                    break;
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

        private void SetActionControls()
        {
            if (selectedAction != null)
            {
                List<PluginAction> controls = pluginActions.FindAll((pluginAction) => pluginAction != this.selectedAction);

                if (controls.Count >= 3)
                {
                    controls[0].SetControlType(Utils.ControlType.Mute);
                    controls[1].SetControlType(Utils.ControlType.VolumeDown);
                    controls[2].SetControlType(Utils.ControlType.VolumeUp);
                } else
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, "Not enough plugin actions available to place controls.");
                }
            } else
            {
                // Reset all actions.
                pluginActions.ForEach(pluginAction => pluginAction.SetControlType(Utils.ControlType.Application));
            }
        }

        public void Dispose()
        {
            GlobalSettingsManager.Instance.OnReceivedGlobalSettings -= GlobalSettingsReceived;
        }
    }
}
