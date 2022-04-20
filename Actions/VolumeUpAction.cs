using BarRaider.SdTools;
using NAudio.CoreAudioApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using System.Timers;

namespace AudioMixer.Actions
{
    [PluginActionId("com.javanpoirier.audiomixer.volumeup")]
    internal class VolumeUpAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    Volume = "1"
                };
                return instance;
            }

            [JsonProperty(PropertyName = "volume")]
            public String Volume { get; set; }
        }

        public readonly SDConnection connection;

        #region Private Memebers
        private PluginController pluginController = PluginController.Instance;
        private System.Timers.Timer timer = new System.Timers.Timer(3000);
        private bool timerElapsed = false;
        private float volumeStep = 0.1F;
        private PluginSettings settings;
        #endregion

        public VolumeUpAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            this.connection = connection;

            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
                SaveSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }

            connection.SetImageAsync(Utils.CreateVolumeUpKey());
        }

        public override void Dispose()
        {
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
            }
            else
            {
                try
                {
                    SimpleAudioVolume volume = pluginController.SelectedAction.AudioSessions[0].session.SimpleAudioVolume;
                    if (volume == null)
                    {
                        throw new Exception("Missing volume object in plugin action. It was likely closed when active.");
                    }

                    float newVolume = 1F;
                    if (volume.Mute) volume.Mute = !volume.Mute;
                    else
                    {
                        newVolume = volume.Volume + volumeStep;
                        volume.Volume = newVolume > 1F ? 1F : newVolume;
                    }

                    pluginController.SelectedAction.AudioSessions.ForEach(session =>
                    {
                        session.session.SimpleAudioVolume.Volume = volume.Volume;
                        session.session.SimpleAudioVolume.Mute = volume.Mute;
                    });
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, ex.ToString());
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
