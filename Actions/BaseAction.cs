using BarRaider.SdTools;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using System.Timers;
using Sentry;
using static AudioMixer.ApplicationAction;
using System.Collections.Generic;
using Newtonsoft.Json.Bson;
using System.Globalization;
using System.Diagnostics;
using BarRaider.SdTools.Events;

namespace AudioMixer
{
    public enum ActionType
    {
        APPLICATION,
        OUTPUTDEVICE,
        VOLUME,
    }

    public abstract class BaseAction<ActionSettings> : PluginBase where ActionSettings : GlobalSettings, new()
    {
        public string actionName;
        public readonly string coords;
        public readonly ActionType type;
        public ActionSettings actionSettings;
        public PluginController pluginController = PluginController.Instance;

        protected bool timerElapsed = false;
        protected bool initialized = false;
        protected Timer timer = new Timer(200);
        protected Stopwatch stopWatch = new Stopwatch();

        public BaseAction(SDConnection connection, InitialPayload payload, ActionType type, string actionName) : base(connection, payload)
        {
            this.type = type;
            this.actionName = actionName;
            
            coords = $"{payload.Coordinates.Column} {payload.Coordinates.Row}";
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Initializing {actionName} key at: {coords}");
            Connection.LogSDMessage($"Initializing {actionName} key at: {coords}");

            SentrySdk.AddBreadcrumb(
                message: $"Initializing {actionName} key",
                category: actionName,
                level: BreadcrumbLevel.Info
            );

            // No need to assign settings if null, as RecievedGlobalSettings will handle it.
            if (payload.Settings != null)
            {
                try
                {
                    // Backfill any properties against those received.
                    var newActionSettingsJObject = JObject.FromObject(new ActionSettings().CreateInstance(Connection.StreamDeckConnection.UUID));
                    newActionSettingsJObject.Merge(payload.Settings, new JsonMergeSettings
                    {
                        MergeArrayHandling = MergeArrayHandling.Merge,
                    });

                    // Tools.AutoPopulateSettings(JObject.FromObject(actionSettings), newActionSettingsJObject);
                    actionSettings = newActionSettingsJObject.ToObject<ActionSettings>();

                    // NOTE: This is called by SaveSettings and requires global settings, and not needed here.
                    // InitAction();
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"Assigning settings from the constructor payload failed. Resetting...");
                    Connection.LogSDMessage($"Assigning settings from the constructor payload failed. Resetting...");
                    SentrySdk.CaptureException(ex, scope => { scope.TransactionName = actionName; });

                    _ = ResetSettings();
                }
            }

            Connection.GetGlobalSettingsAsync();
        }

        protected abstract void InitActionCore();

        public void InitAction()
        {
            if (!initialized)
            {
                initialized = true;
                Connection.OnSendToPlugin += OnSendToPlugin;
                OnKeyRelease += HandleKeyReleased;

                InitActionCore();
                SetKey();
            }
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called: {coords}");

            Connection.OnSendToPlugin -= OnSendToPlugin;
            OnKeyRelease -= HandleKeyReleased;

            timer.Elapsed -= HandleKeyHeld;
            timer.Dispose();
        }

        protected abstract void SetKey();

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Key Pressed: {coords}");

            var sentryEvent = new SentryEvent();
            sentryEvent.Message = "Key Pressed";
            SentrySdk.CaptureEvent(sentryEvent);

            if (!initialized)
            {
                SentrySdk.AddBreadcrumb(
                   message: "Action not yet initialized",
                   category: actionName,
                   level: BreadcrumbLevel.Info
                );
                return;
            }

            timerElapsed = false;
            timer.Elapsed += HandleKeyHeld;
            timer.Elapsed += (object timerSender, ElapsedEventArgs elapsedEvent) => {
                timerElapsed = true;
            };
            timer.AutoReset = false;
            timer.Start();
        }

        protected virtual void HandleKeyHeld(object timerSender, ElapsedEventArgs elapsedEvent) { }

        public class KeyReleasedEventArgs : EventArgs
        {
            public KeyReleasedEventArgs(bool timerElapsed)
            {
                this.timerElapsed = timerElapsed;
            }

            public bool timerElapsed;
        }

        public event EventHandler<KeyReleasedEventArgs> OnKeyRelease;

        public override void KeyReleased(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Released");
            SentrySdk.AddBreadcrumb(
                message: "Key released",
                category: actionName,
                level: BreadcrumbLevel.Info
            );

            timer.Stop();
            stopWatch.Stop();

            timer.Elapsed -= HandleKeyHeld;

            OnKeyRelease?.Invoke(this, new KeyReleasedEventArgs(timerElapsed));
        }

        protected abstract void HandleKeyReleased(object sender, KeyReleasedEventArgs e);

        // Global settings are received on action initialization. Local settings are only received when changed in the PI.
        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
        {
            try
            {
                SentrySdk.AddBreadcrumb(
                   message: "Received global settings",
                   category: actionName,
                   level: BreadcrumbLevel.Info,
                   data: new Dictionary<string, string> { { "settings", payload.Settings.ToString() } }
               );

                // Global settings exist, update the action setting with the global settings.
                if (payload?.Settings != null && payload.Settings.Count > 0)
                {
                    // Backfill any properties against those received.
                    var newActionSettingsJObject = JObject.FromObject(new ActionSettings().CreateInstance(Connection.StreamDeckConnection.UUID));

                    if (actionSettings != null) {
                        newActionSettingsJObject.Merge(JObject.FromObject(actionSettings), new JsonMergeSettings
                        {
                            MergeArrayHandling = MergeArrayHandling.Merge,
                        });
                    }

                    // Replace, not merge global settings that are stored in local settings for the PI.
                    newActionSettingsJObject.Merge(payload.Settings, new JsonMergeSettings
                    {
                        MergeArrayHandling = MergeArrayHandling.Replace,
                    });

                    // For some reason the listener in PluginController is not being called, so we need to set the global settings here.
                    pluginController.globalSettings = payload.Settings.ToObject<GlobalSettings>();

                    actionSettings = newActionSettingsJObject.ToObject<ActionSettings>();
                    _ = SaveSettings();
                }
                else // Global settings do not exist, create them and save
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"No global settings found, creating new object");
                    _ = ResetGlobalSettings();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{GetType()} ReceivedGlobalSettings Exception: {ex}");
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = actionName; });

                _ = ResetGlobalSettings();
            }
        }

        // NOTE: Global settings are always derived from the action settings.
        public virtual async Task SaveGlobalSettings(bool triggerDidReceiveGlobalSettings = true)
        {
            try
            {
                // Merge existing global settings with a new instance to backfill any missing properties. Using MergeArrayHandling.Merge will only merge where properties exist in both.
                // This is intentional to clear out any stale properties should the structure change.
                var newGlobalSettingsJObject = JObject.FromObject(new GlobalSettings().CreateInstance(Connection.StreamDeckConnection.UUID));
                if (pluginController.globalSettings != null)
                {
                    newGlobalSettingsJObject.Merge(JObject.FromObject(pluginController.globalSettings), new JsonMergeSettings
                    {
                        MergeArrayHandling = MergeArrayHandling.Merge,
                    });
                }

                // Do the same as above but with the action settings. Ensuring only the properties that exist in both are updated.
                var newActionSettingsJObject = JObject.FromObject(new ActionSettings().CreateInstance(Connection.StreamDeckConnection.UUID));
                if (actionSettings != null)
                {
                    newActionSettingsJObject.Merge(JObject.FromObject(actionSettings), new JsonMergeSettings
                    {
                        MergeArrayHandling = MergeArrayHandling.Merge,
                    });
                }

                // Merge the action settings with the global as it holds the source of truth from the PI.
                newGlobalSettingsJObject.Merge(newActionSettingsJObject, new JsonMergeSettings
                {
                    MergeArrayHandling = MergeArrayHandling.Merge,
                });

                await Connection.SetGlobalSettingsAsync(newGlobalSettingsJObject, triggerDidReceiveGlobalSettings);
                
                // If we are not going to trigger didReceiveGlobalSettings, then we need to set the global settings here.
                if (!triggerDidReceiveGlobalSettings) pluginController.globalSettings = newGlobalSettingsJObject.ToObject<GlobalSettings>();
            } catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{GetType()} SaveGlobalSettings Exception: {ex}");
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = actionName; });
            }
        }

        // NOTE: Does not get called by SaveSettings
        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            try
            {
                SentrySdk.AddBreadcrumb(
                    message: "Received settings",
                    category: actionName,
                    level: BreadcrumbLevel.Info,
                    data: new Dictionary<string, string> { { "setting", payload.Settings.ToString() } }
                );

                // Tools.AutoPopulateSettings(JObject.FromObject(actionSettings), payload.Settings);
                actionSettings = payload.Settings.ToObject<ActionSettings>();

                // As this is set/derived from global changes, and to prevent recursion, we don't need the notify the receiver of the global settings.
                // _ = SaveGlobalSettings(false);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{GetType()} ReceivedSettings Exception: {ex}");
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = actionName; });

                _ = ResetSettings();
            }
        }

        // NOTE: Calling SetSettingsAsync does NOT fire the ReceivedSettings event. 
        protected async Task SaveSettings()
        {
            if (actionSettings != null) await Connection.SetSettingsAsync(JObject.FromObject(actionSettings));

            if (!initialized) InitAction();
        }

        protected async Task ResetGlobalSettings()
        {
            try
            {
                SentrySdk.AddBreadcrumb(
                  message: "Reset global settings",
                  category: actionName,
                  level: BreadcrumbLevel.Info
                );

                var newGlobalSettingsJObject = JObject.FromObject(new GlobalSettings().CreateInstance(Connection.StreamDeckConnection.UUID));
                await Connection.SetGlobalSettingsAsync(newGlobalSettingsJObject);
            } catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{GetType()} ReceivedSettings Exception: {ex}");
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = actionName; });
            }
        }

        protected async Task ResetSettings()
        {
            try
            {
                SentrySdk.AddBreadcrumb(
                   message: "Reset action settings",
                   category: actionName,
                   level: BreadcrumbLevel.Info
               );

                // Create a new instance with default values, then apply the global settings. Ensuring only the properties that exist in the action settings only are not overwritten.
                var newActionSettingsJObject = JObject.FromObject(new ActionSettings().CreateInstance(Connection.StreamDeckConnection.UUID));
                if (pluginController.globalSettings != null)
                {
                    newActionSettingsJObject.Merge(JObject.FromObject(pluginController.globalSettings), new JsonMergeSettings
                    {
                        MergeArrayHandling = MergeArrayHandling.Merge,
                    });
                }

                actionSettings = newActionSettingsJObject.ToObject<ActionSettings>();
                await SaveSettings();
            } catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{GetType()} ResetSettings Exception: {ex}");
                SentrySdk.CaptureException(ex, scope => { scope.TransactionName = actionName; });
            }
        }

        public virtual async void OnSendToPlugin(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e) { }

        public override void OnTick() { }
    }
}