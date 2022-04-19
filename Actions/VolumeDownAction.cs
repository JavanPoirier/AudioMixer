using BarRaider.SdTools;
using NAudio.CoreAudioApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using System.Timers;

namespace AudioMixer.Actions
{
    [PluginActionId("com.javanpoirier.audiomixer.volumedown")]
    internal class VolumeDownAction : PluginBase
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

        private PluginController pluginController = PluginController.Instance;
        private System.Timers.Timer timer = new System.Timers.Timer(3000);
        private bool timerElapsed = false;
        private PluginSettings settings;
        private float volumeStep = 0.1F;

        public VolumeDownAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
                SaveSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }

            connection.SetImageAsync(Utils.CreateVolumeDownKey());
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
                    SimpleAudioVolume volume = pluginController.SelectedAction.AudioSession.session.SimpleAudioVolume;
                    if (volume == null)
                    {
                        throw new Exception("Missing volume object in plugin action. It was likely closed when active.");
                    }

                    float newVolume = 1F;
                    if (volume.Mute) volume.Mute = !volume.Mute;
                    else
                    {
                        newVolume = volume.Volume - volumeStep;
                        volume.Volume = newVolume < 0F ? 0F : newVolume;
                    }
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
