using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Collections.Generic;

namespace AudioMixer
{
    public sealed class AudioManager
    {
        private static readonly Lazy<AudioManager> instance = new Lazy<AudioManager>(() => new AudioManager());
        private MMDevice device;

        public List<AudioSession> audioSessions = new List<AudioSession>();

        public static AudioManager Instance
        {
            get
            {
                return instance.Value;
            }
        }

        private AudioManager()
        {
            var deviceEnumerator = new MMDeviceEnumerator();
            device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

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
                        var audioSession = new AudioSession(device, session);
                        audioSessions.Add(audioSession);
                    }
                }
            }

            device.AudioSessionManager.OnSessionCreated += AddAudioSession;
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

        void AddAudioSession(object sender, IAudioSessionControl newSession)
        {
            var audioSession = new AudioSession(device, new AudioSessionControl(newSession));
            audioSessions.Add(audioSession);

            PluginController.Instance.UpdateActions();
        }
    }
}
