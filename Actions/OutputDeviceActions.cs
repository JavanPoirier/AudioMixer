using BarRaider.SdTools;
using CoreAudio;
using MoreLinq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sentry;
using streamdeck_client_csharp.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Management.Instrumentation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static AudioMixer.Actions.OutputDeviceAction;
using static AudioMixer.Utils;

/**
 * However, there are some situations where the ID might change:
 * 1. Driver updates: If the driver for the device is updated, the system might assign a new ID to the device.
 * 2. Hardware changes: If the hardware configuration of the device changes (for example, if a USB device is plugged into a different port), the system might assign a new ID to the device.
 * 3. Operating system updates: Updates to the operating system or the audio subsystem might change the way IDs are assigned, causing a device to receive a new ID.
 */

namespace AudioMixer.Actions
{
    public class OutputDeviceSetting
    {
        public readonly string id;
        public readonly string name;
        public readonly string displayName;
        public string icon { get; set; }
        public OutputDeviceType type;
        public OutputDeviceSetting(List<MMDevice> devices, MMDevice device, OutputDeviceType type = OutputDeviceType.Other)
        {
            lock (device)
            {
                id = device.ID;
                name = device.DeviceFriendlyName;

                Regex regex = new Regex(@"\{.*\}\.\{.*(.{6})\}");
                Match match = regex.Match(device.ID);
                if (match.Success)
                {
                    string lastSixChars = match.Groups[1].Value;
                    displayName = AudioManager.ParseDeviceName(name) + $" [{lastSixChars}]";
                } else
                {
                    displayName = AudioManager.ParseDeviceName(name);
                }

                /*if (devices.Count(_device => _device.DeviceFriendlyName == device.DeviceFriendlyName) > 1)
                {
                    var displayNameWithID = displayName + " " + $"[{device.ID.Slice(0, 6)}]";

                    // Back fill any previously added device names. A setting may have been created without the ID if a duplicate was added later.
                    PluginController.Instance.globalSettings.BlacklistedOutputDevices.ForEach(device =>
                    {
                        device.displayName = displayNameWithID
                    })
                }*/

                this.type = type;
            }
        }

        [JsonConstructor]
        public OutputDeviceSetting(string id, string icon, string name, OutputDeviceType type)
        {
            this.id = id;
            this.icon = icon;
            this.name = name;
            this.displayName = AudioManager.ParseDeviceName(name);
            this.type = type;
        }
    }

    public static class OutputDeviceActions
    {
        private static ConcurrentQueue<OutputDeviceAction> outputDeviceActionQueue = new ConcurrentQueue<OutputDeviceAction>();
        private static readonly object actionsLock = new object();

        public static List<OutputDeviceAction> outputDeviceActions = new List<OutputDeviceAction>();

        // TODO: Handle that if there are more than one Output Device Actions, they become static.

        public static void Add(OutputDeviceAction outputDeviceAction)
        {
            if (outputDeviceActions.Contains(outputDeviceAction)) return;
            lock (actionsLock)
            {
                outputDeviceActions.Add(outputDeviceAction);
            }

            Reload();
       }

        public static void Remove(OutputDeviceAction outputDeviceAction)
        {
            lock (actionsLock)
            {
                outputDeviceActions.Remove(outputDeviceAction);
            }

            Reload();
        }

        public static void AddToQueue(OutputDeviceAction outputDeviceAction)
        {
            lock (actionsLock)
            {
                outputDeviceActionQueue.Enqueue(outputDeviceAction);

                OutputDeviceAction enqueuedAction;
                while (outputDeviceActionQueue.TryDequeue(out enqueuedAction)) enqueuedAction.SetDevice();
            }
        }

        public static void Reload(bool releaseDevices = true)
        {
            lock (actionsLock)
            {
                outputDeviceActionQueue = new ConcurrentQueue<OutputDeviceAction>(outputDeviceActions);

                if (releaseDevices) outputDeviceActions.ForEach(action => action.ReleaseDevice());

                OutputDeviceAction enqueuedAction;
                while (outputDeviceActionQueue.TryDequeue(out enqueuedAction)) enqueuedAction.SetDevice();
            }
        }
    }
}
