using BarRaider.SdTools;
using NAudio.CoreAudioApi;
using Newtonsoft.Json.Linq;
using streamdeck_client_csharp;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace AudioMixer
{
    [PluginActionId("com.javanpoirier.audiomixer.application")]
    public class PluginAction : PluginBase
    {
        private PluginController pluginController = PluginController.Instance;
        private PluginSettings settings;
        private System.Timers.Timer timer = new System.Timers.Timer(3000);
        private bool timerElapsed = false;
        private Image iconImage;
        private Image volumeImage;
        private Utils.ControlType controlType = Utils.ControlType.Application;

        public readonly SDConnection connection;
        public readonly KeyCoordinates keyCoordinates;
        public AudioSession audioSession;

        private float volumeStep = 0.1F;

        public PluginAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            this.connection = connection;
            keyCoordinates = payload.Coordinates;

            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
                SaveSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }

            pluginController.AddAction(this);
            UpdateKey();
        }

        public void UpdateKey()
        {
            if (audioSession != null)
            {
                audioSession.SessionDisconnnected -= SessionDisconnected;
                audioSession.VolumeChanged -= VolumeChanged;
            }

            try
            {
                var index = pluginController.pluginActions.IndexOf(this);
                audioSession = pluginController.audioManager.audioSessions[index];
            }
            catch (ArgumentOutOfRangeException)
            {
                audioSession = null;
            }


            if (audioSession != null)
            {
                Boolean selected = pluginController.SelectedAction == this;
                Boolean muted = audioSession.session.SimpleAudioVolume.Mute;

                audioSession.SessionDisconnnected += SessionDisconnected;
                audioSession.VolumeChanged += VolumeChanged;

                iconImage = Utils.CreateIconImage(audioSession.processIcon);
                volumeImage = Utils.CreateVolumeImage(audioSession.session.SimpleAudioVolume.Volume);
                connection.SetImageAsync(Utils.CreateAppKey(iconImage, volumeImage, selected, muted), null, true);
            }
            else
            {
                connection.SetDefaultImageAsync();
            }     
        }

        void SessionDisconnected(object sender, EventArgs e)
        {
            if (audioSession != null)
            {
                pluginController.audioManager.audioSessions.Remove(audioSession);
                audioSession.SessionDisconnnected -= SessionDisconnected;
                audioSession.VolumeChanged -= VolumeChanged;
            }

            pluginController.UpdateActions();
        }

        void VolumeChanged(object sender, EventArgs e)
        {
            Boolean selected = pluginController.SelectedAction == this;
            Boolean muted = audioSession.session.SimpleAudioVolume.Mute;
            volumeImage = Utils.CreateVolumeImage(audioSession.session.SimpleAudioVolume.Volume);
            connection.SetImageAsync(Utils.CreateAppKey(iconImage, volumeImage, selected, muted), null, true);
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
            pluginController.RemoveAction(this);
            timer.Dispose();

            if (audioSession != null)
            {
                audioSession.SessionDisconnnected -= SessionDisconnected;
                audioSession.VolumeChanged -= VolumeChanged;
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
                pluginController.blacklist.Add(audioSession.session.GetSessionIdentifier);
                pluginController.audioManager.audioSessions.Remove(audioSession);
                pluginController.UpdateActions();
            } else
            {

                if (controlType == Utils.ControlType.Application)
                {
                    pluginController.SelectedAction = this;
                } else
                {
                    try
                    {
                        SimpleAudioVolume volume = pluginController.SelectedAction.audioSession.session.SimpleAudioVolume;
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
                    } catch (Exception ex)
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, ex.ToString());
                    }
                }
            }
        }

        public void setSelected(Boolean selected)
        {
            Boolean muted = audioSession.session.SimpleAudioVolume.Mute;
            connection.SetImageAsync(Utils.CreateAppKey(iconImage, volumeImage, selected, muted), null, true);
        }

        public void SetControlType(Utils.ControlType controlType)
        {
            this.controlType = controlType;
            switch(controlType)
            {
                case Utils.ControlType.Mute:
                    connection.SetImageAsync(Utils.CreateMuteKey());
                    break;
                case Utils.ControlType.VolumeDown:
                    connection.SetImageAsync(Utils.CreateVolumeDownKey());
                    break;
                case Utils.ControlType.VolumeUp:
                    connection.SetImageAsync(Utils.CreateVolumeUpKey());
                    break;
                default:
                    UpdateKey();
                    break;
            }
        }

        public override void OnTick() { }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }
    }
}