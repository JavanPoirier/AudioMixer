using BarRaider.SdTools;
using NAudio.CoreAudioApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sentry;
using System;
using System.Threading.Tasks;
using System.Timers;

namespace AudioMixer.Actions
{
    [PluginActionId("com.javanpoirier.audiomixer.volumemute")]
    internal class VolumeMuteAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                };
                return instance;
            }
        }

        public readonly SDConnection connection;

        #region Private Members
        private PluginController pluginController = PluginController.Instance;
        private System.Timers.Timer timer = new System.Timers.Timer(3000);
        private bool timerElapsed = false;
        private PluginSettings settings;
        #endregion

        public VolumeMuteAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            this.connection = connection;

            SentrySdk.AddBreadcrumb(
                message: "Initializiing VolumeMute key",
                category: "VolumeMute",
                level: BreadcrumbLevel.Info
            );

            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
                SaveSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }

            connection.SetImageAsync(Utils.CreateMuteKey(), null, true);
        }

        public override void Dispose()
        {
        }

        public override void KeyPressed(KeyPayload payload)
        {
            SentrySdk.AddBreadcrumb(
                message: "Key pressed",
                category: "VolumeMute",
                level: BreadcrumbLevel.Info
            );

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
            SentrySdk.AddBreadcrumb(
                message: "Key released",
                category: "VolumeMute",
                level: BreadcrumbLevel.Info
            );

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
                    SimpleAudioVolume volume = pluginController.SelectedAction?.AudioSessions?[0]?.session?.SimpleAudioVolume;
                    if (volume == null)
                    {
                        throw new Exception("Missing volume object in plugin action. It was likely closed when active.");
                    }

                    volume.Mute = !volume.Mute;

                    pluginController.SelectedAction.AudioSessions.ForEach(session =>
                    {
                        session.session.SimpleAudioVolume.Mute = volume.Mute;
                    });
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, ex.ToString());
                    SentrySdk.CaptureException(ex, scope => { scope.TransactionName = "VolumeMute"; });
                }

            }
        }

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

        public override void OnTick() { }
    }
}
