using BarRaider.SdTools;
using NLog;
using Sentry;
using System.Threading;
using System;
using System.Diagnostics;
using NLog.Config;
using System.Windows.Forms;

namespace AudioMixer
{
    class Program
    {
        static void Main(string[] args)
        {
            Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            GlobalDiagnosticsContext.Set("Application", "Audio Mixer");
            GlobalDiagnosticsContext.Set("Version", "1.3.2");

            // Uncomment this line of code to allow for debugging
            // while (!System.Diagnostics.Debugger.IsAttached) { System.Threading.Thread.Sleep(100); }

#if DEBUG
            var isDebug = true;
#else   
            var isDebug = false;
#endif

            using (SentrySdk.Init(o =>
            {
                o.Dsn = "https://4a22d40cccbc4268abe582e0e30e6ae3@o1232787.ingest.sentry.io/6381003";
                o.AutoSessionTracking = true;
                o.Release = "audio-mixer@1.3.2";
                o.Environment = isDebug ? "development" : "production";
                o.MaxBreadcrumbs = 50;
                o.SampleRate = isDebug ? 1f : 0.1f;
                o.TracesSampleRate = isDebug ? 1 : 0.1;
                o.Debug = isDebug;
            }))
            {
                LogManager.Configuration = new LoggingConfiguration();
                LogManager.Configuration.AddSentry(o =>
                {
                    o.Layout = "${message}";
                    o.BreadcrumbLayout = "${logger}: ${message}";
                    o.MinimumBreadcrumbLevel = LogLevel.Debug;
                    o.MinimumEventLevel = LogLevel.Error;
                    o.AddTag("logger", "${logger}");
                });

                SDWrapper.Run(args);
            }
        }

        static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            // Log the exception, display it, etc
            Debug.WriteLine(e.Exception.Message);
            SentrySdk.CaptureMessage(e.ToString(), scope => scope.TransactionName = "ThreadException");
        }
        static async void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Log the exception, display it, etc
            Debug.WriteLine((e.ExceptionObject as Exception).Message);
            SentrySdk.CaptureMessage(e.ToString(), scope => scope.TransactionName = "UnhandledException");

            // For safety, clear all settings.
            // await PluginController.ResetGlobalSettings();
        }
    }
}
