using AudioMixer.Actions;
using CoreAudio;
using MoreLinq;
using Sentry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static AudioMixer.AudioManager;

namespace AudioMixer
{
    public static class AudioManager
    {
        private static MMDeviceEnumerator deviceEnumerator;
        private static MMNotificationClient notificationClient;

        static MMDevice prevDefaultMediaOutputDevice;
        public static MMDevice DefaultMediaOutputDevice
        {
            get => deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }

        static MMDevice prevDefaultCommsOutputDevice;
        public static MMDevice DefaultCommsOutputDevice 
        { 
            get => deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications); 
        }

        static MMDevice prevDefaultInputOutputDevice;
        public static MMDevice DefaultInputDevice
        {
            get => deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
        }

        public static MMDeviceCollection OutputDevices
        {
            get => deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        }
        
        public static MMDeviceCollection InputDevices
        {
            get => deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.MaskAll);
        }

        public static List<AudioSession> audioSessions = new List<AudioSession>();

        public static void Init()
        {
            deviceEnumerator = new MMDeviceEnumerator(Guid.NewGuid());
            notificationClient = new MMNotificationClient(deviceEnumerator);

            notificationClient.DefaultDeviceChanged += DefaultDeviceChanged;
            notificationClient.DeviceStateChanged += DeviceStateChanged;
            notificationClient.DeviceAdded += DeviceAdded;
            notificationClient.DeviceRemoved += DeviceRemoved;
            notificationClient.DevicePropertyChanged += DevicePropertyChanged;

            prevDefaultMediaOutputDevice = DefaultMediaOutputDevice;
            prevDefaultCommsOutputDevice = DefaultCommsOutputDevice;
            prevDefaultInputOutputDevice = DefaultInputDevice;

            CreateAudioSessions();
        }

        private static void CreateAudioSessions()
        {
            DefaultMediaOutputDevice.AudioSessionManager2.Sessions.Where(session => !session.IsSystemSoundsSession && Utils.ProcessExists(session.ProcessID)).ForEach(session => AddAudioSession(session));
        }

        public static MMDevice GetDevice(string id)
        {
            return id != null ? deviceEnumerator.GetDevice(id) : null;
        }

        public static void DeleteAudioSessions()
        {
            for (int i = audioSessions.Count - 1; i >= 0; i--)
            {
                DeleteAudioSession(audioSessions[i]);
            }
        }

        public static void DeleteAudioSession(AudioSession session)
        {
            // Remove any action references to this session
            ApplicationActions.applicationActions.Where(action => action.ActionAudioSessions.Contains(session)).ForEach(action => action.ReleaseAudioSession(session));

            audioSessions.Remove(session);
            session.Dispose();
        }

        public static void SetDefaultDevice(MMDevice device)
        {
            deviceEnumerator.SetDefaultAudioEndpoint(device);
        }

        public static Role GetDeviceRole(MMDevice device)
        {
            if (device.ID == deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID)
            {
                return Role.Multimedia;
            }
            else if (device.ID == deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications).ID)
            {
                return Role.Communications;
            }
       
            return Role.Console;
        }

        public static int GetDefaultMediaOutputDeviceIndex(MMDevice device)
        {
            if (device == null) return -1;

            // Find the current device in the list
            int? index = OutputDevices.Select((_device, i) => new { ID = _device.ID, Index = i })
                .FirstOrDefault(tuple => tuple.ID == device.ID)?.Index;

            return index ?? -1;
        }

        public static MMDevice GetNextMediaOutputDevice(MMDevice device)
        {
            if (OutputDevices.Count() < 2) return device;

            // Find the current device in the list
            int? index = OutputDevices.Select((_device, i) => new { _device.ID, Index = i })
               .FirstOrDefault(tuple => tuple.ID == device.ID)?.Index;

            if (GetDefaultMediaOutputDeviceIndex(device) > -1)
            {
                var nextDevice = OutputDevices.ElementAtOrDefault((index.Value + 1) % OutputDevices.Count());

                // Cannot call SetDefaultAudioEndpoint on a device that is not active.
                if (nextDevice.State == DeviceState.Active) deviceEnumerator.SetDefaultAudioEndpoint(nextDevice);

                return nextDevice;
            }
            else
            {
                // The device was not found in the collection
                return null;
            }
        }

        public static MMDevice GetDefaultCommDevice()
        {
            return deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications);
        }

        private static void OnSessionCreated(object sender, CoreAudio.Interfaces.IAudioSessionControl2 newSession)
        {
            if (newSession == null) return;
            AddAudioSession(newSession as AudioSessionControl2);
        }

        private static void DeviceStateChanged(object sender, DeviceStateChangedEventArgs e)
        {
            SentrySdk.AddBreadcrumb(
                message: "Device state changed",
                category: "AudioManager",
                level: BreadcrumbLevel.Info,
                data: new Dictionary<string, string> {
                    { "deviceState", e.DeviceState.ToString() },
                }
            );

            switch (e.DeviceState)
            {
                case DeviceState.Active:
                    break;
                case DeviceState.Disabled:
                    break;
                case DeviceState.NotPresent:
                    break;
                case DeviceState.Unplugged:
                    break;
            }
        }

        private static void DefaultDeviceChanged(object sender, DefaultDeviceChangedEventArgs e)
        {
            SentrySdk.AddBreadcrumb(
                message: "Default device changed",
                category: "AudioManager",
                level: BreadcrumbLevel.Info,
                data: new Dictionary<string, string> {
                    { "role", e.Role.ToString() },
                }
            );

            MMDevice device;
            e.TryGetDevice(out device);
            HandleDefaultDeviceChanged(device);
        }

        private static void HandleDefaultDeviceChanged(MMDevice device)
        {
            if (device != null)
            {
                Role role = GetDeviceRole(device);

                if (device.DataFlow == DataFlow.Render)
                {
                    if (role == Role.Multimedia)
                    {
                        if (prevDefaultMediaOutputDevice?.ID == device.ID) return;
                        // Delete the audio sessions associated with the previous device.
                        prevDefaultMediaOutputDevice.AudioSessionManager2.OnSessionCreated -= OnSessionCreated;
                        DeleteAudioSessions();

                        // Release the output device action from the previous device.
                        OutputDeviceActions.outputDeviceActions.Find(action => action.deviceId == prevDefaultMediaOutputDevice.ID).ReleaseDevice(prevDefaultMediaOutputDevice);
                        OutputDeviceActions.Reload();

                        // Create new audio sessions
                        if (DefaultMediaOutputDevice.ID == device.ID)
                        {
                            prevDefaultMediaOutputDevice = device;

                            CreateAudioSessions();
                            DefaultMediaOutputDevice.AudioSessionManager2.OnSessionCreated += OnSessionCreated;
                            ApplicationActions.Reload();
                        }
                    }
                    else
                    {
                        if (prevDefaultCommsOutputDevice?.ID == device.ID) return;
                    }
                }
                if (device.DataFlow == DataFlow.Capture)
                {
                    if (prevDefaultInputOutputDevice?.ID == device.ID) return;
                }
            }
        }

        private static void DeviceRemoved(object sender, DeviceNotificationEventArgs e)
        {
            SentrySdk.AddBreadcrumb(
                message: "Device removed",
                category: "AudioManager",
                level: BreadcrumbLevel.Info
            );

            // NOTE: DefaultDeviceChanged will be fired in the event the device removed was default...?

            // Update device selection list
            OutputDeviceActions.Reload();
        }

        private static void DeviceAdded(object sender, DeviceNotificationEventArgs e)
        {
            SentrySdk.AddBreadcrumb(
                message: "Device added",
                category: "AudioManager",
                level: BreadcrumbLevel.Info
            );

            // Update device selection list
            OutputDeviceActions.Reload();
        }

        private static void DevicePropertyChanged(object sender, DevicePropertyChangedEventArgs e)
        {
            SentrySdk.AddBreadcrumb(
                message: "Device property changed",
                category: "AudioManager",
                level: BreadcrumbLevel.Info
            );
        }

        public static void Dispose()
        {
            notificationClient.DefaultDeviceChanged -= DefaultDeviceChanged;
            notificationClient.DeviceStateChanged -= DeviceStateChanged;
            notificationClient.DeviceAdded -= DeviceAdded;
            notificationClient.DeviceRemoved -= DeviceRemoved;
        }

        public static void AddAudioSession(AudioSessionControl2 session)
        {
            if (audioSessions.Any(s => s.sessionControl == session)) return;
            var audioSession = new AudioSession(session);
            audioSessions.Add(audioSession);
        }

        public static IEnumerator<MMDevice> GetAudioDevices()
        {
            return deviceEnumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.MaskAll).GetEnumerator();
        }

        public static string ParseDeviceName(string deviceName)
        {
            // Remove text with parenthesis, inclusive.
            return Regex.Replace(deviceName, @"\s*\([^)]*\)", string.Empty);
        }
    }
}
