using BarRaider.SdTools;
using Newtonsoft.Json.Linq;
using streamdeck_client_csharp;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace AudioMixer
{
    [PluginActionId("com.javanpoirier.audiomixer.application")]
    public class PluginAction : PluginBase
    {
        // private PluginController pluginController = PluginController.Instance;
        // private AudioManager audioManager = AudioManager.Instance;
        private PluginSettings settings;
        // private System.Timers.Timer timer = new System.Timers.Timer(3000);
        //private bool timerElapsed = false;
        //private int actionIndex;
        //private Image iconImage;
        //private Image volumeImage;

        public readonly SDConnection connection;
        public readonly KeyCoordinates keyCoordinates;
        public AudioSession audioSession;

        public PluginAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            // Logger.Instance.LogMessage(TracingLevel.INFO, $"Constructor called");
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
            //actionIndex = pluginController.AddAction(this);

            //UpdateKey();
        }

        //public void UpdateKey()
        //{
        //    try
        //    {
        //        //audioSession = pluginController.audioManager.audioSessions[actionIndex];
        //        audioSession.SessionDisconnnected += SessionDisconnected;
        //        audioSession.VolumeChanged += VolumeChanged;
        //    }
        //    catch (IndexOutOfRangeException)
        //    {
        //        connection.SetDefaultImageAsync();
        //    }

        //    try
        //    {
        //        if (audioSession != null)
        //        {
        //            iconImage = Utils.CreateIconImage(audioSession.processIcon);
        //            volumeImage = Utils.CreateVolumeImage(audioSession.volume);

        //            connection.SetImageAsync(Utils.CreateAppKey(iconImage, volumeImage), null, true);
        //        } else
        //        {
        //            connection.SetDefaultImageAsync();
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Instance.LogMessage(TracingLevel.ERROR, e.Message);
        //    }
        //}

        //void SessionDisconnected(object sender, EventArgs e)
        //{
        //    //audioManager.audioSessions.Remove(audioSession);
        //    this.UpdateKey();
        //}

        //void VolumeChanged(object sender, EventArgs e)
        //{
        //    // Delete session from array.
        //    this.UpdateKey();
        //    // Get new session.
        //    volumeImage = Utils.CreateVolumeImage(audioSession.volume);
        //    connection.SetImageAsync(Utils.CreateAppKey(iconImage, volumeImage), null, true);
        //}

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
            //pluginController.RemoveAction(this);

            //timer.Dispose();
        }

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");

            //timerElapsed = false;
            //timer.Elapsed += (object timerSender, ElapsedEventArgs elapsedEvent) =>
            //{
            //    timerElapsed = true;
            //};
            //timer.AutoReset = false;
            //timer.Start();
        }

        public override void KeyReleased(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Released");

            //timer.Stop();
            //if (timerElapsed)
            //{
            //    pluginController.blacklist.Add(audioSession.session.GetSessionIdentifier);
            //}
        }

        public override void OnTick() { }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        #endregion
    }
}