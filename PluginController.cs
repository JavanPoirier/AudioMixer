using BarRaider.SdTools;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AudioMixer
{
    public class PluginController : IDisposable
    {
        private static readonly Lazy<PluginController> instance = new Lazy<PluginController>(() => new PluginController());

        public AudioManager audioManager;
        public List<ApplicationAction> applicationActions = new List<ApplicationAction>();
        public List<string> blacklist = new List<string>();
        public List<string> whitelist = new List<string>();
        private ApplicationAction selectedAction;
        //private GlobalSettings globalSettings;

        public ApplicationAction SelectedAction
        {
            get { return selectedAction; }
            set
            {
                if (value == selectedAction)
                {
                    selectedAction.SetSelected(false);
                    selectedAction = null;
                } else
                {
                    // Reset previous selected action
                    if (selectedAction != null) selectedAction.SetSelected(false);

                    selectedAction = value;
                    if (selectedAction != null) selectedAction.SetSelected(true);
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
            audioManager = new AudioManager(this);

            GlobalSettingsManager.Instance.OnReceivedGlobalSettings += OnReceivedGlobalSettings;
            GlobalSettingsManager.Instance.RequestGlobalSettings();
        }


        public void Dispose()
        {
            GlobalSettingsManager.Instance.OnReceivedGlobalSettings -= OnReceivedGlobalSettings;
        }

        public void AddAction(ApplicationAction applicationAction)
        {
            applicationActions.Add(applicationAction);

            // If a static application action was added, update all dynamic application actions.
            if (applicationAction.settings.StaticApplication == null)
            {
                UpdateActions();
            }
        }

        public void RemoveAction(ApplicationAction pluginAction)
        {
            applicationActions.Remove(pluginAction);
            UpdateActions();
        }

        public void UpdateActions()
        {
            var dynamicApplicationActions = applicationActions.FindAll(action => action.settings.StaticApplication == null);
            dynamicApplicationActions.ForEach(action => action.SetAudioSession());
        }

        private void SetActionControls()
        {
            if (selectedAction != null)
            {
                List<ApplicationAction> controls = applicationActions.FindAll((pluginAction) => pluginAction != this.selectedAction);

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
                applicationActions.ForEach(pluginAction => pluginAction.SetControlType(Utils.ControlType.Application));
            }
        }

        private void OnReceivedGlobalSettings(object sender, ReceivedGlobalSettingsPayload payload)
        {
            //// Global Settings exist
            //if (payload?.Settings != null && payload.Settings.Count > 0)
            //{
            //    globalSettings = payload.Settings.ToObject<GlobalSettings>();

            //    // global now has all the settings
            //    // Console.Writeline(global.MyFirstField);

            //}
            //else // Global settings do not exist, create new one and SAVE it
            //{
            //    Logger.Instance.LogMessage(TracingLevel.WARN, $"No global settings found, creating new object");
            //    globalSettings = new GlobalSettings();
            //    SetGlobalSettings();
            //}
        }

        //// Saves the global object back the global settings
        //private void SetGlobalSettings()
        //{
        //    Connection.SetGlobalSettingsAsync(JObject.FromObject(global));
        //}
    }
}
