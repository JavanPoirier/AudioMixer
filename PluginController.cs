using AudioMixer.Actions;
using BarRaider.SdTools;
using Newtonsoft.Json.Linq;
using NLog;
using Sentry;
using streamdeck_client_csharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AudioMixer
{
    public class PluginController : IDisposable
    {
        private static readonly Lazy<PluginController> instance = new Lazy<PluginController>(() => new PluginController());

        public static PluginController Instance
        {
            get
            {
                return instance.Value;
            }
        }

        public GlobalSettings globalSettings;
        public List<BaseAction<GlobalSettings>> actions = new List<BaseAction<GlobalSettings>>();

        private bool initialized = false;

        private PluginController()
        {
            BarRaider.SdTools.Logger.Instance.LogMessage(TracingLevel.DEBUG, $"PluginController: {GetHashCode()}");

            // TODO: Not being called... from here or from connection.
            GlobalSettingsManager.Instance.OnReceivedGlobalSettings += ReceivedGlobalSettings;
            GlobalSettingsManager.Instance.RequestGlobalSettings();
             
            LogManager.Configuration.AddSentry(o =>
                {
                    o.Layout = "${message}";
                    o.BreadcrumbLayout = "${logger}: ${message}";
                    o.MinimumBreadcrumbLevel = LogLevel.Debug;
                    o.MinimumEventLevel = LogLevel.Error;
                    o.AddTag("logger", "${logger}");
                });

            // If not admin, restart as admin
            var isAdmin = Utils.IsAdministrator();
            if (!isAdmin)
            {
                Utils.ExecuteAsAdmin(Environment.GetCommandLineArgs()[0]);
                Thread.Sleep(3000);
                Environment.Exit(-1);
            }

            AudioManager.Init();
        }

        public void Dispose()
        {
            GlobalSettingsManager.Instance.OnReceivedGlobalSettings -= ReceivedGlobalSettings;
        }

        public List<ApplicationAction> GetApplicationActions()
        {
            return actions.OfType<ApplicationAction>().Where(action => action.type == ActionType.APPLICATION).ToList();
        }

        public List<OutputDeviceAction> GetOutputDeviceActions()
        {
            // Filter actions where the action is of type OutputDeviceAction and the action type is OUTPUTDEVICE
            return actions.OfType<OutputDeviceAction>().Where(action => action.type == ActionType.OUTPUTDEVICE).ToList();
        }

        private void ReceivedGlobalSettings(object sender, ReceivedGlobalSettingsPayload payload)
        {
            if (payload?.Settings != null && payload.Settings.Count > 0)
            {
                globalSettings = payload.Settings.ToObject<GlobalSettings>();

                if (!initialized)
                {
                    initialized = true;
                    SentrySdk.ConfigureScope(scope =>
                    {
                        var user = new User();
                        user.Id = globalSettings.UUID;
                        scope.User = user;
                    });

                    var sentryEvent = new SentryEvent();
                    SentrySdk.CaptureMessage("Initialized", scope => scope.TransactionName = "PluginController", SentryLevel.Info);
                }   
            }
        }
    }
}
