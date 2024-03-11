
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace AudioMixer
{
    // https://stackoverflow.com/questions/2183541/c-detecting-which-application-has-focus
    public class FocusMonitor
    {
        public FocusMonitor(focusHandler)
        {
            AutomationFocusChangedEventHandler focusHandler = OnFocusChanged;
            Automation.AddAutomationFocusChangedEventHandler(focusHandler);
        }

        public Process OnFocusChanged(object sender, AutomationFocusChangedEventArgs e)
        {
            AutomationElement focusedElement = sender as AutomationElement;
            if (focusedElement != null)
            {
                int processId = focusedElement.Current.ProcessId;
                using (Process process = Process.GetProcessById(processId))
                {
                    Debug.WriteLine(process.ProcessName);
                    return process;
                }
            }
        }
    }
}
