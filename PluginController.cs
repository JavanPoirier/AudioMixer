using BarRaider.SdTools;
using Newtonsoft.Json.Linq;
using Sentry;
using streamdeck_client_csharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AudioMixer
{
    public class PluginController : IDisposable
    {
        public string deviceId;
        public AudioManager audioManager;
        public List<ApplicationAction> applicationActions = new List<ApplicationAction>();

        private ApplicationAction selectedAction;
        private GlobalSettings globalSettings;
        private static readonly Lazy<PluginController> instance = new Lazy<PluginController>(() => new PluginController());
        private ConcurrentQueue<ApplicationAction> actionQueue = new ConcurrentQueue<ApplicationAction>();
        private readonly object updateActionsLock = new object();

        public ApplicationAction SelectedAction
        {
            get { return selectedAction; }
            set
            {
                if (value != null && value == selectedAction)
                {
                    selectedAction.SetSelected(false);
                    selectedAction = null;
                }
                else
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
            UpdateActions();
        }

        public void RemoveAction(ApplicationAction action)
        {
            applicationActions.Remove(action);

            if (SelectedAction == action)
            {
                SelectedAction = null;
            }

            UpdateActions();
        }

        public void AddActionToQueue(ApplicationAction action)
        {
            actionQueue.Enqueue(action);

            ApplicationAction enqueuedAction;
            while (actionQueue.TryDequeue(out enqueuedAction)) enqueuedAction.SetAudioSession();
        }

        public void UpdateActions()
        {
            lock (updateActionsLock)
            {
                if (globalSettings != null && globalSettings.InlineControlsEnabled) SelectedAction = null;

                actionQueue = new ConcurrentQueue<ApplicationAction>(applicationActions);

                // No need to reset icon as when the action is set in queue it will be reset if need be.
                applicationActions.ToList().ForEach(action => action.ReleaseAudioSession(false));

                ApplicationAction enqueuedAction;
                while (actionQueue.TryDequeue(out enqueuedAction)) enqueuedAction.SetAudioSession();
            }
        }

        private async void SetActionControls()
        {
            if (selectedAction != null && globalSettings.InlineControlsEnabled)
            {
                List<ApplicationAction> controls = applicationActions.ToList().FindAll(action => action != this.selectedAction);

                if (controls.Count >= 3)
                {
                    await controls[0].SetControlType(Utils.ControlType.Mute);
                    await controls[1].SetControlType(Utils.ControlType.VolumeDown);
                    await controls[2].SetControlType(Utils.ControlType.VolumeUp);
                }
                else
                {
                    string warn = "Not enough plugin actions available to place controls.";
                    Logger.Instance.LogMessage(TracingLevel.WARN, warn);
                    SentrySdk.AddBreadcrumb(
                       message: "warn",
                       category: "PluginController",
                       level: BreadcrumbLevel.Warning
                     );
                }
            }
            else
            {
                // Reset all application actions.
                applicationActions.ToList().ForEach(async pluginAction => await pluginAction.SetControlType(Utils.ControlType.Application));
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

        static public async Task ResetGlobalSettings()
        {
            await GlobalSettingsManager.Instance.SetGlobalSettings(JObject.FromObject(GlobalSettings.CreateDefaultSettings()));
        }
    }
}
