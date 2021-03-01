using BarRaider.SdTools;
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
        private AudioManager audioManager = AudioManager.Instance;
        private PluginController pluginController = PluginController.Instance;
        private PluginSettings settings;
        private System.Timers.Timer timer = new System.Timers.Timer(3000);
        private bool timerElapsed = false;
        private Image iconImage;
        private Image volumeImage;
        private bool toggled;

        public readonly SDConnection connection;
        public readonly KeyCoordinates keyCoordinates;
        public AudioSession audioSession;

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
            var index = pluginController.pluginActions.IndexOf(this);

            try
            {
                audioSession = pluginController.audioManager.audioSessions[index];
                audioSession.SessionDisconnnected += SessionDisconnected;
                audioSession.VolumeChanged += VolumeChanged;
            }
            catch (ArgumentOutOfRangeException)
            {
                connection.SetDefaultImageAsync();
            }

            try
            {
                if (audioSession != null)
                {
                    iconImage = Utils.CreateIconImage(audioSession.processIcon);
                    volumeImage = Utils.CreateVolumeImage(audioSession.session.SimpleAudioVolume.Volume);

                    connection.SetImageAsync(Utils.CreateAppKey(iconImage, volumeImage), null, true);
                }
                else
                {
                    connection.SetDefaultImageAsync();
                }
            }
            catch (Exception e)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, e.Message);
            }
        }

        void SessionDisconnected(object sender, EventArgs e)
        {
            pluginController.audioManager.audioSessions.Remove(audioSession);
            audioSession.SessionDisconnnected -= SessionDisconnected;
            audioSession.VolumeChanged -= VolumeChanged;

            pluginController.UpdateActions();
        }

        void VolumeChanged(object sender, EventArgs e)
        {
            volumeImage = Utils.CreateVolumeImage(audioSession.session.SimpleAudioVolume.Volume);
            connection.SetImageAsync(Utils.CreateAppKey(iconImage, volumeImage), null, true);
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
            if (timerElapsed)
            {
                pluginController.blacklist.Add(audioSession.session.GetSessionIdentifier);
            } else
            {
                if (toggled)
                {
                    toggled = false;
                } else
                {
                    toggled = true;
                }
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