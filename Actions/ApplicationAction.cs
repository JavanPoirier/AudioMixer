using BarRaider.SdTools;
using NAudio.CoreAudioApi;
using Newtonsoft.Json.Linq;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using MoreLinq.Extensions;
using Sentry;
using System.Diagnostics;

namespace AudioMixer
{
    [PluginActionId("com.javanpoirier.audiomixer.application")]
    public class ApplicationAction : PluginBase
    {
        public class PluginSettings
        {
            public const string VOLUME_STEP = "10";
            public const double INLINE_CONTROLS_HOLD_DURATION = 200;
            public const int INLINE_CONTROLS_TIMEOUT = 0;

            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    DeviceId = null,
                    VolumeStep = VOLUME_STEP,
                    StaticApplication = null,
                    StaticApplicationSelector = new List<AudioSessionSetting>(),
                    BlacklistApplicationName = null,
                    BlacklistedApplications = new List<AudioSessionSetting>(),
                    BlacklistApplicationSelector = new List<AudioSessionSetting>(),
                    WhitelistApplicationName = null,
                    WhitelistedApplications = new List<AudioSessionSetting>(),
                    WhitelistApplicationSelector = new List<AudioSessionSetting>(),
                    InlineControlsEnabled = true,
                    InlineControlsHoldDuration = INLINE_CONTROLS_HOLD_DURATION,
                    InlineControlsTimeout = INLINE_CONTROLS_TIMEOUT,
                };
                return instance;
            }

            [JsonProperty(PropertyName = "deviceId")]
            public string DeviceId { get; set; }

            [JsonProperty(PropertyName = "volumeStep")]
            public string VolumeStep { get; set; }

            [JsonProperty(PropertyName = "staticApplicationName")]
            public string StaticApplicationName { get; set; }

            [JsonProperty(PropertyName = "staticApplication")]
            public AudioSessionSetting StaticApplication { get; set; }

            [JsonProperty(PropertyName = "staticApplicationSelector")]
            public List<AudioSessionSetting> StaticApplicationSelector { get; set; }

            [JsonProperty(PropertyName = "blacklistApplicationName")]
            public string BlacklistApplicationName { get; set; }

            [JsonProperty(PropertyName = "blacklistedApplications")]
            public List<AudioSessionSetting> BlacklistedApplications { get; set; }

            [JsonProperty(PropertyName = "blacklistApplicationSelector")]
            public List<AudioSessionSetting> BlacklistApplicationSelector { get; set; }

            [JsonProperty(PropertyName = "whitelistedApplicationName")]
            public string WhitelistApplicationName { get; set; }

            [JsonProperty(PropertyName = "whitelistedApplications")]
            public List<AudioSessionSetting> WhitelistedApplications { get; set; }

            [JsonProperty(PropertyName = "whitelistApplicationSelector")]
            public List<AudioSessionSetting> WhitelistApplicationSelector { get; set; }

            [JsonProperty(PropertyName = "inlineControlsEnabled")]
            public bool InlineControlsEnabled { get; set; }

            [JsonProperty(PropertyName = "inlineControlsHoldDuation")]
            public double InlineControlsHoldDuration { get; set; }

            [JsonProperty(PropertyName = "inlineControlsTimeout")]
            public int InlineControlsTimeout { get; set; }
        }

        private PluginController pluginController = PluginController.Instance;
        private Utils.ControlType controlType = Utils.ControlType.Application;
        private Stopwatch stopWatch = new Stopwatch();
        private System.Timers.Timer timer = new System.Timers.Timer(200);
        private GlobalSettings globalSettings;

        private Image iconImage;
        private Image volumeImage;
        private float volume;
        private bool muted;

        public readonly string coords;
        public string processName;
        public PluginSettings settings;

        public List<AudioSession> AudioSessions { get => pluginController.audioManager.audioSessions.ToList().FindAll(session => session.processName == processName); }

        public ApplicationAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (pluginController.deviceId == null)
            {
                pluginController.deviceId = Connection.DeviceId;

                SentrySdk.ConfigureScope(scope =>
                {
                    scope.User = new User
                    {
                        Id = pluginController.deviceId
                    };
                });

                SentrySdk.CaptureMessage("Initialized", scope => scope.TransactionName = "ApplicationAction", SentryLevel.Info);
            }

            coords = $"{payload.Coordinates.Column} {payload.Coordinates.Row}";
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Initializing key at: {coords}");
            Connection.LogSDMessage($"Initializing key at: {coords}");
            SentrySdk.AddBreadcrumb(
               message: "Initializiing Application key",
               category: "ApplicationAction",
               level: BreadcrumbLevel.Info
             );

            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                settings = PluginSettings.CreateDefaultSettings();
                SaveSettings();
            }
            else
            {
                try
                {
                    // TODO: Create & assign a default and merge to allow for compatability of new features.
                    settings = payload.Settings.ToObject<PluginSettings>();
                } catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"Assigning settings from the constructor payload failed. Resetting...");
                    Connection.LogSDMessage($"Assigning settings from the constructor payload failed. Resetting...");
                    SentrySdk.CaptureException(ex, scope => { scope.TransactionName = "ApplicationAction"; });

                    ResetSettings();
                }
            }

            Connection.GetGlobalSettingsAsync();

            Connection.OnSendToPlugin += OnSendToPlugin;
            timer.Elapsed += KeyHoldEvent;
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
            Connection.OnSendToPlugin -= OnSendToPlugin;

            ReleaseAudioSession();
            pluginController.RemoveAction(this);

            if (timer != null) timer.Dispose();
        }

        // NOTE:
        // This does not appear to work as expected when switching pages on the Stream Deck.
        //  Connection.SetDefaultImageAsync();
        //
        // However this works...
        //  Connection.SetImageAsync((string)null, 0, true);
        public void SetAudioSession()
        {
            SentrySdk.AddBreadcrumb(
                message: "Setting audio session",
                category: "ApplicationAction",
                level: BreadcrumbLevel.Info
            );

            // Previous audio session cleanup. Do not update icon to prevent flashing if process remains the same.
            ReleaseAudioSession(false);

            // If audio session is static...
            if (settings.StaticApplication != null)
            {
                // Before self assignin, ensure no other application action has the session.
                var applicationAction = pluginController.applicationActions.Find(action =>
                {
                    if (action.AudioSessions.Count > 0) return action.AudioSessions[0].processName == settings.StaticApplication.processName;
                    return false;
                });

                // Self assign before re-assigning the last action.
                this.processName = settings.StaticApplication.processName;

                // If an application action DOES have the session we want, and it is not this action...
                if (applicationAction != null && applicationAction != this)
                {
                    // Reset and re-assign a new session to the previous action, if any.
                    applicationAction.ReleaseAudioSession();
                    pluginController.AddActionToQueue(applicationAction);
                }

                SentrySdk.AddBreadcrumb(
                    message: "Set static audio session",
                    category: "ApplicationAction",
                    level: BreadcrumbLevel.Info,
                    data: new Dictionary<string, string> {
                        { "processName", $"{processName}" },
                        { "applicationActions", $"{pluginController.applicationActions.Count()}" }
                    }
                 );
            }
            else
            {
                // Get the next unassigned audio session. Assign it.
                var audioSession = pluginController.audioManager.audioSessions.Find(session =>
                {
                    // Ensure it is not a blacklisted application.
                    var blacklistedApplication = settings.BlacklistedApplications.Find(application => application.processName == session.processName);
                    if (blacklistedApplication != null) return false;

                    // Ensure no application action has the application set, both statically and dynamically.
                    var existingApplicationAction = pluginController.applicationActions.Find(action =>
                        action.settings.StaticApplication?.processName == session.processName || action.processName == session.processName
                    );
                    if (existingApplicationAction != null) return false;

                    return true;
                });

                if (audioSession != null) this.processName = audioSession.processName;

                SentrySdk.AddBreadcrumb(
                    message: "Set new audio session",
                    category: "ApplicationAction",
                    level: BreadcrumbLevel.Info,
                    data: new Dictionary<string, string> {
                        { "processName", $"{processName}" },
                        { "applicationActions", $"{pluginController.applicationActions.Count()}" },
                        { "audioSessionCount", $"{AudioSessions.Count}" }
                    }
                 );
            }

            // Do NOT add 0 check condition ot this outer if as it is required for the nested if of StaticApplication.
            if (AudioSessions.Count < 1)
            {
                // If application action is static and audio session is not available, use greyscaled last known icon.
                if (settings.StaticApplication != null)
                {
                    var lastKnownIcon = Utils.CreateIconImage(Utils.Base64ToBitmap(settings.StaticApplication.processIcon));

                    if (controlType == Utils.ControlType.Application)
                    {
                        Connection.SetImageAsync(Utils.CreateAppKey(lastKnownIcon, volumeImage, false, false, false), null, true);
                    }

                    SentrySdk.AddBreadcrumb(
                        message: "Set unavailable static session",
                        category: "ApplicationAction",
                        level: BreadcrumbLevel.Info,
                        data: new Dictionary<string, string> {
                            { "processName", $"{processName}" },
                            { "applicationActions", $"{pluginController.applicationActions.Count()}" }
                        }
                    );
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, "No sessions available.");
                    SentrySdk.AddBreadcrumb(
                        message: "No sessions available",
                        category: "ApplicationAction",
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
                    var audioSessions = AudioSessions.ToList();
                    audioSessions.ForEach(session => session.SessionDisconnnected += SessionDisconnected);
                    audioSessions.ForEach(session => session.VolumeChanged += VolumeChanged);

                    bool selected = pluginController.SelectedAction == this;
                    bool syncedMuted = audioSessions.Any(session => session.session.SimpleAudioVolume.Mute == true);
                    var syncedVolume = audioSessions.First().session.SimpleAudioVolume.Volume;
                    this.volume = syncedVolume;
                    this.muted = syncedMuted;

                    // Update sessions to ensure a consistent volume setting. This inturn will call to set the image.
                    AudioSessions.ForEach(session =>
                    {
                        // Only change them if not already the to be value to prevent recursion. 
                        if (session.session.SimpleAudioVolume.Volume != syncedVolume) session.session.SimpleAudioVolume.Volume = syncedVolume;
                        if (session.session.SimpleAudioVolume.Mute != syncedMuted) session.session.SimpleAudioVolume.Mute = syncedMuted;
                    });

                    if (controlType == Utils.ControlType.Application)
                    {
                        // Assing all sessions the highest volume value found by triggering a valume change.
                        iconImage = Utils.CreateIconImage(audioSessions.First().processIcon);
                        volumeImage = Utils.CreateVolumeImage(this.volume);
                        Connection.SetImageAsync(Utils.CreateAppKey(iconImage, volumeImage, selected, muted), null, true);
                    }

                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Combined audio sessions for process {processName}");
                    SentrySdk.AddBreadcrumb(
                        message: "Combined audio sessions",
                        category: "ApplicationAction",
                        level: BreadcrumbLevel.Info,
                        data: new Dictionary<string, string> {
                            { "processName", $"{processName}" },
                            { "audioSessionCount", $"{AudioSessions.Count}" }
                        }
                    );

                }
                // AudioSession may be released by the time the images are created/set. Retry re-setting session.
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, ex.Message);
                    SentrySdk.CaptureException(ex, scope => { scope.TransactionName = "ApplicationAction"; });

                    pluginController.AddActionToQueue(this);
                }
            }

           /* RefreshApplicationSelectors();*/
        }

        public void ReleaseAudioSession(bool resetIcon = true)
        {
            try
            {
                SentrySdk.AddBreadcrumb(
                    message: "Releasing audio session",
                    category: "ApplicationAction",
                    level: BreadcrumbLevel.Info,
                     data: new Dictionary<string, string> {
                        { "processName", $"{processName}" },
                        { "applicationActions", $"{pluginController.applicationActions.Count()}" }
                     }
                );

                if (controlType == Utils.ControlType.Application)
                {
                    // We only want to reset the icon if we don't know what it's next one will be.
                    if (resetIcon && settings.StaticApplication == null) Connection.SetImageAsync((string)null, null, true);
                }

                // Iterate through all audio sessions as by this time it could already been removed.
                AudioSessions.ToList().ForEach(session =>
                {
                    session.SessionDisconnnected -= SessionDisconnected;
                    session.VolumeChanged -= VolumeChanged;
                });
            } catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, ex.Message);
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = "ApplicationAction"; });
            }

            this.processName = null;
        }

        void SessionDisconnected(object sender, EventArgs e)
        {
            SentrySdk.AddBreadcrumb(
                message: "Session disconnected",
                category: "ApplicationAction",
                level: BreadcrumbLevel.Info,
                data: new Dictionary<string, string> {
                    { "processName", $"{processName}" },
                    { "controlType", $"{controlType}" },
                    { "isMuted", $"{muted}" },
                    { "currentVolume", $"{volume}" },
                    { "applicationActions", $"{pluginController.applicationActions.Count()}" }
                }
            );

            if (AudioSessions.Count < 2) pluginController.AddActionToQueue(this);
        }

        void VolumeChanged(object sender, AudioSession.VolumeChangedEventArgs e)
        {
            SentrySdk.AddBreadcrumb(
                message: "Volume changed",
                category: "ApplicationAction",
                level: BreadcrumbLevel.Info,
                data: new Dictionary<string, string> {
                    { "processName", $"{processName}" },
                    { "controlType", $"{controlType}" },
                    { "isMuted", $"{muted}" },
                    { "currentVolume", $"{volume}" },
                    { "applicationActions", $"{pluginController.applicationActions.Count()}" }
                }
            );

            AudioSession senderSession = sender as AudioSession;
            volume = senderSession.session.SimpleAudioVolume.Volume;
            muted = senderSession.session.SimpleAudioVolume.Mute;

            // Update sessions to ensure a consistent volume setting.
            AudioSessions.ToList().ForEach(session =>
            {
                // Only change them if not already the to be value to prevent recursion. 
                if (session.session.SimpleAudioVolume.Volume != senderSession.session.SimpleAudioVolume.Volume)
                {
                    session.session.SimpleAudioVolume.Volume = senderSession.session.SimpleAudioVolume.Volume;
                }
                if (session.session.SimpleAudioVolume.Mute != senderSession.session.SimpleAudioVolume.Mute)
                {
                    session.session.SimpleAudioVolume.Mute = muted;
                }
            });

            // Only update the key if its being displayed as an application.
            if (controlType == Utils.ControlType.Application)
            {
                volumeImage = Utils.CreateVolumeImage(volume);
                Boolean selected = pluginController.SelectedAction == this;
                Connection.SetImageAsync(Utils.CreateAppKey(iconImage, volumeImage, selected, muted), null, true);
            }
        }

        public override void KeyPressed(KeyPayload payload)
        {
            SentrySdk.AddBreadcrumb(
                message: "Key pressed",
                category: "ApplicationAction",
                level: BreadcrumbLevel.Info,
                data: new Dictionary<string, string> {
                    { "processName", $"{processName}" },
                    { "controlType", $"{controlType}" },
                    { "isMuted", $"{muted}" },
                    { "currentVolume", $"{volume}" },
                    { "applicationActions", $"{pluginController.applicationActions.Count()}" }
                }
            );

            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");

            stopWatch.Restart();
            timer.Start();
        }

        private void KeyHoldEvent(object timerSender, ElapsedEventArgs elapsedEvent)
        {
            if (controlType == Utils.ControlType.VolumeUp || controlType == Utils.ControlType.VolumeDown)
            {
                SetVolume();
            }
        }

        public override void KeyReleased(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Released");
            SentrySdk.AddBreadcrumb(
                message: "Key released",
                category: "ApplicationAction",
                level: BreadcrumbLevel.Info,
                data: new Dictionary<string, string> {
                    { "processName", $"{processName}" },
                    { "controlType", $"{controlType}" },
                    { "isMuted", $"{muted}" },
                    { "currentVolume", $"{volume}" },
                    { "applicationActions", $"{pluginController.applicationActions.Count()}" }
                }
            );

            timer.Stop();
            stopWatch.Stop();

            if (controlType == Utils.ControlType.Application)
            {
                if (processName == null) return;

                if (stopWatch.ElapsedMilliseconds >= 2000)
                {
                    ToggleBlacklistApp(processName);

                } else if (AudioSessions.Count > 0) pluginController.SelectedAction = this;
            } else
            {
                SetVolume();
            }
        }

        private void SetVolume()
        {
            try
            {
                SimpleAudioVolume volume = pluginController.SelectedAction?.AudioSessions?[0]?.session?.SimpleAudioVolume;
                if (volume == null)
                {
                    pluginController.SelectedAction = null;

                    // TODO: Can be removed?
                    throw new Exception("Missing volume object in plugin action. It was likely closed when active.");
                }

                float newVolume = 1F;
                float volumeStep = (float)Int32.Parse(settings.VolumeStep) / 100;
                switch (controlType)
                {
                    case Utils.ControlType.Mute:
                        volume.Mute = !volume.Mute;
                        break;
                    case Utils.ControlType.VolumeDown:
                        if (volume.Mute) volume.Mute = !volume.Mute;
                        else
                        {
                            newVolume = volume.Volume - (volumeStep);
                            volume.Volume = newVolume < 0F ? 0F : newVolume;
                        }
                        break;
                    case Utils.ControlType.VolumeUp:
                        if (volume.Mute) volume.Mute = !volume.Mute;
                        else
                        {
                            newVolume = volume.Volume + volumeStep;
                            volume.Volume = newVolume > 1F ? 1F : newVolume;
                        }
                        break;
                }

                // Assign to all sessions
                pluginController.SelectedAction.AudioSessions.ToList().ForEach(session =>
                {
                    session.session.SimpleAudioVolume.Volume = volume.Volume;
                    session.session.SimpleAudioVolume.Mute = volume.Mute;
                });
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, ex.ToString());
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = "ApplicationAction"; });
            }
        }

        public void SetSelected(Boolean selected)
        {
            if (AudioSessions.Count > 0)
            {
                Boolean muted = AudioSessions.Any(session => session.session.SimpleAudioVolume.Mute == true);
                Connection.SetImageAsync(Utils.CreateAppKey(iconImage, volumeImage, selected, muted), null, true);
            }
            else
            {
                if (settings?.StaticApplication != null)
                {
                    var lastKnownIcon = Utils.CreateIconImage(Utils.Base64ToBitmap(settings.StaticApplication.processIcon));
                    Connection.SetImageAsync(Utils.CreateAppKey(lastKnownIcon, volumeImage, false, false, false), null, true);
                }
            }
        }

        public async Task SetControlType(Utils.ControlType controlType)
        {
            this.controlType = controlType;
            switch (controlType)
            {
                case Utils.ControlType.Mute:
                    await Connection.SetImageAsync(Utils.CreateMuteKey(), null, true);
                    break;
                case Utils.ControlType.VolumeDown:
                    await Connection.SetImageAsync(Utils.CreateVolumeDownKey((float)Int32.Parse(settings.VolumeStep)), null, true);
                    break;
                case Utils.ControlType.VolumeUp:
                    await Connection.SetImageAsync(Utils.CreateVolumeUpKey((float)Int32.Parse(settings.VolumeStep)), null, true);
                    break;
                default:
                    pluginController.AddActionToQueue(this);
                    break;
            }
        }

        private async void ToggleBlacklistApp(string processName)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{processName} added to blacklist");
            SentrySdk.AddBreadcrumb(
                message: "Add to blacklist",
                category: "ApplicationAction",
                level: BreadcrumbLevel.Info,
                data: new Dictionary<string, string> {
                    { "processName", $"{processName}" },
                    { "applicationActions", $"{pluginController.applicationActions.Count()}" }
                }
            );

            var staticApp = globalSettings.StaticApplications.Find(session => session.processName == processName);
            if (staticApp != null) return;

            var existingBlacklistedApp = settings.BlacklistedApplications.Find(session => session.processName == processName);
            if (existingBlacklistedApp != null)
            {
                settings.BlacklistedApplications.Remove(existingBlacklistedApp);
                settings.BlacklistApplicationName = null;
            }
            else
            {
                AudioSession audioSession = pluginController.audioManager.audioSessions.Find(session => session.processName == processName);

                if (audioSession != null)
                {
                    settings.BlacklistedApplications.Add(new AudioSessionSetting(audioSession));
                    settings.BlacklistApplicationName = null;
                }
            }

            await RefreshApplicationSelectors();
        }

        public async Task RefreshApplicationSelectors()
        {
            SentrySdk.AddBreadcrumb(
                message: "Refresh applications",
                category: "ApplicationAction",
                level: BreadcrumbLevel.Info
            );

            var applications = new List<AudioSession>(pluginController.audioManager.audioSessions).ConvertAll(session => new AudioSessionSetting(session));

            // Remove duplicate process'
            var distinctApplications = applications.DistinctBy(app => app.processName).ToList();

            settings.StaticApplicationSelector = new List<AudioSessionSetting>(distinctApplications);
            settings.BlacklistApplicationSelector = new List<AudioSessionSetting>(distinctApplications);
            settings.WhitelistApplicationSelector = new List<AudioSessionSetting>(distinctApplications);

            // TODO: Add additional logic removing imposible combinations. Handle if one was already set.
            settings.StaticApplicationSelector.RemoveAll(app => globalSettings.StaticApplications.Find(_app => _app.processName == app.processName) != null);
            settings.StaticApplicationSelector.RemoveAll(app => settings.BlacklistedApplications.Find(_app => _app.processName == app.processName) != null);
            if (settings.StaticApplication != null && !settings.StaticApplicationSelector.Contains(settings.StaticApplication)) settings.StaticApplicationSelector.Add(settings.StaticApplication);

            settings.BlacklistApplicationSelector.RemoveAll(app => settings.WhitelistedApplications.Find(_app => _app.processName == app.processName) != null);
            settings.BlacklistApplicationSelector.RemoveAll(app => globalSettings.StaticApplications.Find(_app => _app.processName == app.processName) != null);

            settings.WhitelistApplicationSelector.RemoveAll(app => settings.BlacklistedApplications.Find(_app => _app.processName == app.processName) != null);

            await SetGlobalSettings();
            await SaveSettings();
        }

        // Global settings are received on action initialization. Local settings are only received when changed in the PI.
        public async override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
        {
            try
            {
                SentrySdk.AddBreadcrumb(
                    message: "Received global settings",
                    category: "ApplicationAction",
                    level: BreadcrumbLevel.Info,
                    data: new Dictionary<string, string> { { "settings", payload.Settings.ToString() } }
                );

                // Global Settings exist
                if (payload?.Settings != null && payload.Settings.Count > 0)
                {
                    globalSettings = payload.Settings.ToObject<GlobalSettings>();
                    settings.VolumeStep = globalSettings.VolumeStep;
                    settings.BlacklistedApplications = globalSettings.BlacklistedApplications;
                    settings.WhitelistedApplications = globalSettings.WhitelistedApplications;
                    settings.InlineControlsEnabled = globalSettings.InlineControlsEnabled;
                    settings.InlineControlsHoldDuration = globalSettings?.InlineControlsHoldDuration ?? GlobalSettings.INLINE_CONTROLS_HOLD_DURATION;
                    settings.InlineControlsTimeout = globalSettings?.InlineControlsTimeout ?? GlobalSettings.INLINE_CONTROLS_TIMEOUT;
                    await SaveSettings();

                    // Only once the settings are set do we then add the action.
                    var currentAction = pluginController.applicationActions.Find((action) => action.coords == coords);
                    if (currentAction == null)
                    {
                        pluginController.AddAction(this);
                        RefreshApplicationSelectors();
                    }

                    pluginController.UpdateActions();
                }
                else // Global settings do not exist, create new one and SAVE it
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"No global settings found, creating new object");
                    globalSettings = GlobalSettings.CreateDefaultSettings();
                    await SetGlobalSettings();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{GetType()} ReceivedGlobalSettings Exception: {ex}");
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = "ApplicationAction"; });

                ResetSettings();
            }
        }

        private Task SetGlobalSettings()
        {
            globalSettings.VolumeStep = settings.VolumeStep;
            globalSettings.BlacklistedApplications = settings.BlacklistedApplications;
            globalSettings.WhitelistedApplications = settings.WhitelistedApplications;
            globalSettings.InlineControlsEnabled = settings.InlineControlsEnabled;
            globalSettings.InlineControlsHoldDuration = settings.InlineControlsHoldDuration;

            return Connection.SetGlobalSettingsAsync(JObject.FromObject(globalSettings));
        }

        public override async void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            try
            {
                SentrySdk.AddBreadcrumb(
                    message: "Received settings",
                    category: "ApplicationAction",
                    level: BreadcrumbLevel.Info,
                    data: new Dictionary<string, string> { { "setting", payload.Settings.ToString() } }
                );

                Tools.AutoPopulateSettings(settings, payload.Settings);

                await SetGlobalSettings();
                await SaveSettings();
            } catch(Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{GetType()} ReceivedSettings Exception: {ex}");
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = "ApplicationAction"; });

                ResetSettings();
            }
        }

        private Task SaveSettings()
        {
            SentrySdk.AddBreadcrumb(
                message: "Save settings",
                category: "ApplicationAction",
                level: BreadcrumbLevel.Info,
                data: new Dictionary<string, string> { { "setting", settings.ToString() } }
            );

            settings.DeviceId = pluginController.deviceId;
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private async void ResetSettings()
        {
            SentrySdk.AddBreadcrumb(
                message: "Reset settings",
                category: "ApplicationAction",
                level: BreadcrumbLevel.Info,
                data: new Dictionary<string, string> { { "setting", settings.ToString() } }
            );

            globalSettings = GlobalSettings.CreateDefaultSettings();
            settings = PluginSettings.CreateDefaultSettings();
            await SetGlobalSettings();
            await SaveSettings();

            await RefreshApplicationSelectors();
        }

        private async void OnSendToPlugin(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;

            Logger.Instance.LogMessage(TracingLevel.INFO, JObject.FromObject(new Dictionary<string, string> {
                        { "processName", processName },
                        { "controlType", $"{controlType}" },
                        { "isMuted", $"{muted}" },
                        { "currentVolume", $"{volume}" },
                        { "applicationActions", $"{pluginController.applicationActions.Count()}" },
                        { "payload", payload.ToString() }
                    }).ToString());

            SentrySdk.AddBreadcrumb(
                message: "Received data from property inspector",
                category: "ApplicationAction",
                level: BreadcrumbLevel.Info,
                data: new Dictionary<string, string>{
                    { "processName", processName },
                    { "controlType", $"{controlType}" },
                    { "isMuted", $"{muted}" },
                    { "currentVolume", $"{volume}" },
                    { "applicationActions", $"{pluginController.applicationActions.Count()}" },
                    { "payload", payload.ToString() }
                 }
            );

            if (payload["property_inspector"] != null)
            {
                AudioSession audioSession;
                switch (payload["property_inspector"].ToString().ToLowerInvariant())
                {
                    case "setstaticapp":
                        var value = payload["value"].ToString();

                        if (value == "")
                        {
                            if (settings.StaticApplication == null) return;

                            globalSettings.StaticApplications.Remove(settings.StaticApplication);

                            settings.StaticApplication = null;
                            settings.StaticApplicationName = null;
                        }
                        else
                        {
                            audioSession = pluginController.audioManager.audioSessions.Find(session => session.processName == payload["value"].ToString());
                            if (audioSession == null) return;
                            // Ensure it is not already a static application.
                            if (globalSettings.StaticApplications.Find(session => session.processName == payload["value"].ToString()) != null) return;
                                                        
                            settings.StaticApplication = new AudioSessionSetting(audioSession);
                            settings.StaticApplicationName = settings.StaticApplication.processName;

                            globalSettings.StaticApplications.Add(settings.StaticApplication);
                        }

                        await SetGlobalSettings();
                        await SaveSettings();

                        pluginController.UpdateActions();
                        break;
                    case "toggleblacklistapp":
                        ToggleBlacklistApp(payload["value"].ToString());
                        break;
                    case "refreshapplications":
                        RefreshApplicationSelectors();
                        break;
                    case "resetsettings":
                        ResetSettings();
                        break;
                }
            }
        }

        public override void OnTick() { }
    }
}