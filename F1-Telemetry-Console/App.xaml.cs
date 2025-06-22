using System.Windows;

namespace F1_Telemetry_Console
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Set the UI culture for consistent formatting
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
        }
        
        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
        }
    }
} 