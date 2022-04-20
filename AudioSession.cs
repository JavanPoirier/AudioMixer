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
        public readonly string processName;
        public string processIcon { get; private set; }

        public AudioSessionSetting(AudioSession audioSession)
        {
            processName = audioSession.processName;
            processIcon = Utils.BitmapToBase64(audioSession.processIcon);
        }
    }

    public class AudioSession : IAudioSessionEventsHandler, IDisposable
    {
        public class VolumeChangedEventArgs
        {
            public float volume;
            public bool isMuted;

            public VolumeChangedEventArgs(float volume, bool isMuted)
            {
                this.volume = volume;
                this.isMuted = isMuted;
            }
        }

        private PluginController pluginController;
        private MMDevice device;

        public string actionId;
        public readonly int processId;
        public readonly string processName;
        public readonly AudioSessionControl session;
        public Bitmap processIcon { get; private set; }

        public event EventHandler SessionDisconnnected;
        public event EventHandler<VolumeChangedEventArgs> VolumeChanged;

        public AudioSession(PluginController pluginController, MMDevice device, AudioSessionControl session)
        {
            this.pluginController = pluginController;
            this.device = device;
            this.session = session;

            try {
                Process process = Process.GetProcessById((int)session.GetProcessID);

                processId = process.Id;
                processName = process.ProcessName;
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

            var applicationActions = this.pluginController.applicationActions.FindAll(actions => actions.processName == this.processName);
            applicationActions.ForEach(action => action.SetAudioSession());
        }

        public void OnVolumeChanged(float volume, bool isMuted)
        {
            if (VolumeChanged != null)
            {
                var e = new VolumeChangedEventArgs(volume, isMuted);
                VolumeChanged(this, e);
            }
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
                    // Occurs when application is closed.
                    this.Dispose();
                    break;
                case AudioSessionState.AudioSessionStateInactive:
                    // Occurs when application has released audio.
                    // this.Dispose();
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
