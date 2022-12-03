using BarRaider.SdTools;
using NAudio.CoreAudioApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sentry;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using static AudioMixer.Utils;

namespace AudioMixer.Actions
{
    [PluginActionId("com.javanpoirier.audiomixer.volumedown")]
    internal class VolumeDownAction : PluginBase
    {
        private class PluginSettings
        {
            public const string VOLUME_STEP = "10";

            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    GlobalLock = true,
                    GlobalVolumeStep = VOLUME_STEP,
                    IndependantVolumeStep = VOLUME_STEP,
                    InlineControlsEnabled = true,
                };
                return instance;
            }

            [JsonProperty(PropertyName = "globalLock")]
            public bool GlobalLock { get; set; } = true;

            [JsonProperty(PropertyName = "globalVolumeStep")]
            public string GlobalVolumeStep { get; set; }

            [JsonProperty(PropertyName = "independantVolumeStep")]
            public string IndependantVolumeStep { get; set; }

            [JsonProperty(PropertyName = "inlineControlsEnabled")]
            public bool InlineControlsEnabled { get; set; }
        }

        private PluginController pluginController = PluginController.Instance;
        private System.Timers.Timer timer = new System.Timers.Timer(GlobalSettings.INLINE_CONTROLS_HOLD_DURATION);
        private PluginSettings settings;

        private SDConnection connection;
        private GlobalSettings globalSettings;

        public VolumeDownAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            this.connection = connection;

            SentrySdk.AddBreadcrumb(
                message: "Initializiing VolumeDown key",
                category: "VolumeDownAction",
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

            Connection.GetGlobalSettingsAsync();
            connection.SetImageAsync(Utils.CreateVolumeDownKey(null), null, true);

            timer.Elapsed += KeyHoldEvent;
        }

        public override void Dispose()
        {
        }

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");
            SentrySdk.AddBreadcrumb(
                message: "Key pressed",
                category: "VolumeDownAction",
                level: BreadcrumbLevel.Info
            );

            timer.Start();
        }

        private void KeyHoldEvent(object timerSender, ElapsedEventArgs elapsedEvent)
        {
            VolumeDown();
        }

        public override void KeyReleased(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Released");
            SentrySdk.AddBreadcrumb(
               message: "Key released",
               category: "VolumeDownAction",
               level: BreadcrumbLevel.Info
            );

            timer.Stop();

            VolumeDown();
        }

        private void VolumeDown()
        {
            try
            {
                SimpleAudioVolume volume = pluginController.SelectedAction?.AudioSessions?[0]?.session?.SimpleAudioVolume;
                if (volume == null)
                {
                    SentrySdk.AddBreadcrumb(
                       message: "No selected action for volume to control",
                       category: "VolumeDownAction",
                       level: BreadcrumbLevel.Info
                   );
                   return;
                }

                float newVolume = 1F;
                float volumeStep = float.Parse(settings.GlobalLock ? globalSettings.VolumeStep : settings.IndependantVolumeStep) / 100;
                if (volume.Mute) volume.Mute = !volume.Mute;
                else
                {
                    newVolume = volume.Volume - volumeStep;
                    volume.Volume = newVolume < 0F ? 0F : newVolume;
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
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = "VolumeDownAction"; });
            }
        }

        // Global settings are received on action initialization. Local settings are only received when changed in the PI.
        public override async void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
        {
            try
            {
                SentrySdk.AddBreadcrumb(
                    message: "Received global settings",
                    category: "VolumeDownAction",
                    level: BreadcrumbLevel.Info,
                    data: new Dictionary<string, string> { { "settings", payload.Settings.ToString() } }
                );

                // Global Settings exist
                if (payload?.Settings != null && payload.Settings.Count > 0)
                {
                    globalSettings = payload.Settings.ToObject<GlobalSettings>();
                    settings.GlobalVolumeStep = globalSettings.VolumeStep;
                    settings.InlineControlsEnabled = globalSettings.InlineControlsEnabled;
                    await InitializeSettings();
                    await SaveSettings();
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
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = "VolumeDownAction"; });

                globalSettings = GlobalSettings.CreateDefaultSettings();
                await SetGlobalSettings();
            }
        }

        private Task SetGlobalSettings()
        {
            globalSettings.VolumeStep = settings.GlobalVolumeStep;
            globalSettings.InlineControlsEnabled = settings.InlineControlsEnabled;

            return Connection.SetGlobalSettingsAsync(JObject.FromObject(globalSettings));
        }

        private async Task InitializeSettings()
        {
            if (String.IsNullOrEmpty(settings.IndependantVolumeStep))
            {
                settings.IndependantVolumeStep = PluginSettings.VOLUME_STEP;
            }

            float volumeStep = float.Parse(settings.GlobalLock ? settings.GlobalVolumeStep : settings.IndependantVolumeStep);
            await connection.SetImageAsync(Utils.CreateVolumeDownKey(volumeStep), null, true);

            await SaveSettings();
        }

        public override async void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            try
            {
                SentrySdk.AddBreadcrumb(
                    message: "Received settings",
                    category: "VolumeDownAction",
                    level: BreadcrumbLevel.Info,
                    data: new Dictionary<string, string> { { "setting", payload.Settings.ToString() } }
                );

                Tools.AutoPopulateSettings(settings, payload.Settings);
                await InitializeSettings();

                await SetGlobalSettings();
                await SaveSettings();

                float volumeStep = float.Parse(settings.GlobalLock ? settings.GlobalVolumeStep : settings.IndependantVolumeStep);
                await connection.SetImageAsync(Utils.CreateVolumeDownKey(volumeStep), null, true);
            } catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{GetType()} ReceivedSettings Exception: {ex}");
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = "VolumeDownAction"; });

                settings = PluginSettings.CreateDefaultSettings();
                await SaveSettings();
            }
        }

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        public override void OnTick() { }
    }
}
