using System;
using System.Diagnostics;
using NAudio.CoreAudioApi;
using System.Drawing;
using NAudio.CoreAudioApi.Interfaces;
using BarRaider.SdTools;

namespace AudioMixer
{
    public class AudioSessionSetting
    {
        public readonly string name;
        public string processIcon { get; private set; }

        public AudioSessionSetting(AudioSession audioSession)
        {
            name = audioSession.name;
            processIcon = Utils.BitmapToBase64(audioSession.processIcon);
        }
    }

    public class AudioSession : IAudioSessionEventsHandler, IDisposable
    {
        private PluginController pluginController;
        private MMDevice device;
        public readonly AudioSessionControl session;

        public event EventHandler SessionDisconnnected;
        public event EventHandler VolumeChanged;

        public string actionId;
        public readonly string name;
        public Bitmap processIcon { get; private set; }

        public AudioSession(PluginController pluginController, MMDevice device, AudioSessionControl session)
        {
            this.pluginController = pluginController;
            this.device = device;
            this.session = session;

            try
            {
                Process process = Process.GetProcessById((int)session.GetProcessID);

                name = process.ProcessName;
                processIcon = Icon.ExtractAssociatedIcon(process.MainModule.FileName).ToBitmap();

                session.RegisterEventClient(this);
            } catch (Exception ex)
            {
                switch (ex.GetType().Name)
                {
                    case "Exception":
                        this.Dispose();
                        break;
                    default:
                        Logger.Instance.LogMessage(TracingLevel.ERROR, ex.GetType().Name);
                        Logger.Instance.LogMessage(TracingLevel.ERROR, ex.Message);

                        // TODO: Find case.
                        //Logger.Instance.LogMessage(TracingLevel.ERROR, "This application must be run as an administrator.");
                        throw;
                }
            }
        }

        public void Dispose()
        {
            this.pluginController.audioManager.audioSessions.Remove(this);

            var applicationActions = this.pluginController.applicationActions.FindAll(actions => actions.actionId == this.actionId);
            applicationActions.ForEach(action => action.SetAudioSession());
        }

        public void OnVolumeChanged(float volume, bool isMuted)
        {
            if (VolumeChanged != null)
                VolumeChanged(this, null);
        }
        
        public void OnDisplayNameChanged(string displayName)
        {
        }

        public void OnIconPathChanged(string iconPath)
        {
        }

        public void OnChannelVolumeChanged(uint channelCount, IntPtr newVolumes, uint channelIndex)
        {
        }

        public void OnGroupingParamChanged(ref Guid groupingId)
        {
        }

        public void OnStateChanged(AudioSessionState e)
        {
            switch (e)
            {
                case AudioSessionState.AudioSessionStateExpired:
                    this.Dispose();
                    break;
                case AudioSessionState.AudioSessionStateInactive:
                    this.Dispose();
                    break;
                default:
                    break;
            }
        }

        public void OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason)
        {
            if (SessionDisconnnected != null)
                SessionDisconnnected(this, null);
        }
    }
}
