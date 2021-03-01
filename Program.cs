using BarRaider.SdTools;
using System;

namespace AudioMixer
{
    class Program
    {
        static void Main(string[] args)
        {
            // Uncomment this line of code to allow for debugging
            while (!System.Diagnostics.Debugger.IsAttached) { System.Threading.Thread.Sleep(100); }

            try {
                Console.WriteLine("HERE");
                SDWrapper.Run(args);
            } catch (Exception e)
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"ERROR: {e.Message}");
            }

            Console.ReadKey();
        }
    }
}
