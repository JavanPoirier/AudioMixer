using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using System.Drawing;
using NAudio.CoreAudioApi.Interfaces;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using BarRaider.SdTools;

namespace AudioMixer
{
    public class AudioSession : IAudioSessionEventsHandler
    {
        private MMDevice device;
        public readonly AudioSessionControl session;

        public event EventHandler SessionDisconnnected;
        public event EventHandler VolumeChanged;

        public readonly string processName;
        public Bitmap processIcon { get; private set; }

        public AudioSession(MMDevice device, AudioSessionControl session)
        {
            this.device = device;
            this.session = session;

            Process process = Process.GetProcessById((int)session.GetProcessID);
            processName = process.ProcessName;

            try
            {
                processIcon = Icon.ExtractAssociatedIcon(process.MainModule.FileName).ToBitmap();
                session.RegisterEventClient(this);
            }
            catch (Exception e)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, "This application must be run as an administrator.");
            }
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
            if (e == AudioSessionState.AudioSessionStateExpired)
            {
                if (SessionDisconnnected != null)
                    SessionDisconnnected(this, null);
            }
        }

        public void OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason)
        {
            if (SessionDisconnnected != null)
                SessionDisconnnected(this, null);
        }
    }
}
