using System;
using System.Diagnostics;
using NAudio.CoreAudioApi;
using System.Drawing;
using NAudio.CoreAudioApi.Interfaces;
using BarRaider.SdTools;
using Newtonsoft.Json;
using Sentry;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

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

        public readonly int processId;
        public readonly string processName;
        public readonly AudioSessionControl session;
        public Bitmap processIcon { get; private set; } = new Bitmap(Utils.CreateDefaultAppKey());

        public event EventHandler SessionDisconnnected;
        public event EventHandler<VolumeChangedEventArgs> VolumeChanged;

        public AudioSession(PluginController pluginController, MMDevice device, AudioSessionControl session)
        {
            this.pluginController = pluginController;
            this.device = device;
            this.session = session;

            try {
                SentrySdk.AddBreadcrumb(
                    message: "Creating audio session",
                    category: "AudioSession",
                    level: BreadcrumbLevel.Info,
                    data: new Dictionary<string, string> {
                        { "sessionDisplayName", session.DisplayName },
                        { "sessionIconPath", session.IconPath },
                        { "processName", processName },
                    }
                );

                Process process = Process.GetProcessById((int)session.GetProcessID);

                processId = process.Id;

                // NOTE: Don't use MainWindowTitle as some applciations dynamically update it. Ex: Spotify changes it to the playing song.
                processName = process.ProcessName;

                // NOTE: The following causing Win32Expections with some processes. See Utils.GetProcessName for SO resolution.
                // processIcon = Icon.ExtractAssociatedIcon(process.MainModule.FileName).ToBitmap();

                try
                {
                    var processFilePath = Utils.GetProcessName(process.Id);

                    SentrySdk.AddBreadcrumb(
                        message: "Retrieved process file path",
                        category: "AudioSession",
                        level: BreadcrumbLevel.Info,
                        data: new Dictionary<string, string> {
                            { "sessionDisplayName", session.DisplayName },
                            { "sessionIconPath", session.IconPath },
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
                    var name = ex.GetType().Name;
                    Logger.Instance.LogMessage(TracingLevel.ERROR, name);
                    Logger.Instance.LogMessage(TracingLevel.ERROR, ex.Message);
                    SentrySdk.CaptureException(ex, scope => { scope.TransactionName = "AudioSession"; });
                }

                // TODO:
                //processIcon = Icon.ExtractAssociatedIcon(session.IconPath).ToBitmap(); "%windir%\\system32\\mmres.dll,-3030"
                //Environment.ExpandEnvironmentVariables("%windir%\\system32\\mmres.dll");

                if (processIcon == null)
                {
                    SentrySdk.AddBreadcrumb(
                        message: "Unable to find process icon",
                        category: "AudioSession",
                        level: BreadcrumbLevel.Info,
                        data: new Dictionary<string, string> {
                            { "sessionDisplayName", session.DisplayName },
                            { "sessionIconPath", session.IconPath },
                            { "processName", processName }
                        }
                    );

                    // TODO: Only in debug?
                    var sentryEvent = new SentryEvent();
                    sentryEvent.Message = "Unable to find process icon";
                    sentryEvent.SetExtras(new Dictionary<string, object> {
                            { "sessionDisplayName", session.DisplayName },
                            { "sessionIconPath", session.IconPath },
                            { "sessionIsSystemSoundsSession", session.IsSystemSoundsSession },
                            { "processName", processName }
                        });
                    SentrySdk.CaptureEvent(sentryEvent);

                    processIcon = new Bitmap(Utils.CreateDefaultAppKey());
                }

                session.RegisterEventClient(this);

                Logger.Instance.LogMessage(TracingLevel.INFO, JObject.FromObject(new Dictionary<string, string> {
                        { "sessionDisplayName", session.DisplayName },
                        { "sessionIconPath", session.IconPath },
                        { "sessionIsSystemSoundsSession", session.IsSystemSoundsSession.ToString() },
                        { "processName", processName }
                    }).ToString());

                SentrySdk.AddBreadcrumb(
                       message: "Created session",
                       category: "AudioSession",
                       level: BreadcrumbLevel.Info,
                       data: new Dictionary<string, string> {
                            { "sessionDisplayName", session.DisplayName },
                            { "sessionIconPath", session.IconPath },
                            { "processName", processName }
                       }
                   );
            } catch (Exception ex)
            {
                var name = ex.GetType().Name;
                Logger.Instance.LogMessage(TracingLevel.ERROR, name);
                Logger.Instance.LogMessage(TracingLevel.ERROR, ex.Message);
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = "AudioSession"; });
            }
        }

        public void Dispose()
        {
            var applicationAction = this.pluginController.applicationActions.Find(actions => actions.processName == this.processName);
            if (applicationAction != null) applicationAction.ReleaseAudioSession();

            this.pluginController.audioManager.audioSessions.Remove(this);
            pluginController.UpdateActions();
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
