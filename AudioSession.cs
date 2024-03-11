using System;
using System.Diagnostics;
using System.Drawing;
using BarRaider.SdTools;
using Newtonsoft.Json;
using AudioMixer.Actions;
using CoreAudio;
using CoreAudio.Interfaces;
using Sentry;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Collections.Specialized;
using System.Linq;

namespace AudioMixer
{
    public class AudioSessionSetting
    {
        public readonly string processName;
        public string processIcon { get; private set; }
        public AudioSessionSetting(AudioSession audioSession)
        {
            lock(audioSession)
            {
                processName = audioSession.processName;
                processIcon = Utils.BitmapToBase64(audioSession.processIcon);
            }
        }

        [JsonConstructor]
        public AudioSessionSetting(string processIcon, string processName)
        {
            this.processIcon = processIcon;
            this.processName = processName;
        }
    }

    public class AudioSession : IDisposable
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

        public readonly int processId;
        public readonly string processName;
        public readonly AudioSessionControl2 sessionControl;
        public Bitmap processIcon { get; private set; } = new Bitmap(Utils.CreateDefaultAppKey());

        public event EventHandler OnSessionDisconnected;
        public event EventHandler<VolumeChangedEventArgs> OnVolumeChanged;

        public float MasterVolume
        {
            get
            {
                return sessionControl.SimpleAudioVolume.MasterVolume;
            }
            set
            {
                sessionControl.SimpleAudioVolume.MasterVolume = value;
                OnVolumeChanged?.Invoke(this, new VolumeChangedEventArgs(value, Mute));
            }
        }

        public bool Mute
        {
            get
            {
                return sessionControl.SimpleAudioVolume.Mute;
            }
            set
            {
                sessionControl.SimpleAudioVolume.Mute = value;
                OnVolumeChanged?.Invoke(this, new VolumeChangedEventArgs(MasterVolume, value));
            }
        }

        public AudioSession(AudioSessionControl2 sessionControl)
        {
            this.sessionControl = sessionControl;

            try {
                SentrySdk.AddBreadcrumb(
                    message: "Creating audio session",
                    category: "AudioSession",
                    level: BreadcrumbLevel.Info,
                    data: new Dictionary<string, string> {
                        { "sessionDisplayName", sessionControl.DisplayName },
                        { "sessionIconPath", sessionControl.IconPath },
                        { "processName", processName },
                    }
                );

                Process process = Process.GetProcessById((int)sessionControl.ProcessID);

                processId = process.Id;

                // NOTE: Don't use MainWindowTitle as some applications dynamically update it. Ex: Spotify changes it to the playing song.
                processName = process.ProcessName;

                // NOTE: The following causing Win32Expections with some processes. See Utils.GetProcessName.
                // processIcon = Icon.ExtractAssociatedIcon(process.MainModule.FileName).ToBitmap();

                try
                {
                    var processFilePath = Utils.GetProcessName(process.Id);

                    SentrySdk.AddBreadcrumb(
                        message: "Retrieved process file path",
                        category: "AudioSession",
                        level: BreadcrumbLevel.Info,
                        data: new Dictionary<string, string> {
                            { "sessionDisplayName", sessionControl.DisplayName },
                            { "sessionIconPath", sessionControl.IconPath },
                            { "processName", processName },
                            { "processFilePath", processFilePath }
                        }
                    );

                    if ( processFilePath != null )
                    {
                        processIcon = Icon.ExtractAssociatedIcon(processFilePath).ToBitmap();
                    }
                } catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, ex.ToString());
                    SentrySdk.CaptureException(ex, scope => { scope.TransactionName = "AudioSession"; });
                }

                // TODO:
                //processIcon = Icon.ExtractAssociatedIcon(session.IconPath).ToBitmap(); "%windir%\\system32\\mmres.dll,-3030"
                //Environment.ExpandEnvironmentVariables("%windir%\\system32\\mmres.dll");

                var data = new Dictionary<string, string> {
                        { "sessionDisplayName", sessionControl.DisplayName },
                        { "sessionIconPath", sessionControl.IconPath },
                        { "sessionIsSystemSoundsSession", sessionControl.IsSystemSoundsSession.ToString() },
                        { "processName", processName }
                };

                if (processIcon == null)
                {
                    SentrySdk.AddBreadcrumb(
                        message: "Unable to find process icon",
                        category: "AudioSession",
                        level: BreadcrumbLevel.Info,
                        data: data
                    );

                    // TODO: Only in debug?
                    var sentryEvent = new SentryEvent();
                    sentryEvent.Message = "Unable to find process icon";
                    sentryEvent.SetExtras(new Dictionary<string, object>(data.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)));
                    SentrySdk.CaptureEvent(sentryEvent);

                    processIcon = new Bitmap(Utils.CreateDefaultAppKey());
                }

                sessionControl.OnSimpleVolumeChanged += HandleVolumeChanged;
                sessionControl.OnStateChanged += HandleStateChanged;
                sessionControl.OnSessionDisconnected += HandleSessionDisconnected;

                Logger.Instance.LogMessage(TracingLevel.INFO, JObject.FromObject(data).ToString());
                SentrySdk.AddBreadcrumb(
                       message: "Created session",
                       category: "AudioSession",
                       level: BreadcrumbLevel.Info,
                       data: data
                   );
            } catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, ex.ToString());
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = "AudioSession"; });
            }
        }

        public void Dispose()
        {
            if (sessionControl == null) return;
            sessionControl.OnSimpleVolumeChanged -= HandleVolumeChanged;
            sessionControl.OnStateChanged -= HandleStateChanged;
            sessionControl.OnSessionDisconnected -= HandleSessionDisconnected;
        }

        private void HandleVolumeChanged(object sender, float newVolume, bool newMute)
        {
            if (OnVolumeChanged != null) OnVolumeChanged(this, new VolumeChangedEventArgs(newVolume, newMute));   
        }

        public void HandleStateChanged(object sender, AudioSessionState newState)
        {
            switch (newState)
            {
                case AudioSessionState.AudioSessionStateExpired:
                    // Occurs when application is closed.
                    /*this.Dispose();*/
                    AudioManager.DeleteAudioSession(this);
                    break;
                case AudioSessionState.AudioSessionStateInactive:
                    // Occurs when application has released audio.
                    // this.Dispose();
                    break;
                default:
                    break;
            }
        }

        public void HandleSessionDisconnected(object sender, AudioSessionDisconnectReason disconnectReason)
        {
            if (OnSessionDisconnected != null) OnSessionDisconnected(this, null);
        }
    }
}
