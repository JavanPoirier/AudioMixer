using BarRaider.SdTools;
using MoreLinq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AudioMixer
{
    public class PluginController : IDisposable
    {
        public AudioManager audioManager;
        public List<ApplicationAction> applicationActions = new List<ApplicationAction>();

        private ApplicationAction selectedAction;
        private GlobalSettings globalSettings;
        private static readonly Lazy<PluginController> instance = new Lazy<PluginController>(() => new PluginController());

        // We can only assign one action at a time.
        private bool isSettingActions = false;
        private List<ApplicationAction> actionQueue = new List<ApplicationAction>();

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

        public void AddAction(ApplicationAction action)
        {
            applicationActions.Add(action);
            AddActionToQueue(action);
            UpdateActions();
        }

        public void RemoveAction(ApplicationAction action)
        {
            applicationActions.Remove(action);
            UpdateActions();
        }

        public async void AddActionToQueue(ApplicationAction action)
        {
            actionQueue.Add(action);
            if (!isSettingActions)
            {
                isSettingActions = true;
                while (actionQueue.Count > 0)
                {
                    try
                    {
                        await actionQueue.First().SetAudioSession();
                        actionQueue.RemoveAt(0);
                    } catch { }
                }
                isSettingActions = false;
            }
        }

        public void UpdateActions()
        {
            applicationActions.ForEach(action =>
            {
                action.ReleaseAudioSession();
            });

            applicationActions.ForEach(action =>
            {
                AddActionToQueue(action);
            });
        }

        private void SetActionControls()
        {
            if (selectedAction != null && globalSettings.InlineControlsEnabled)
            {
                List<ApplicationAction> controls = applicationActions.FindAll(action => action != this.selectedAction);

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
                // Reset all application actions.
                applicationActions.ForEach(pluginAction => pluginAction.SetControlType(Utils.ControlType.Application));
                //UpdateActions();
            }
        }

        private void OnReceivedGlobalSettings(object sender, ReceivedGlobalSettingsPayload payload)
        {
            if (payload?.Settings != null && payload.Settings.Count > 0)
            {
                globalSettings = payload.Settings.ToObject<GlobalSettings>();
            }
        }
    }
}
