using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Sentry;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AudioMixer
{
    public class AudioManager : IMMNotificationClient
    {
        private PluginController pluginController;
        private MMDeviceEnumerator deviceEnum = new MMDeviceEnumerator();
        private MMDevice device;

        public List<AudioSession> audioSessions = new List<AudioSession>();

        public AudioManager(PluginController pluginController)
        {
            this.pluginController = pluginController;
            
            SetAudioSessions();

            deviceEnum.RegisterEndpointNotificationCallback(this);
        }

        public void SetAudioSessions() {
            try
            {
                if (device != null)
                {
                    device.AudioSessionManager.OnSessionCreated -= AddAudioSession;
                }

                audioSessions.Clear();

                device = deviceEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var sessions = device.AudioSessionManager.Sessions;
                if (sessions == null)
                {

                }
                else
                {
                    for (int i = 0; i < sessions.Count; i++)
                    {
                        var session = sessions[i];
                        if (!session.IsSystemSoundsSession && ProcessExists(session.GetProcessID))
                        {
                            AddAudioSession(session);
                        }
                    }
                }

                device.AudioSessionManager.OnSessionCreated += AddAudioSession;
            } catch (Exception ex)
            {
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = "AudioManager"; });
            }
        }

        private void Dispose()
        {
            deviceEnum.UnregisterEndpointNotificationCallback(this);
            device.AudioSessionManager.OnSessionCreated -= AddAudioSession;
            audioSessions.Clear();
        }

        bool ProcessExists(uint processId)
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById((int)processId);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        void AddAudioSession(AudioSessionControl session)
        {
            var audioSession = new AudioSession(pluginController, device, session);
            audioSessions.Add(audioSession);
            pluginController.UpdateActions();
        }

        void AddAudioSession(object sender, IAudioSessionControl audioSessionControl)
        {
            var session = new AudioSessionControl(audioSessionControl);
            AddAudioSession(session);
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }

        public void OnDeviceAdded(string pwstrDeviceId)
        {
            SentrySdk.AddBreadcrumb(
                message: "Device added",
                category: "AudioManager",
                level: BreadcrumbLevel.Info
            );
        }

        public void OnDeviceRemoved(string deviceId)
        {
            SentrySdk.AddBreadcrumb(
                message: "Device removed",
                category: "AudioManager",
                level: BreadcrumbLevel.Info
            );
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            SentrySdk.AddBreadcrumb(
                message: "Default device changed",
                category: "AudioManager",
                level: BreadcrumbLevel.Info
            );

            SetAudioSessions();
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}
