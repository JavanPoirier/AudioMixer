using BarRaider.SdTools;
using MoreLinq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sentry;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using static AudioMixer.Actions.VolumeMuteAction;

namespace AudioMixer.Actions
{
    [PluginActionId("com.javanpoirier.audiomixer.volumemute")]
    public class VolumeMuteAction : BaseAction<VolumeMuteSettings>
    {
        public class VolumeMuteSettings : GlobalSettings
        {
            public VolumeMuteSettings() : base() { }

            public override GlobalSettings CreateInstance(string UUID)
            {
                return new VolumeMuteSettings { UUID = UUID };
            }
        }

        public VolumeMuteAction(SDConnection connection, InitialPayload payload) : base(connection, payload, ActionType.VOLUME, "VolumeMute") { }

        protected override void InitActionCore() { }

        protected override void SetKey()
        {
            Connection.SetImageAsync(Utils.CreateVolumeMuteKey(), null, true);
        }

        protected override void HandleKeyReleased(object sender, KeyReleasedEventArgs e)
        {
            VolumeMute();
        }

        private void VolumeMute()
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

                ApplicationActions.SelectedAction.ActionAudioSessions.ForEach(session =>
                {
                    session.Mute = !session.Mute;
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
