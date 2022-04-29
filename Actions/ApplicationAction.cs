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

namespace AudioMixer
{
    [PluginActionId("com.javanpoirier.audiomixer.application")]
    public class ApplicationAction : PluginBase
    {
        public class PluginSettings
        {
            public const string VOLUME_STEP = "10";
            public const int INLINE_CONTROLS_TIMEOUT = 0;

            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    VolumeStep = VOLUME_STEP,
                    StaticApplication = null,
                    StaticApplications = new List<AudioSessionSetting>(),
                    BlacklistApplicationName = null,
                    BlacklistApplications = new List<AudioSessionSetting>(),
                    BlacklistedApplications = new List<AudioSessionSetting>(),
                    WhitelistApplicationName = null,
                    WhitelistApplications = new List<AudioSessionSetting>(),
                    WhitelistedApplications = new List<AudioSessionSetting>(),
                    InlineControlsEnabled = true,
                    InlineControlsTimeout = INLINE_CONTROLS_TIMEOUT,
                };
                return instance;
            }

            [JsonProperty(PropertyName = "volumeStep")]
            public string VolumeStep { get; set; }

            [JsonProperty(PropertyName = "staticApplicationName")]
            public string StaticApplicationName { get; set; }

            [JsonProperty(PropertyName = "staticApplication")]
            public AudioSessionSetting StaticApplication { get; set; }

            [JsonProperty(PropertyName = "staticApplications")]
            public List<AudioSessionSetting> StaticApplications { get; set; }

            [JsonProperty(PropertyName = "blacklistApplications")]
            public List<AudioSessionSetting> BlacklistApplications { get; set; }

            [JsonProperty(PropertyName = "blacklistApplicationName")]
            public string BlacklistApplicationName { get; set; }

            [JsonProperty(PropertyName = "blacklistedApplications")]
            public List<AudioSessionSetting> BlacklistedApplications { get; set; }

            [JsonProperty(PropertyName = "whitelistApplications")]
            public List<AudioSessionSetting> WhitelistApplications { get; set; }

            [JsonProperty(PropertyName = "whitelistedApplicationName")]
            public string WhitelistApplicationName { get; set; }

            [JsonProperty(PropertyName = "whitelistedApplications")]
            public List<AudioSessionSetting> WhitelistedApplications { get; set; }

            [JsonProperty(PropertyName = "inlineControlsEnabled")]
            public bool InlineControlsEnabled { get; set; }

            [JsonProperty(PropertyName = "inlineControlsTimeout")]
            public int InlineControlsTimeout { get; set; }
        }

        private PluginController pluginController = PluginController.Instance;
        private Utils.ControlType controlType = Utils.ControlType.Application;
        private System.Timers.Timer timer = new System.Timers.Timer(1500);
        private bool timerElapsed = false;
        private GlobalSettings globalSettings;

        private Image iconImage;
        private Image volumeImage;
        private float volume;
        private bool isMuted;

        public string actionId = Guid.NewGuid().ToString();
        public string processName;
        public PluginSettings settings;

        public List<AudioSession> AudioSessions { get => pluginController.audioManager.audioSessions.FindAll(session => session.processName == this.processName); }

        public ApplicationAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                settings = PluginSettings.CreateDefaultSettings();
                SaveSettings();
            }
            else
            {
                settings = payload.Settings.ToObject<PluginSettings>();
            }

            Connection.GetGlobalSettingsAsync();
            InitializeSettings();

            Connection.OnSendToPlugin += OnSendToPlugin;
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
            Connection.OnSendToPlugin -= OnSendToPlugin;

            ReleaseAudioSession();
            pluginController.RemoveAction(this);

            timer.Dispose();
        }

        public async Task SetAudioSession()
        {
            // Previous audio session cleanup.
            ReleaseAudioSession();

            // If audio session is static...
            if (settings.StaticApplication != null)
            {
                // Before self assignin, ensure no other application action has the session.
                var applicationAction = pluginController.applicationActions.Find(action => {
                    if (action.AudioSessions.Count > 0) return action.AudioSessions[0].processName == settings.StaticApplication.processName;
                    return false;
                });

                // Self assign before re-assigning the last action.
                this.processName = settings.StaticApplication.processName;
                AudioSessions.ForEach(session =>
                {
                    session.actionId = this.actionId;
                });

                // If an application action DOES have the session we want, and it is not this action...
                if (applicationAction != null && applicationAction != this)
                {
                    // Reset and re-assign a new session to the previous action, if any.
                    applicationAction.ReleaseAudioSession();
                    pluginController.AddActionToQueue(applicationAction);
                }
            }
            else
            {
                // Get the next unassigned audio session. Assign it.
                var audioSession = pluginController.audioManager.audioSessions.Find(session =>
                {
                    var blacklistedApplication = settings.BlacklistedApplications.Find(application => application.processName == session.processName);

                    // Ensure no application action has the session statically set.
                    var staticApplicationAction = pluginController.applicationActions.Find(action => action.settings.StaticApplication?.processName == session.processName);

                    return session.actionId == null && blacklistedApplication == null && staticApplicationAction == null;
                });

                if (audioSession != null) {
                    this.processName = audioSession.processName;
                    AudioSessions.ForEach(session =>
                    {
                        session.actionId = this.actionId;
                    });
                }
            }

            if (AudioSessions.Count < 1)
            {
                // If application action is static and audio session is not available, use greyscaled last known icon.
                if (settings.StaticApplication != null)
                {
                    var lastKnownIcon = Utils.CreateIconImage(Utils.Base64ToBitmap(settings.StaticApplication.processIcon));
                    await Connection.SetImageAsync(Utils.CreateAppKey(lastKnownIcon, volumeImage, false, false, false));
                } else
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, "No sessions available.");
                    await Connection.SetDefaultImageAsync();
                }
            }
            else
            {
                // NOTE: All audio sessions in one process are treated as one. If one changes volume so does the other.
                // The reasoning for this comes down to possible unwanted multiple process icons being shown, and with no way
                // of discriminating them I felt this was the best UX. Ex: Discord opens 2 audio sessions, 1 for comms, and the other for notifications.
                // Anywho, this at least makes it easier for the user, I hope.

                AudioSessions.ForEach(session => session.SessionDisconnnected += SessionDisconnected);
                AudioSessions.ForEach(session => session.VolumeChanged += VolumeChanged);

                try
                {
                    Boolean selected = pluginController.SelectedAction == this;
                    Boolean muted = AudioSessions.Any(session => session.session.SimpleAudioVolume.Mute == true);

                    iconImage = Utils.CreateIconImage(AudioSessions[0].processIcon);
                    volumeImage = Utils.CreateVolumeImage(AudioSessions[0].session.SimpleAudioVolume.Volume);
                    await Connection.SetImageAsync(Utils.CreateAppKey(iconImage, volumeImage, selected, muted), null, true);
                } catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, ex.Message.ToString());
                    return;
                }
            }
        }

        public void ReleaseAudioSession()
        {
            AudioSessions.ForEach(session =>
            {
                session.actionId = null;
                session.SessionDisconnnected -= SessionDisconnected;
                session.VolumeChanged -= VolumeChanged;
            });

            this.processName = null;
        }

        void SessionDisconnected(object sender, EventArgs e)
        {
            if (AudioSessions.Count < 2) pluginController.AddActionToQueue(this);
        }

        void VolumeChanged(object sender, AudioSession.VolumeChangedEventArgs e)
        {
            AudioSession senderSession = sender as AudioSession;

            Boolean selected = pluginController.SelectedAction == this;

            // NOTE: Do not use event arguments as they are not the correct values where volume and mute are set independantly
            // causing two events to get fired.
            if (volume != senderSession.session.SimpleAudioVolume.Volume || isMuted != senderSession.session.SimpleAudioVolume.Mute)
            {
                volume = senderSession.session.SimpleAudioVolume.Volume;
                isMuted = senderSession.session.SimpleAudioVolume.Mute;

                // Update any other sessions associated with the process.
                AudioSessions.FindAll(session => session != senderSession).ForEach(session =>
                {
                    session.session.SimpleAudioVolume.Volume = volume;
                    session.session.SimpleAudioVolume.Mute = isMuted;
                });

                volumeImage = Utils.CreateVolumeImage(volume);
                Connection.SetImageAsync(Utils.CreateAppKey(iconImage, volumeImage, selected, isMuted));
            }
        }

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");

            timerElapsed = false;
            timer.Elapsed += (object timerSender, ElapsedEventArgs elapsedEvent) =>
            {
                timerElapsed = true;
            };
            timer.AutoReset = false;
            timer.Start();
        }

        public override void KeyReleased(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Released");

            timer.Stop();
            // If the timer of 3 seconds has passed.
            if (timerElapsed)
            {
                ToggleBlacklistApp(processName);
            } else
            {
                if (controlType == Utils.ControlType.Application)
                {
                    pluginController.SelectedAction = this;
                } else
                {
                    try
                    {
                        SimpleAudioVolume volume = pluginController.SelectedAction.AudioSessions[0].session.SimpleAudioVolume;
                        if (volume == null)
                        {
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

                        pluginController.SelectedAction.AudioSessions.ForEach(session =>
                        {
                            session.session.SimpleAudioVolume.Volume = volume.Volume;
                            session.session.SimpleAudioVolume.Mute = volume.Mute;
                        });
                    } catch (Exception ex)
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, ex.ToString());
                    }
                }
            }
        }

        public void SetSelected(Boolean selected)
        {
            if (AudioSessions.Count > 0) {
                Boolean muted = AudioSessions.Any(session => session.session.SimpleAudioVolume.Mute == true);
                Connection.SetImageAsync(Utils.CreateAppKey(iconImage, volumeImage, selected, muted));
            } else
            {
                if (settings?.StaticApplication != null)
                {
                    var lastKnownIcon = Utils.CreateIconImage(Utils.Base64ToBitmap(settings.StaticApplication.processIcon));
                    Connection.SetImageAsync(Utils.CreateAppKey(lastKnownIcon, volumeImage, false, false, false));
                }
            }
        }

        public void SetControlType(Utils.ControlType controlType)
        {
            this.controlType = controlType;
            switch(controlType)
            {
                case Utils.ControlType.Mute:
                    Connection.SetImageAsync(Utils.CreateMuteKey());
                    break;
                case Utils.ControlType.VolumeDown:
                    Connection.SetImageAsync(Utils.CreateVolumeDownKey());
                    break;
                case Utils.ControlType.VolumeUp:
                    Connection.SetImageAsync(Utils.CreateVolumeUpKey());
                    break;
                default:
                    pluginController.AddActionToQueue(this);
                    break;
            }
        }

        private async void ToggleBlacklistApp(string processName)
        {
            AudioSession audioSession = pluginController.audioManager.audioSessions.Find(session => session.processName == processName);
            if (audioSession != null)
            {
                var existingBlacklistedApp = settings.BlacklistedApplications.Find(session => session.processName == processName);
                if (existingBlacklistedApp != null)
                {
                    settings.BlacklistedApplications.Remove(existingBlacklistedApp);
                    settings.BlacklistApplicationName = null;
                }
                else
                {
                    settings.BlacklistedApplications.Add(new AudioSessionSetting(audioSession));
                    settings.BlacklistApplicationName = null;
                }

                await SetGlobalSettings();
                await SaveSettings();

                //await Connection.SendToPropertyInspectorAsync(JObject.FromObject(settings));
                //pluginController.UpdateActions();
            }
        }

        public void RefreshApplications()
        {
            var applications = new List<AudioSession>(pluginController.audioManager.audioSessions).ConvertAll(session => new AudioSessionSetting(session));

            // Remove duplicate process'
            var distinctApplications = applications.DistinctBy(app => app.processName).ToList();

            settings.StaticApplications = distinctApplications;
            settings.BlacklistApplications = new List<AudioSessionSetting>(distinctApplications);
            settings.WhitelistApplications = new List<AudioSessionSetting>(distinctApplications);

            settings.BlacklistApplications.RemoveAll(app => !settings.WhitelistApplications.Contains(app));
            settings.WhitelistApplications.RemoveAll(app => !settings.BlacklistApplications.Contains(app));

            SetGlobalSettings();
            SaveSettings();
        }

        // Global settings are received on action initialization. Local settings are only received when changed in the PI.
        public async override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) {
            try
            {
                // Global Settings exist
                if (payload?.Settings != null && payload.Settings.Count > 0)
                {
                    globalSettings = payload.Settings.ToObject<GlobalSettings>();
                    settings.VolumeStep = globalSettings.VolumeStep;
                    settings.BlacklistApplications = globalSettings.BlacklistApplications;
                    settings.BlacklistedApplications = globalSettings.BlacklistedApplications;
                    settings.InlineControlsEnabled = globalSettings.InlineControlsEnabled;
                    settings.InlineControlsTimeout = globalSettings.InlineControlsTimeout;
                    await InitializeSettings();
                    await SaveSettings();

                    // Only once the settings are set do we add the action.
                    if (!pluginController.applicationActions.Contains(this))
                    {
                        pluginController.AddAction(this);
                    }
                }
                else // Global settings do not exist, create new one and SAVE it
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"No global settings found, creating new object");
                    globalSettings = new GlobalSettings();
                    globalSettings.InlineControlsEnabled = true;
                    await SetGlobalSettings();
                }

                //pluginController.AddActionToQueue(this);
                pluginController.UpdateActions();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{GetType()} ReceivedGlobalSettings Exception: {ex}");
            }
        }

        private Task SetGlobalSettings()
        {
            globalSettings.VolumeStep = settings.VolumeStep;

            // Concat blacklistApplications with existing blacklistedApplications and remove duplicate process. This is required as the
            // process wanting to be removed fromt he blacklist may not be running.
            globalSettings.BlacklistApplications = settings.BlacklistApplications.Concat(settings.BlacklistedApplications).DistinctBy(app => app.processName).ToList();

            globalSettings.BlacklistedApplications = settings.BlacklistedApplications;
            globalSettings.WhitelistedApplications = settings.WhitelistedApplications;
            globalSettings.InlineControlsEnabled = settings.InlineControlsEnabled;

            return Connection.SetGlobalSettingsAsync(JObject.FromObject(globalSettings));
        }

        private Task InitializeSettings()
        {
            if (String.IsNullOrEmpty(settings.VolumeStep))
            {
                settings.VolumeStep = PluginSettings.VOLUME_STEP;
                SaveSettings();
            }

            if (settings.BlacklistApplications == null)
            {
                settings.BlacklistApplications = new List<AudioSessionSetting>();
                SaveSettings();
            }

            if (settings.BlacklistedApplications == null)
            {
                settings.BlacklistedApplications = new List<AudioSessionSetting>();
                SaveSettings();
            }

            return Task.CompletedTask;
        }

        public async override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            await InitializeSettings();

            await SetGlobalSettings();
            await SaveSettings();
        }

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private async void ResetSettings()
        {
            settings = PluginSettings.CreateDefaultSettings();
            await SetGlobalSettings();
            await SaveSettings();
        }

        private async void OnSendToPlugin(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;

            if (payload["property_inspector"] != null)
            {
                AudioSession audioSession;
                switch (payload["property_inspector"].ToString().ToLowerInvariant())
                {
                    case "setstaticapp":
                        var value = payload["value"].ToString();

                        if (value == "")
                        {
                            settings.StaticApplication = null;
                            settings.StaticApplicationName = null;
                        } else
                        {
                            audioSession = pluginController.audioManager.audioSessions.Find(session => session.processName == payload["value"].ToString());
                            if (audioSession != null)
                            {
                                settings.StaticApplication = new AudioSessionSetting(audioSession);
                                settings.StaticApplicationName = settings.StaticApplication.processName;
                            }
                        }

                        await SaveSettings();
                        pluginController.UpdateActions();
                        break;
                    case "toggleblacklistapp":
                        ToggleBlacklistApp(payload["value"].ToString());
                        break;
                    case "refreshapplications":
                        RefreshApplications();
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