using BarRaider.SdTools;
using NAudio.CoreAudioApi;
using Newtonsoft.Json.Linq;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NAudio.CoreAudioApi.Interfaces;

namespace AudioMixer
{
    [PluginActionId("com.javanpoirier.audiomixer.application")]
    public class ApplicationAction : PluginBase
    {
        public class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    StaticApplication = null,
                    StaticApplicationName = String.Empty,
                    StaticApplications = null,
                    BlacklistApplications = null,
                    BlacklistedApplications = null,
                    WhitelistApplications = null,
                    WhitelistedApplications = null,
                    VolumeStep = "10",
                };
                return instance;
            }

            [JsonProperty(PropertyName = "staticApplication")]
            public AudioSessionSetting StaticApplication { get; set; }

            [JsonProperty(PropertyName = "staticApplicationName")]
            public String StaticApplicationName { get; set; }

            [JsonProperty(PropertyName = "staticApplications")]
            public List<AudioSession> StaticApplications { get; set; }

            [JsonProperty(PropertyName = "blacklistApplications")]
            public List<AudioSession> BlacklistApplications { get; set; }

            [JsonProperty(PropertyName = "blacklistApplicationName")]
            public String BlacklistApplicationName { get; set; }

            [JsonProperty(PropertyName = "blacklistedApplications")]
            public List<AudioSessionSetting> BlacklistedApplications { get; set; }

            [JsonProperty(PropertyName = "whitelistApplications")]
            public List<AudioSession> WhitelistApplications { get; set; }

            [JsonProperty(PropertyName = "whitelistedApplicationName")]
            public String WhitelistApplicationName { get; set; }

            [JsonProperty(PropertyName = "whitelistedApplications")]
            public List<AudioSessionSetting> WhitelistedApplications { get; set; }

            [JsonProperty(PropertyName = "volumeStep")]
            public String VolumeStep { get; set; }
        }

        private PluginController pluginController = PluginController.Instance;

        private System.Timers.Timer timer = new System.Timers.Timer(3000);
        private bool timerElapsed = false;

        private Image iconImage;
        private Image volumeImage;
        private float volumeStep = 0.1F;
        private float volume;
        private bool isMuted;

        private Utils.ControlType controlType = Utils.ControlType.Application;

        public string actionId = Guid.NewGuid().ToString();
        public string processName;
        public PluginSettings settings;
        public List<AudioSession> AudioSessions { get => pluginController.audioManager.audioSessions.FindAll(session => session.processName == this.processName); }

        public ApplicationAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            this.settings = PluginSettings.CreateDefaultSettings();
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                //this.settings = payload.Settings.ToObject<PluginSettings>();
            }

            this.settings.StaticApplications = pluginController.audioManager.audioSessions;
            SaveSettings();

            Connection.OnSendToPlugin += OnSendToPlugin;

            pluginController.AddAction(this);
            SetAudioSession();
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
            Connection.OnSendToPlugin -= OnSendToPlugin;

            ReleaseAudioSession();

            pluginController.RemoveAction(this);
            timer.Dispose();
        }

        public void SetAudioSession()
        {
            // Previous audio session cleanup.
            ReleaseAudioSession();

            // If audio session is static...
            if (settings.StaticApplication != null)
            {
                // Before self assigning, ensure no other application action has the session.
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
                    applicationAction.processName = null;
                    applicationAction.SetAudioSession();
                }
            }
            else
            {
                // Get the next unassigned audio session. Assign it.
                var audioSession = pluginController.audioManager.audioSessions.Find(session => session.actionId == null);
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
                    Connection.SetImageAsync(Utils.CreateAppKey(lastKnownIcon, volumeImage, false, false, false));
                } else
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, "No sessions available.");
                    Connection.SetDefaultImageAsync();
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

                Boolean selected = pluginController.SelectedAction == this;
                Boolean muted = Convert.ToBoolean(AudioSessions.Find(session => session.session.SimpleAudioVolume.Mute == true));

                iconImage = Utils.CreateIconImage(AudioSessions[0].processIcon);
                volumeImage = Utils.CreateVolumeImage(AudioSessions[0].session.SimpleAudioVolume.Volume);
                Connection.SetImageAsync(Utils.CreateAppKey(iconImage, volumeImage, selected, muted), null, true);
            }

            if (settings.StaticApplication != null) pluginController.UpdateActions();
        }

        public void ReleaseAudioSession()
        {
            AudioSessions.ForEach(session =>
            {
                session.actionId = null;
                session.SessionDisconnnected -= SessionDisconnected;
                session.VolumeChanged -= VolumeChanged;
            });
        }

        void SessionDisconnected(object sender, EventArgs e)
        {
            if (AudioSessions.Count < 2) this.SetAudioSession();
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
                //pluginController.blacklist.Add(AudioSession.session.GetSessionIdentifier);
                //pluginController.audioManager.audioSessions.Remove(AudioSession);
                //pluginController.UpdateActions();
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
                        switch (controlType)
                        {
                            case Utils.ControlType.Mute:
                                volume.Mute = !volume.Mute;
                                break;
                            case Utils.ControlType.VolumeDown:
                                if (volume.Mute) volume.Mute = !volume.Mute;
                                else
                                {
                                    newVolume = volume.Volume - volumeStep;
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
            if (AudioSessions != null) {
                Boolean muted = Convert.ToBoolean(AudioSessions.Find(session => session.session.SimpleAudioVolume.Mute == true));
                Connection.SetImageAsync(Utils.CreateAppKey(iconImage, volumeImage, selected, muted));
            } else
            {
                var lastKnownIcon = Utils.CreateIconImage(Utils.Base64ToBitmap(settings.StaticApplication.processIcon));
                Connection.SetImageAsync(Utils.CreateAppKey(lastKnownIcon, volumeImage, false, false, false));
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
                    SetAudioSession();
                    break;
            }
        }

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);

            if (settings.StaticApplication != null) SetAudioSession();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) {
            // Global Settings exist
            if (payload?.Settings != null && payload.Settings.Count > 0)
            {
                var global = payload.Settings.ToObject<GlobalSettings>();

                // global now has all the settings
                Console.Write(global.VolumeStep);

            }
            else // Global settings do not exist, create new one and SAVE it
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"No global settings found, creating new object");
                var global = new GlobalSettings();
                Connection.SetGlobalSettingsAsync(JObject.FromObject(global));
            }
        }

        private void OnSendToPlugin(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;

            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString().ToLowerInvariant())
                {
                    case "setstaticapplication":
                        // Find the session...
                        var audioSession = pluginController.audioManager.audioSessions.Find(session => session.processName == payload["value"].ToString());
                        settings.StaticApplication = new AudioSessionSetting(audioSession);
                        settings.StaticApplicationName = settings.StaticApplication.processName;
                        SaveSettings();

                        SetAudioSession();
                        break;
                    case "refreshapplications":
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"{GetType()} refreshApplications called");
                        settings.StaticApplications = pluginController.audioManager.audioSessions;
                        settings.BlacklistApplications = pluginController.audioManager.audioSessions;
                        settings.WhitelistApplications = pluginController.audioManager.audioSessions;
                        SaveSettings();
                        break;
                }
            }
        }

        public override void OnTick() { }
    }
}