using AudioSwitcher.AudioApi.Session;
using BarRaider.SdTools;
using CoreAudio;
using MoreLinq.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sentry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using static AudioMixer.Actions.OutputDeviceAction;
using static AudioMixer.ApplicationAction;
using static AudioMixer.Utils;

namespace AudioMixer.Actions
{
    [PluginActionId("com.javanpoirier.audiomixer.outputdevice")]
    public class OutputDeviceAction : BaseAction<OutputDeviceSettings>
    {
        public class OutputDeviceSettings : GlobalSettings
        {
            public OutputDeviceSettings() : base()
            {
                StaticOutputDeviceName = null;
                StaticOutputDevice = null;
                BlacklistedOutputDeviceName = null;
                WhitelistedOutputDeviceName = null;
            }

            public override GlobalSettings CreateInstance(string UUID)
            {
                return new OutputDeviceSettings { UUID = UUID };
            }

            [JsonProperty(PropertyName = "staticOutputDeviceName")]
            public string StaticOutputDeviceName { get; set; }

            [JsonProperty(PropertyName = "staticOutputDevice")]
            public OutputDeviceSetting StaticOutputDevice { get; set; }

            [JsonProperty(PropertyName = "blacklistedOutputDeviceName")]
            public string BlacklistedOutputDeviceName { get; set; }

            [JsonProperty(PropertyName = "whitelistedOutputDeviceName")]
            public string WhitelistedOutputDeviceName { get; set; }
        }

        private Utils.ControlType controlType = Utils.ControlType.OutputDevice;
        public string deviceId { get; private set; }
        public string displayName { get => AudioManager.GetDevice(deviceId)?.DeviceFriendlyName; }

        private MMDevice Device { get => AudioManager.GetDevice(deviceId);}

        private int DeviceIndex { get => AudioManager.GetDefaultMediaOutputDeviceIndex(Device); }

        private bool IsCyclableAction { get => OutputDeviceActions.outputDeviceActions.Where(action => action.actionSettings.StaticOutputDevice == null).Count() == 1; }

    public OutputDeviceAction(SDConnection connection, InitialPayload payload) : base(connection, payload, ActionType.OUTPUTDEVICE, "OutputDevice") { }

        protected override void InitActionCore()
        {
            // TODO: Remove any whitelisted devices that no longer exist
        }

        protected override void SetKey()
        {
            // If not currently in the actions list, add it.
            if (!OutputDeviceActions.outputDeviceActions.Any(action => action == this))
            {
                OutputDeviceActions.Add(this);
            }

        }

        protected override void HandleKeyHeld(object timerSender, ElapsedEventArgs elapsedEvent)
        {
        }

        protected override void HandleKeyReleased(object sender, KeyReleasedEventArgs e)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Released");
            SentrySdk.AddBreadcrumb(
                message: "Key released",
                category: actionName,
                level: BreadcrumbLevel.Info
            );
            
            if (IsCyclableAction)
            {
                var nextDevice = AudioManager.GetNextMediaOutputDevice(Device);
                ReleaseDevice();

                deviceId = nextDevice.ID;
                AudioManager.SetDefaultDevice(Device);
            }
            else if (AudioManager.DefaultMediaOutputDevice.ID != deviceId)
            {
                AudioManager.SetDefaultDevice(Device);
            }

            ApplicationActions.SelectedAction = null;
        }

        public void SetDevice()
        {
            SentrySdk.AddBreadcrumb(
               message: "Setting device",
               category: actionName,
               level: BreadcrumbLevel.Info
           );

            ReleaseDevice();

            // If audio session is static...
            if (actionSettings.StaticOutputDevice != null)
            {
                // Before self assigning, find the application action that has the session.
                var existingOutputDeviceAction = OutputDeviceActions.outputDeviceActions.Find(action =>
                   action != this && action.Device.ID == actionSettings.StaticOutputDevice.id
                );

                // Self assign before re-assigning the last action.
                deviceId = AudioManager.OutputDevices.FirstOrDefault(device => device.ID == actionSettings.StaticOutputDevice.id).ID;

                // If an action has the device we want, clear it.
                if (existingOutputDeviceAction != null) OutputDeviceActions.AddToQueue(existingOutputDeviceAction);

                
                // If output device is not available, use gray-scaled last known icon.
                if (Device == null)
                {
                  /*  var lastKnownIcon = Utils.CreateIconImage(Utils.Base64ToBitma(actionSettings.StaticOutputDevice.icon));*/

                    SentrySdk.AddBreadcrumb(
                        message: "Set unavailable static device",
                        category: actionName,
                        level: BreadcrumbLevel.Info,
                        data: new Dictionary<string, string> {
                    { "deviceId", $"{deviceId}" },
                    { "displayName", $"{displayName}" },
                    { "outputDeviceActions", $"{OutputDeviceActions.outputDeviceActions.Count()}" },
                       }
                    );
                } else
                {
                   SentrySdk.AddBreadcrumb(
                      message: "Reserved static device",
                      category: actionName,
                      level: BreadcrumbLevel.Info,
                      data: new Dictionary<string, string> {
                                    { "deviceId", $"{deviceId}" },
                                    { "displayName", $"{displayName}" },
                                    { "outputDeviceActions", $"{OutputDeviceActions.outputDeviceActions.Count()}" },
                      }
                   );
                  }
            } else
            {
                try
                {
                    var availableOutputDevices = AudioManager.OutputDevices.Where(device =>
                    {
                        // Ensure it is not a blacklisted application.
                        var blacklistedOutputDevice = actionSettings.BlacklistedOutputDevices.Find(_device => _device.id == device.ID);
                        if (blacklistedOutputDevice != null) return false;

                        // Ensure no application action has the application set, both statically and dynamically.
                        var existingOutputDeviceAction = OutputDeviceActions.outputDeviceActions.Find(action =>
                            device.ID == action.actionSettings.StaticOutputDevice?.id || action.deviceId == device.ID
                        );
                        if (existingOutputDeviceAction != null) return false;

                        return true;
                    });


                    if (availableOutputDevices.Count() > 0)
                    {
                        // If there is only 1 non-static action, make this action cycle through all devices.
                        if (IsCyclableAction)
                        {
                            SentrySdk.AddBreadcrumb(
                                   message: "Cyclable action",
                                   category: actionName,
                                   level: BreadcrumbLevel.Info
                                );

                            // Cyclable actions should always be released to prevent multiple event handlers.
                            ReleaseDevice();

                            deviceId = AudioManager.DefaultMediaOutputDevice.ID;
                        }
                        else
                        {
                            // Get the next unassigned device session.
                            deviceId = availableOutputDevices.FirstOrDefault().ID;
                        }

                        SentrySdk.AddBreadcrumb(
                           message: "Set device",
                           category: actionName,
                           level: BreadcrumbLevel.Info,
                           data: new Dictionary<string, string> {
                                { "displayName", $"{displayName}" },
                                { "outputDeviceActions", $"{OutputDeviceActions.outputDeviceActions.Count()}" },
                           }
                       );
                    }
                    else
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, "No device available.");
                        SentrySdk.AddBreadcrumb(
                            message: "No device available",
                            category: actionName,
                            level: BreadcrumbLevel.Info
                        );
                    }
                } 
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, ex.Message);
                    SentrySdk.CaptureException(ex, scope => { scope.TransactionName = actionName; });

                    OutputDeviceActions.Reload();
                }

                try
                {
                    if (Device?.AudioEndpointVolume != null) Device.AudioEndpointVolume.OnVolumeNotification += VolumeNotification;
                }
                catch (Exception ex)
                {
                    SentrySdk.AddBreadcrumb(
                         message: "Unable to access AudioEndpointVolume",
                         category: actionName,
                         level: BreadcrumbLevel.Error,
                         data: new Dictionary<string, string> {
                        { "error", ex.Message.ToString() }
                     }
                     );
                }

                SetImage();
            }
        }

        public void SetImage()
        {
            try
            {
                if (controlType == Utils.ControlType.OutputDevice)
                {
                    if (Device != null)
                    {
                        // Selected is only shown for non-cyclable actions.
                        var selected = IsCyclableAction ? false : Device.Selected;
                        // Only cyclable actions show the device count.
                        var deviceCount = IsCyclableAction ? AudioManager.OutputDevices.Count() : 0;
                        OutputDeviceType? type = actionSettings.StaticOutputDevice?.type;

                        if (type == null)
                        {
                            try
                            {
                                // Attempt to determine the device type
                                // Define the property key for the jack description
                                PropertyStore properties = Device.Properties;
                                PropertyKey PKEY_AudioJackDescription = new PropertyKey(new Guid("2F1A279C-3C0D-4D5B-A31B-01F9C4B1C6FA"), 5);

                                if (properties.Contains(PKEY_AudioJackDescription))
                                {
                                    PropertyStoreProperty jackDescription = properties[PKEY_AudioJackDescription];
                                    // jackDescription now contains the jack description

                                    jackDescription.Value.ToString();
                                };
                            }
                            catch (Exception ex)
                            {
                                SentrySdk.AddBreadcrumb(
                                  message: "Unable to get jack information",
                                  category: actionName,
                                  level: BreadcrumbLevel.Error,
                                  data: new Dictionary<string, string> {
                                { "error", ex.ToString() },
                                     }
                                 );
                            }

                            type = OutputDeviceType.Other;
                        }

                        // Connection.SetImageAsync(Utils.CreateOutputDeviceKey(device, Utils.OutPutDeviceType.Headset, 5, 2), null, true);
                        Connection.SetImageAsync(Utils.CreateOutputDeviceKey(Device, (OutputDeviceType)type, deviceCount, DeviceIndex, selected), null, true);
                    }
                    else
                    {
                        Connection.SetImageAsync(Utils.CreateOutputDeviceKey(actionSettings?.StaticOutputDevice.type));
                    }
                }
            } catch  (Exception ex)
            {
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = actionName; });
                Connection.SetImageAsync(Utils.CreateOutputDeviceKey(null));
            }
        }

        private void VolumeNotification(AudioVolumeNotificationData data)
        {
            SetImage();
        }

        public async void SetType(string type)
        {
            // TODO: Allow types on non static actions. 
            if (actionSettings.StaticOutputDevice == null) return;
            switch (type)
            {
                case "headset":
                    actionSettings.StaticOutputDevice.icon = BitmapToBase64(new Bitmap(headsetImage));
                    break;
                case "speaker":
                    actionSettings.StaticOutputDevice.icon = BitmapToBase64(new Bitmap(speakerImage));
                    break;
                case "display":
                    actionSettings.StaticOutputDevice.icon = BitmapToBase64(new Bitmap(displayImage));
                    break;
                default:
                    actionSettings.StaticOutputDevice.icon = BitmapToBase64(new Bitmap(outputImage)); ;
                    break;
            }

            // Save only global, as it will update the local settings.
            await SaveGlobalSettings();
        }

        public void SetControlType(Utils.ControlType controlType)
        {
            this.controlType = controlType;
            switch (controlType)
            {
                case Utils.ControlType.VolumeMute:
                    _ = Connection.SetImageAsync(Utils.CreateVolumeMuteKey(), null, true);
                    break;
                case Utils.ControlType.VolumeDown:
                    _ = Connection.SetImageAsync(Utils.CreateVolumeDownKey((float)Int32.Parse(actionSettings.GlobalVolumeStep)), null, true);
                    break;
                case Utils.ControlType.VolumeUp:
                    _ = Connection.SetImageAsync(Utils.CreateVolumeUpKey((float)Int32.Parse(actionSettings.GlobalVolumeStep)), null, true);
                    break;
                default:
                    OutputDeviceActions.AddToQueue(this);
                    break;
            }
        }

        private async void ToggleBlacklistDevice(string displayName)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{displayName} added to blacklist");
            SentrySdk.AddBreadcrumb(
                message: "Add to blacklist",
                category: actionName,
                level: BreadcrumbLevel.Info,
                data: new Dictionary<string, string> {
                    { "displayName", $"{displayName}" },
                }
            );

            var staticDevice = pluginController.globalSettings.StaticOutputDevices.Find(device => device.displayName == displayName);
            if (staticDevice != null) return;

            var existingBlacklistedDevice = actionSettings.BlacklistedOutputDevices.Find(device => device.displayName == displayName);
            if (existingBlacklistedDevice != null)
            {
                actionSettings.BlacklistedOutputDevices.Remove(existingBlacklistedDevice);
                actionSettings.BlacklistedOutputDeviceName = null;
            }
            else
            {
                OutputDeviceSetting outputDeviceSetting = actionSettings.BlacklistedOutputDevicesSelector.Find(device => device.id == deviceId);

                if (outputDeviceSetting != null)
                {
                    actionSettings.BlacklistedOutputDevices.Add(outputDeviceSetting);
                    actionSettings.BlacklistedOutputDeviceName = null;
                }
            }

            // Save only global, as it will update the local settings.
            await SaveGlobalSettings();
        }

        public override void Dispose()
        {
            base.Dispose();

            ReleaseDevice();

            // Required before releasing...
            ToggleStaticDevice(null);

            OutputDeviceActions.Remove(this);
        }

        public void ReleaseDevice(MMDevice device = null)
        {
           SentrySdk.AddBreadcrumb(
              message: "Releasing device",
              category: actionName,
              level: BreadcrumbLevel.Info,
              data: new Dictionary<string, string> {
                  { "deviceId", $"{deviceId}" },
                  { "displayName", $"{displayName}" },
                  { "outputDeviceActions", $"{OutputDeviceActions.outputDeviceActions.Count()}" },
              }
           );

            try
            {
                if (device == null) device = Device;
                if (device?.AudioEndpointVolume != null) device.AudioEndpointVolume.OnVolumeNotification -= VolumeNotification;
            } catch (Exception ex)
            {
                SentrySdk.AddBreadcrumb(
                     message: "Unable to access AudioEndpointVolume",
                     category: actionName,
                     level: BreadcrumbLevel.Error,
                     data: new Dictionary<string, string> {
                        { "error", ex.Message.ToString() }
                     }
                 );
            }

            deviceId = null;
        }

        public async Task RefreshDevices()
        {
            try
            {
                SentrySdk.AddBreadcrumb(
                    message: "Refresh devices",
                    category: actionName,
                    level: BreadcrumbLevel.Info
                );

                var devices = AudioManager.OutputDevices.ToList();
                var deviceSettings = devices.ConvertAll(device => new OutputDeviceSetting(devices, device));

                /**
                * Static
                **/
                actionSettings.StaticOutputDevicesSelector = new List<OutputDeviceSetting>(deviceSettings);

                // TODO: Add additional logic removing impossible combinations. Handle if one was already set.
                actionSettings.StaticOutputDevicesSelector.RemoveAll(device => pluginController.globalSettings.StaticOutputDevices.Find(_device => _device.id == device.id) != null);
                actionSettings.StaticOutputDevicesSelector.RemoveAll(device => pluginController.globalSettings.BlacklistedOutputDevices.Find(_device => _device.id == device.id) != null);


                // If this is a static process which does not have an active audio session, add it to the selector.
                /* if (actionSettings.StaticApplication != null)
                 {
                     var staticApplication = actionSettings.StaticApplicationsSelector.Find(app => app.processName == actionSettings.StaticApplication.processName);
                     if (staticApplication == null)
                     {
                         actionSettings.StaticApplicationsSelector.Add(actionSettings.StaticApplication);
                     }
                 }*/


                /**
                * Blacklist
                * 
                * NOTES: 
                * - The blacklist selector should also include blacklisted devices
                **/
                actionSettings.BlacklistedOutputDevicesSelector = new List<OutputDeviceSetting>(deviceSettings).Concat(pluginController.globalSettings.BlacklistedOutputDevices).DistinctBy(device => device.id).ToList();
                actionSettings.BlacklistedOutputDevicesSelector.RemoveAll(device => pluginController.globalSettings.StaticOutputDevices.Find(_device => _device.id == device.id) != null);
                actionSettings.BlacklistedOutputDevicesSelector.RemoveAll(device => pluginController.globalSettings.WhitelistedOutputDevices.Find(_device => _device.id == device.id) != null);

                /**
                * Whitelist
                **/
                actionSettings.WhitelistedOutputDevicesSelector = new List<OutputDeviceSetting>(deviceSettings);
                actionSettings.WhitelistedOutputDevicesSelector.RemoveAll(device => actionSettings.BlacklistedOutputDevices.Find(_device => _device.id == device.id) != null);

                await SaveSettings();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, ex.ToString());
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = actionName; });
            }
        }

        public override async Task SaveGlobalSettings(bool triggerDidReceiveGlobalSettings = true)
        {
            await base.SaveGlobalSettings();

            OutputDeviceActions.Reload();
        }

        // TODO: Handle empty processName or omit them as sessions.
        private async void ToggleStaticDevice(string displayName)
        {
            // TODO: Check that no other action has the same static application.
            if (string.IsNullOrEmpty(displayName) || this.actionSettings.StaticOutputDeviceName == displayName)
            {
                if (actionSettings.StaticOutputDevice == null) return;

                pluginController.globalSettings.StaticOutputDevices.Remove((actionSettings.StaticOutputDevice));

                actionSettings.StaticOutputDevice = null;
                actionSettings.StaticOutputDeviceName = null;
            }
            else
            {
                OutputDeviceSetting outputDeviceSetting = actionSettings.BlacklistedOutputDevicesSelector.Find(device => device.displayName == displayName);
                MMDevice outputDevice = AudioManager.OutputDevices.FirstOrDefault(device => device.ID == outputDeviceSetting.id);
                if (outputDevice == null) return;

                // Ensure it is not a static application.
                if (pluginController.globalSettings.StaticOutputDevices.Find(device => device.id == outputDevice.ID) != null) return;
                // Ensure it is not in the blacklist.
                if (pluginController.globalSettings.BlacklistedOutputDevices.Find(device => device.id == outputDevice.ID) != null) return;

                actionSettings.StaticOutputDevice = outputDeviceSetting;
                actionSettings.StaticOutputDeviceName = displayName;

                // Add it to the global settings list. 
                pluginController.globalSettings.StaticOutputDevices.Add(outputDeviceSetting);
            }

            // Save only global, as it will update the local settings.
            await SaveGlobalSettings();
        }

        public override async void OnSendToPlugin(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;
            if (payload["action"] == null || payload["action"].ToString() != "outputDevice") return;

            Logger.Instance.LogMessage(TracingLevel.INFO, JObject.FromObject(new Dictionary<string, string> {
               { "payload", payload.ToString() }
            }).ToString());

            SentrySdk.AddBreadcrumb(
                message: "Received data from property inspector",
                category: actionName,
                level: BreadcrumbLevel.Info,
                data: new Dictionary<string, string>{
                    { "payload", payload.ToString() }
                 }
            );

            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString())
                {
                    case "setStaticDevice":
                        ToggleStaticDevice(payload["value"].ToString());
                        break;
                    case "toggleBlacklistDevice":
                        ToggleBlacklistDevice(payload["value"].ToString());
                        break;
                    case "setType":
                        SetType(payload["value"].ToString());
                        break;
                    case "refreshDevices":
                        await RefreshDevices();
                        break;
                    case "resetGlobalSettings":
                        await ResetGlobalSettings();
                        break;
                    case "resetSettings":
                        await ResetSettings();
                        await RefreshDevices();
                        break;
                }
            }
        }
    }
}
