using BarRaider.SdTools;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace AudioMixer.Actions
{
    internal class ApplicationActions
    {
        private static ConcurrentQueue<ApplicationAction> applicationActionQueue = new ConcurrentQueue<ApplicationAction>();
        private static readonly object actionsLock = new object();

        public static List<ApplicationAction> applicationActions = new List<ApplicationAction>();
        private static ApplicationAction selectedApplicationAction;

        public static ApplicationAction SelectedAction
        {
            get { return selectedApplicationAction; }
            set
            {
                if (value == selectedApplicationAction)
                {
                    selectedApplicationAction?.SetSelected(false);
                    selectedApplicationAction = null;
                }
                else
                {
                    // Reset previous selected action
                    if (selectedApplicationAction != null) selectedApplicationAction.SetSelected(false);

                    selectedApplicationAction = value;
                    if (selectedApplicationAction != null) selectedApplicationAction.SetSelected(true);
                }

                if (PluginController.Instance.globalSettings.InlineControlsEnabled) SetActionControls();
            }
        }

         public static void Add(ApplicationAction applicationAction)
        {
            if (applicationActions.Contains(applicationAction)) return;
            lock (actionsLock) 
            {
                applicationActions.Add(applicationAction);       
            }

            Reload();
        }

         public static void Remove(ApplicationAction applicationAction)
        {
            lock (actionsLock)
            {
                applicationActions.Remove(applicationAction);
            }

            Reload();
        }

         public static void AddToQueue(ApplicationAction applicationAction)
        {
            lock (actionsLock)
            {
                applicationActionQueue.Enqueue(applicationAction);

                ApplicationAction enqueuedAction;
                while (applicationActionQueue.TryDequeue(out enqueuedAction)) enqueuedAction.SetAudioSession();
            }
        }

         public static void Reload()
        {
            lock (actionsLock)
            {
                // Sort the application actions by static first.
                var sortedByStaticApplications = applicationActions.OrderBy(x => x.actionSettings.StaticApplicationName ?? "").ToList();
                applicationActionQueue = new ConcurrentQueue<ApplicationAction>(sortedByStaticApplications);

                // No need to reset icon as when the action is set in queue it will be reset if need be.
                applicationActions.ForEach(action => action.ReleaseAudioSessions());

                ApplicationAction enqueuedAction;
                while (applicationActionQueue.TryDequeue(out enqueuedAction)) enqueuedAction.SetAudioSession(false);
            }
        }

         private static void SetActionControls()
         {
            // Only do this if inline controls are enabled.
            if (!PluginController.Instance.globalSettings.InlineControlsEnabled) return;

            // TODO: Should I handle this possibly undefined?
            if (SelectedAction != null && PluginController.Instance.globalSettings.InlineControlsEnabled)
            {
                List<ApplicationAction> controls = applicationActions.FindAll(action => action != SelectedAction);

                if (controls.Count >= 3)
                {
                    controls[0].SetControlType(Utils.ControlType.VolumeMute);
                    controls[1].SetControlType(Utils.ControlType.VolumeDown);
                    controls[2].SetControlType(Utils.ControlType.VolumeUp);
                }
                else
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, "Not enough plugin actions available to place controls.");
                }
            }
            else
            {
                // Reset all application actions.
                applicationActions.ForEach(pluginAction => pluginAction.SetControlType(Utils.ControlType.Application));
            }
        }
    }
}
