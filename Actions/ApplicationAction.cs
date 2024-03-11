using BarRaider.SdTools;
using Newtonsoft.Json.Linq;
using System;
using System.Drawing;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using MoreLinq.Extensions;
using Sentry;
using AudioMixer.Actions;
using System.Timers;
using static AudioMixer.ApplicationAction;
using System.Diagnostics;
using AudioSwitcher.AudioApi.Session;
using System.Runtime;

namespace AudioMixer
{
    [PluginActionId("com.javanpoirier.audiomixer.application")]
    public class ApplicationAction : BaseAction<ApplicationSettings>
    {
        public class ApplicationSettings : GlobalSettings
        {
            public ApplicationSettings() : base()
            {
                StaticApplication = null;
                StaticApplicationName = null;
                BlacklistedApplicationName = null;
                WhitelistedApplicationName = null;
            }

            public override GlobalSettings CreateInstance(string UUID)
            {
                return new ApplicationSettings { UUID = UUID };
            }

            [JsonProperty(PropertyName = "staticApplication")]
            public AudioSessionSetting StaticApplication { get; set; }

            [JsonProperty(PropertyName = "staticApplicationName")]
            public string StaticApplicationName { get; set; }

            [JsonProperty(PropertyName = "blacklistedApplicationName")]
            public string BlacklistedApplicationName { get; set; }

            [JsonProperty(PropertyName = "whitelistedApplicationName")]
            public string WhitelistedApplicationName { get; set; }
        }

        private Utils.ControlType controlType = Utils.ControlType.Application;
        private Image iconImage;
        private Image volumeImage;
        public string processName;

        public IEnumerable<AudioSession> ActionAudioSessions
        {
            get => AudioManager.audioSessions.Where(session => session.processName == processName);
        }

        public float MasterVolume {
            get => ActionAudioSessions.Max(session => session.MasterVolume);
            private set => ActionAudioSessions.ForEach(session => session.MasterVolume = value);
        }
        public bool Mute
        {
            get => ActionAudioSessions.Any(session => session.Mute == true);
            private set => ActionAudioSessions.ForEach(session => session.Mute = value);
        }
     

        public ApplicationAction(SDConnection connection, InitialPayload payload) : base(connection, payload, ActionType.APPLICATION, "Application") { }

        protected override void InitActionCore() {  }

        protected override void SetKey()
        {
            // If not currently in the actions list, add it.
            if (!ApplicationActions.applicationActions.Any(action => action == this))
            {
                ApplicationActions.Add(this);
            }
        }

        protected override void HandleKeyHeld(object timerSender, ElapsedEventArgs elapsedEvent)
        {
            if (controlType == Utils.ControlType.VolumeUp || controlType == Utils.ControlType.VolumeDown)
            {
                SetSelectedActionVolume();
            }
        }

        protected override void HandleKeyReleased(object sender, KeyReleasedEventArgs e)
        {
            if (controlType == Utils.ControlType.Application)
            {
                if (processName == null) return;

                if (stopWatch.ElapsedMilliseconds >= 2000) ToggleBlacklistApp(processName);
                else if (ActionAudioSessions.Count() > 0) ApplicationActions.SelectedAction = this;
            }
            else
            {
                SetSelectedActionVolume();
            }
        }

        // User has deleted the action from the device.
        public override void Dispose()
        {
            base.Dispose();

            // Required before releasing...
            ToggleStaticApp(null);

            ApplicationActions.Remove(this);
        }

        // NOTE:
        // This does not appear to work as expected when switching pages on the Stream Deck.
        // Connection.SetDefaultImageAsync();
        //
        // However this does...
        // Connection.SetImageAsync((string)null, 0, true);
        public void SetAudioSession(bool releaseSessions = true)
        {
            SentrySdk.AddBreadcrumb(
                message: "Setting audio session",
                category: actionName,
                level: BreadcrumbLevel.Info
            );

            // Previous audio session cleanup.
            if (releaseSessions) ReleaseAudioSessions();

            // If audio session is static...
            if (actionSettings.StaticApplication != null)
            {
                // Before self assigning, find the application action that has the session.
                var existingApplicationAction = ApplicationActions.applicationActions.Find(action =>
                   action != this && action.processName == actionSettings.StaticApplication.processName
                );

                // In case of duplicates, clear this one.
                var existingStaticApplication = ApplicationActions.applicationActions.Find(action => action.actionSettings.StaticApplication?.processName == actionSettings.StaticApplication.processName && action != this);
                if (existingStaticApplication != null)
                {
                    ToggleStaticApp(null);
                    return;
                }

                // Self assign before re-assigning the last action.
                this.processName = actionSettings.StaticApplication.processName;

                // If an application action that is not this one has the session we want, clear it.
                if (existingApplicationAction != null && existingApplicationAction != this) ApplicationActions.AddToQueue(existingApplicationAction);

                SentrySdk.AddBreadcrumb(
                    message: "Reserved static audio session",
                    category: actionName,
                    level: BreadcrumbLevel.Info,
                    data: new Dictionary<string, string> {
                        { "processName", $"{processName}" },
                        { "applicationActions", $"{ApplicationActions.applicationActions.Count()}" },
                        { "audioSessionCount", $"{ActionAudioSessions.Count()}" }
                    }
                 );
            }
            else
            {
                // Get the next unassigned audio session. Assign it.
                var audioSession = AudioManager.audioSessions.Find(session =>
                {
                    // Ensure it is not a blacklisted application.
                    var blacklistedApplication = actionSettings.BlacklistedApplications.Find(application => application.processName == session.processName);
                    if (blacklistedApplication != null) return false;

                    // Ensure no application action has the application set, both statically and dynamically.
                    var existingApplicationAction = ApplicationActions.applicationActions.Find(action =>
                        session.processName == action.actionSettings.StaticApplication?.processName || action.processName == session.processName
                    );
                    if (existingApplicationAction != null) return false;

                    return true;
                });

                if (audioSession != null) this.processName = audioSession.processName;

                SentrySdk.AddBreadcrumb(
                    message: "Reserved audio session",
                    category: actionName,
                    level: BreadcrumbLevel.Info,
                    data: new Dictionary<string, string> {
                        { "processName", $"{processName}" },
                        { "applicationActions", $"{ApplicationActions.applicationActions.Count()}" },
                        { "audioSessionCount", $"{ActionAudioSessions.Count()}" }
                    }
                );
            }

            // Do NOT add 0 check condition to this outer if statement as it is required for the nested if of StaticApplication.
            if (!ActionAudioSessions.Any())
            {
                // If application action is static and audio session is not available, use gray-scaled last known icon.
                if (actionSettings.StaticApplication != null)
                {
                    var lastKnownIcon = Utils.CreateIconImage(Utils.Base64ToBitmap(actionSettings.StaticApplication.processIcon));

                    if (controlType == Utils.ControlType.Application)
                    {
                        Connection.SetImageAsync(Utils.CreateAppKey(lastKnownIcon, volumeImage, false, false, false), null, true);
                    }

                    SentrySdk.AddBreadcrumb(
                        message: "Set unavailable static session",
                        category: actionName,
                        level: BreadcrumbLevel.Info,
                        data: new Dictionary<string, string> {
                            { "processName", $"{processName}" },
                            { "applicationActions", $"{ApplicationActions.applicationActions.Count()}" }
                        }
                    );
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, "No sessions available.");
                    SentrySdk.AddBreadcrumb(
                        message: "No sessions available",
                        category: actionName,
                        level: BreadcrumbLevel.Info
                    );

                    if (controlType == Utils.ControlType.Application)
                    {
                        Connection.SetImageAsync((string)null, null, true);
                    }
                }
            }
            else
            {
                try
                {
                    // NOTE: All audio sessions in one process are treated as one. If one changes volume so does the other.
                    // The reasoning for this comes down to possible unwanted multiple process icons being shown, and with no way
                    // of discriminating them I felt this was the best UX. Ex: Discord opens 2 audio sessions, 1 for comms, and the other for notifications.
                    ActionAudioSessions.ForEach(session => session.OnSessionDisconnected += HandleSessionDisconnected);
                    ActionAudioSessions.ForEach(session => session.OnVolumeChanged += HandleVolumeChanged);

                    // Update sessions to ensure a consistent volume setting. This in-turn will call to set the image.
                    ActionAudioSessions.ForEach(session =>
                    {
                        // Only change them if not already the to be value to prevent recursion. 
                        if (session.MasterVolume != MasterVolume) session.MasterVolume = MasterVolume;
                        if (session.Mute != Mute) session.Mute = Mute;
                    });

                    SetImage();

                    SentrySdk.AddBreadcrumb(
                        message: "Set audio sessions",
                        category: actionName,
                        level: BreadcrumbLevel.Info,
                        data: new Dictionary<string, string> {
                            { "processName", $"{processName}" },
                            { "applicationActions", $"{ApplicationActions.applicationActions.Count()}" },
                            { "audioSessionCount", $"{ActionAudioSessions.Count()}" }
                        }
                    );
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, ex.Message);
                    SentrySdk.CaptureException(ex, scope => { scope.TransactionName = actionName; });

                    ApplicationActions.Reload();
                }
            }
        }

        public void SetImage()
        {
            if (controlType == Utils.ControlType.Application)
            {
                // Assigning all sessions the highest volume value found by triggering a volume change.
                iconImage = Utils.CreateIconImage(ActionAudioSessions.First().processIcon);
                volumeImage = Utils.CreateTextImage(Utils.FormatVolume(MasterVolume));
                bool isSelected = ApplicationActions.SelectedAction == this;
                Connection.SetImageAsync(Utils.CreateAppKey(iconImage, volumeImage, isSelected, Mute), null, true);
            }
        }

        public void ReleaseAudioSession(AudioSession session)
        {
            SentrySdk.AddBreadcrumb(
                  message: "Releasing audio session",
                  category: actionName,
                  level: BreadcrumbLevel.Info,
                  data: new Dictionary<string, string> {
                      { "processName", $"{processName}" },
                  }
               );

            session.OnSessionDisconnected -= HandleSessionDisconnected;
            session.OnVolumeChanged -= HandleVolumeChanged;
        }

        public void ReleaseAudioSessions()
        {
            try
            {
                SentrySdk.AddBreadcrumb(
                   message: "Releasing audio sessions",
                   category: actionName,
                   level: BreadcrumbLevel.Info,
                   data: new Dictionary<string, string> {
                        { "processName", $"{processName}" },
                        { "sessionCount", $"{ActionAudioSessions.Count()}" }
                   }
                );

                // Iterate through all audio sessions as by this time it could already been removed.
                ActionAudioSessions.ForEach(ReleaseAudioSession);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, ex.Message);
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = actionName; });
            }
            finally
            {
                this.processName = null;
                // Clear the selected action
                // ApplicationActions.SelectedAction = null;
            }
        }

        void HandleSessionDisconnected(object sender, EventArgs e)
        {
            SentrySdk.AddBreadcrumb(
            message: "Session disconnected",
            category: actionName,
            level: BreadcrumbLevel.Info,
            data: new Dictionary<string, string> {
                    { "processName", $"{processName}" },
                    { "controlType", $"{controlType}" },
                    { "mute", $"{Mute}" },
                    { "masterVolume", $"{MasterVolume}" },
                    { "applicationActions", $"{ApplicationActions.applicationActions.Count()}" }
                }
            );

            if (ActionAudioSessions.Count() < 2) ApplicationActions.AddToQueue(this);
        }

        void HandleVolumeChanged(object sender, AudioSession.VolumeChangedEventArgs e)
        {
            SentrySdk.AddBreadcrumb(
                message: "Volume changed",
                category: actionName,
                level: BreadcrumbLevel.Info,
                data: new Dictionary<string, string> {
                    { "processName", $"{processName}" },
                    { "controlType", $"{controlType}" },
                    { "mute", $"{Mute}" },
                    { "masterVolume", $"{MasterVolume}" },
                    { "applicationActions", $"{ApplicationActions.applicationActions.Count()}" }
                }
            );

            // NOTE: Do not use event arguments as they are the value prior to the change. Also volume and mute are set independently causing two events to get fired.
            // Find the updated session and update all to ensure a consistent volume setting. This in-turn will call to set the image.
            var updatedSession = ActionAudioSessions.FirstOrDefault(session => session.Equals(sender));
            if (updatedSession == null) return;

            var newMasterVolume = updatedSession.MasterVolume;
            var newMute = updatedSession.Mute;
            ActionAudioSessions.ForEach(session =>
            {
                // Only change them if not already the to be value to prevent recursion. 
                if (session.MasterVolume != newMasterVolume) session.MasterVolume = newMasterVolume;
                if (session.Mute != newMute) session.Mute = newMute;
            });

            // Only update the key if its being displayed as an application.
            if (controlType == Utils.ControlType.Application)
            {
                bool selected = ApplicationActions.SelectedAction == this;
                volumeImage = Utils.CreateTextImage(Utils.FormatVolume(MasterVolume));
                Connection.SetImageAsync(Utils.CreateAppKey(iconImage, volumeImage, selected, Mute), null, true);
            }
        }

        public override void KeyPressed(KeyPayload payload)
        {
            SentrySdk.AddBreadcrumb(
               message: "Key pressed",
               category: actionName,
               level: BreadcrumbLevel.Info,
               data: new Dictionary<string, string> {
                    { "processName", $"{processName}" },
                    { "controlType", $"{controlType}" },
                    { "mute", $"{Mute}" },
                    { "masterVolume", $"{MasterVolume}" },
                    { "applicationActions", $"{ApplicationActions.applicationActions.Count()}" }
               }
           );

            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");

            stopWatch.Restart();
            timer.Start();
        }

        private void SetSelectedActionVolume()
        {
            try
            {
                if (ApplicationActions.SelectedAction == null || !ApplicationActions.SelectedAction.ActionAudioSessions.Any())
                {
                    SentrySdk.AddBreadcrumb(
                        message: "No selected action, or missing audio sessions",
                        category: actionName,
                        level: BreadcrumbLevel.Info,
                        data: new Dictionary<string, string> {
                            { "selectedAction", $"{ApplicationActions.SelectedAction.processName}" },
                            { "selectedActionAudioSessions", $"{ApplicationActions.SelectedAction.ActionAudioSessions.Count()}" }
                        }
                    );
                    return;
                }

                float newVolume = 1F;
                float volumeStep = (float)Int32.Parse(pluginController.globalSettings.GlobalVolumeStep) / 100;
                switch (controlType)
                {
                    case Utils.ControlType.VolumeMute:
                        ApplicationActions.SelectedAction.Mute = !ApplicationActions.SelectedAction.Mute;
                        break;
                    case Utils.ControlType.VolumeDown:
                        if (ApplicationActions.SelectedAction.Mute) ApplicationActions.SelectedAction.Mute = !ApplicationActions.SelectedAction.Mute;
                        else
                        {
                            newVolume = ApplicationActions.SelectedAction.MasterVolume - (volumeStep);
                            ApplicationActions.SelectedAction.MasterVolume = newVolume < 0F ? 0F : newVolume;
                        }
                        break;
                    case Utils.ControlType.VolumeUp:
                        if (ApplicationActions.SelectedAction.Mute) ApplicationActions.SelectedAction.Mute = !ApplicationActions.SelectedAction.Mute;
                        else
                        {
                            newVolume = ApplicationActions.SelectedAction.MasterVolume + volumeStep;
                            ApplicationActions.SelectedAction.MasterVolume = newVolume > 1F ? 1F : newVolume;
                        }
                        break;
                }

                // Assign to all sessions
                /*  ApplicationActions.SelectedAction?.ActionAudioSessions.ToList().ForEach(session =>
                {
                    session.MasterVolume = MasterVolume;
                    session.Mute = Mute;
                });*/
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, ex.ToString());
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = actionName; });
            }
        }

        public void SetSelected(Boolean selected)
        {
            if (ActionAudioSessions.Count() > 0)
            {
                Connection.SetImageAsync(Utils.CreateAppKey(iconImage, volumeImage, selected, Mute), null, true);
            }
            else
            {
                if (actionSettings?.StaticApplication != null)
                {
                    var lastKnownIcon = Utils.CreateIconImage(Utils.Base64ToBitmap(actionSettings.StaticApplication.processIcon));
                    Connection.SetImageAsync(Utils.CreateAppKey(lastKnownIcon, volumeImage, false, false, false), null, true);
                }
            }
        }

        public void SetControlType(Utils.ControlType controlType)
        {
            this.controlType = controlType;
            switch (controlType)
            {
                case Utils.ControlType.VolumeMute:
                    _ = Connection.SetImageAsync(Utils.CreateVolumeMuteKey(), null, true);
                    break;
                case Utils.ControlType.VolumeDown:
                    _ = Connection.SetImageAsync(Utils.CreateVolumeDownKey((float)Int32.Parse(actionSettings.GlobalVolumeStep)), null, true);
                    break;
                case Utils.ControlType.VolumeUp:
                    _ = Connection.SetImageAsync(Utils.CreateVolumeUpKey((float)Int32.Parse(actionSettings.GlobalVolumeStep)), null, true);
                    break;
                default:
                    ApplicationActions.AddToQueue(this);
                    break;
            }
        }

        private async void ToggleBlacklistApp(string processName)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{processName} added to blacklist");
            SentrySdk.AddBreadcrumb(
                message: "Add to blacklist",
                category: actionName,
                level: BreadcrumbLevel.Info,
                data: new Dictionary<string, string> {
                    { "processName", $"{processName}" },
                    { "applicationActions", $"{ApplicationActions.applicationActions.Count()}" }
                }
            );

            var staticApp = pluginController.globalSettings.StaticApplications.Find(session => session.processName == processName);
            if (staticApp != null) return;

            var existingBlacklistedApp = actionSettings.BlacklistedApplications.Find(session => session.processName == processName);
            if (existingBlacklistedApp != null)
            {
                actionSettings.BlacklistedApplications.Remove(existingBlacklistedApp);
                actionSettings.BlacklistedApplicationName = null;
            }
            else
            {
                AudioSession audioSession = AudioManager.audioSessions.Find(session => session.processName == processName);

                if (audioSession != null)
                {
                    actionSettings.BlacklistedApplications.Add(new AudioSessionSetting(audioSession));
                    actionSettings.BlacklistedApplicationName = null;
                }
            }

            // Save only global, as it will update the local settings.
            await SaveGlobalSettings();
        }

        // TODO: Anytime the global settings update we want to refresh, however we don't want this to make recursive calls.
        public async Task RefreshApplications()
        {
            try
            {
                SentrySdk.AddBreadcrumb(
                    message: "Refresh applications",
                    category: actionName,
                    level: BreadcrumbLevel.Info
                );

                var applications = AudioManager.audioSessions.ConvertAll(session => new AudioSessionSetting(session));

                // Remove duplicate process'
                var distinctApplications = applications.DistinctBy(app => app.processName).ToList();

                /**
                * Static
                **/
                actionSettings.StaticApplicationsSelector = new List<AudioSessionSetting>(distinctApplications);

                // TODO: Add additional logic removing impossible combinations. Handle if one was already set.
                actionSettings.StaticApplicationsSelector.RemoveAll(app => pluginController.globalSettings.StaticApplications.Find(_app => _app.processName == app.processName) != null);
                actionSettings.StaticApplicationsSelector.RemoveAll(app => pluginController.globalSettings.BlacklistedApplications.Find(_app => _app.processName == app.processName) != null);

                // If this is a static process which does not have an active audio session, add it to the selector.
                if (actionSettings.StaticApplication != null)
                {
                    var staticApplication = actionSettings.StaticApplicationsSelector.Find(app => app.processName == actionSettings.StaticApplication.processName);
                    if (staticApplication == null)
                    {
                        actionSettings.StaticApplicationsSelector.Add(actionSettings.StaticApplication);
                    }
                }

                /**
                 * Blacklist
                 * 
                 * NOTES: 
                 * - The blacklist selector should also include blacklisted apps
                 **/
                actionSettings.BlacklistedApplicationsSelector = new List<AudioSessionSetting>(distinctApplications).Concat(pluginController.globalSettings.BlacklistedApplications).DistinctBy(app => app.processName).ToList();
                actionSettings.BlacklistedApplicationsSelector.RemoveAll(app => pluginController.globalSettings.StaticApplications.Find(_app => _app.processName == app.processName) != null);
                actionSettings.BlacklistedApplicationsSelector.RemoveAll(app => pluginController.globalSettings.WhitelistedApplications.Find(_app => _app.processName == app.processName) != null);


                /**
                 * Whitelist
                 **/
                actionSettings.WhitelistedApplicationsSelector = new List<AudioSessionSetting>(distinctApplications);
                actionSettings.WhitelistedApplicationsSelector.RemoveAll(app => actionSettings.BlacklistedApplications.Find(_app => _app.processName == app.processName) != null);

                await SaveSettings();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, ex.ToString());
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = actionName; });
            }
        }

        // TODO: Handle empty processName or omit them as sessions.
        private async void ToggleStaticApp(string processName)
        {
            if (string.IsNullOrEmpty(processName))
            {
                if (actionSettings.StaticApplication == null) return;
                
                pluginController.globalSettings.StaticApplications.Remove((actionSettings.StaticApplication));

                actionSettings.StaticApplication = null;
                actionSettings.StaticApplicationName = null;
            }
            else
            {
                // Check for an existing static application. If one exist replace it with this one.
               /* var existingStaticApplication = ApplicationActions.applicationActions.Find(action => action.actionSettings.StaticApplication.processName == processName && action != this);
                if (existingStaticApplication != null)
                {
                    existingStaticApplication.ToggleStaticApp(null);
                }*/

                // If the current action is already a static application, clear it.
                if (actionSettings.StaticApplication != null)
                {
                    var staticApplication = pluginController.globalSettings.StaticApplications.Find(session => session.processName == actionSettings.StaticApplication.processName);
                    pluginController.globalSettings.StaticApplications.Remove(staticApplication);
                }

                AudioSession audioSession = AudioManager.audioSessions.Find(session => session.processName == processName);
                if (audioSession == null) return;

                // Ensure it is not a static application.
                if (pluginController.globalSettings.StaticApplications.Find(session => session.processName == processName) != null) return;
                // Ensure it is not in the blacklist.
                if (pluginController.globalSettings.BlacklistedApplications.Find(app => app.processName == processName) != null) return;

                actionSettings.StaticApplication = new AudioSessionSetting(audioSession);
                actionSettings.StaticApplicationName = actionSettings.StaticApplication.processName;

                // Add it to the global settings list. 
                pluginController.globalSettings.StaticApplications.Add(actionSettings.StaticApplication);
            }

            // Save only global, as it will update the local settings.
            SaveGlobalSettings();
        }

        public override async Task SaveGlobalSettings(bool triggerDidReceiveGlobalSettings = true)
        {
            await base.SaveGlobalSettings();

            ApplicationActions.Reload();
        }

        public override async void OnSendToPlugin(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;
            if (payload["action"] == null || payload["action"].ToString() != "application") return;

            Logger.Instance.LogMessage(TracingLevel.INFO, JObject.FromObject(new Dictionary<string, string> {
               { "processName", processName },
               { "controlType", $"{controlType}" },
               { "mute", $"{Mute}" },
               { "masterVolume", $"{MasterVolume}" },
               { "applicationActions", $"{ApplicationActions.applicationActions.Count()}" },
               { "payload", payload.ToString() }
            }).ToString());

            SentrySdk.AddBreadcrumb(
                message: "Received data from property inspector",
                category: actionName,
                level: BreadcrumbLevel.Info,
                data: new Dictionary<string, string>{
                    { "processName", processName },
                    { "controlType", $"{controlType}" },
                    { "mute", $"{Mute}" },
                    { "masterVolume", $"{MasterVolume}" },
                    { "applicationActions", $"{ApplicationActions.applicationActions.Count()}" },
                    { "payload", payload.ToString() }
                 }
            );

            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString())
                {
                    case "setStaticApp":
                        ToggleStaticApp(payload["value"].ToString());
                        break;
                    case "toggleBlacklistedApp":
                        ToggleBlacklistApp(payload["value"].ToString());
                        break;
                    case "refreshApplications":
                        await RefreshApplications();
                        break;
                    case "resetGlobalSettings":
                        await ResetGlobalSettings();
                        break;
                    case "resetSettings":
                        await ResetSettings();
                        await RefreshApplications();
                        break;
                }
            }
        }
    }
}