using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Collections.Generic;

namespace AudioMixer
{
    public class AudioManager 
    {
        private PluginController pluginController;
        private MMDevice device;

        public List<AudioSession> audioSessions = new List<AudioSession>();

        public AudioManager(PluginController pluginController)
        {
            this.pluginController = pluginController;

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
                        AddAudioSession(session);
                    }
                }
            }

            device.AudioSessionManager.OnSessionCreated += AddAudioSession;
        }

        private void Dispose()
        {
            device.AudioSessionManager.OnSessionCreated -= AddAudioSession;
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
            var test = session.IsSystemSoundsSession;
            // TODO: Still add them to session list, just add a blacklist property.
            if (!pluginController.blacklist.Contains(session.GetSessionIdentifier))
            {
                var audioSession = new AudioSession(pluginController, device, session);
                audioSessions.Add(audioSession);

                // Check if session has a static application action.
                var applicationAction = pluginController.applicationActions.Find(action => action.settings?.StaticApplication?.name == audioSession.name);
                if (applicationAction != null)
                {
                    audioSession.actionId = applicationAction.actionId;
                    applicationAction.SetAudioSession();
                } else
                {
                    pluginController.UpdateActions();
                }
            }
        }

        void AddAudioSession(object sender, IAudioSessionControl audioSessionControl)
        {
            // TODO: Still add them to session list, just add a blacklist property.
            var session = new AudioSessionControl(audioSessionControl);
            this.AddAudioSession(session);
        }
    }
}
