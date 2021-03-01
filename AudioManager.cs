using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Collections.Generic;

namespace AudioMixer
{
    public sealed class AudioManager
    {
        private static readonly AudioManager instance = new AudioManager();

        private MMDevice device;
        public List<AudioSession> audioSessions = new List<AudioSession>();

        static AudioManager()
        {
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
                audioSessions = new List<AudioSession>(sessions.Count);
                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    if (!session.IsSystemSoundsSession && ProcessExists(session.GetProcessID))
                    {
                        AddAudioSession(null, session as IAudioSessionControl);
                    }
                }
            }

            device.AudioSessionManager.OnSessionCreated += AddAudioSession;
        }

        public static AudioManager Instance
        {
            get
            {
                return instance;
            }
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
            var audioSession = new AudioSession(ref device, (AudioSessionControl)newSession);
            audioSessions.Add(audioSession);
        }
    }
}
