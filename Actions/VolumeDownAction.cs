using BarRaider.SdTools;
using MoreLinq;
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
    public class VolumeDownAction : BaseAction<VolumeSettings>
    {
        public VolumeDownAction(SDConnection connection, InitialPayload payload) : base(connection, payload, ActionType.VOLUME, "VolumeDown") { }

        protected override void InitActionCore() { }

        protected override void SetKey()
        {
            try
            {
                // float volumeStep = (actionSettings.GlobalLock ? (float)Int32.Parse(actionSettings.VolumeStep) / 100 : (float)Int32.Parse(actionSettings.IndependantVolumeStep) / 100) * 100;
                float volumeStep = float.Parse(pluginController.globalSettings.GlobalVolumeStepLock ? pluginController.globalSettings.GlobalVolumeStep : actionSettings.LocalVolumeStep);
                Connection.SetImageAsync(Utils.CreateVolumeDownKey(volumeStep), null, true);
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = actionName; });
                Connection.SetImageAsync(Utils.CreateVolumeDownKey(null), null, true);
            }
        }

        protected override void HandleKeyHeld(object timerSender, ElapsedEventArgs elapsedEvent)
        {
            VolumeDown();
        }

        protected override void HandleKeyReleased(object sender, KeyReleasedEventArgs e)
        {
            VolumeDown();
        }

        private void VolumeDown()
        {
            if (!initialized) return;

            try
            {
                if (ApplicationActions.SelectedAction == null)
                {
                    SentrySdk.AddBreadcrumb(
                       message: "No selected action for volume to control",
                       category: actionName,
                       level: BreadcrumbLevel.Info
                   );
                    return;
                }

                var masterVolume = ApplicationActions.SelectedAction.MasterVolume;
                var mute = ApplicationActions.SelectedAction.Mute;
                
                float newVolume = 1F;
                float volumeStep = float.Parse(pluginController.globalSettings.GlobalVolumeStepLock ? pluginController.globalSettings.GlobalVolumeStep : actionSettings.LocalVolumeStep) / 100;
                if (mute) mute = !mute;
                else
                {
                    newVolume = masterVolume - volumeStep;
                    masterVolume = newVolume < 0F ? 0F : newVolume;
                }

                ApplicationActions.SelectedAction.ActionAudioSessions.ForEach(session =>
                {
                    session.MasterVolume = masterVolume;
                    session.Mute = mute;
                });
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, ex.ToString());
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = actionName; });
            }
        }
    }
}
