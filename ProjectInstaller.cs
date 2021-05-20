using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace WHEAservice
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
            EventLogInstaller DefaultInstaller = null;
            foreach (Installer installer in serviceInstaller1.Installers)
            {
                if (installer is EventLogInstaller)
                {
                    DefaultInstaller = (EventLogInstaller)installer;
                    break;
                }
            }
            if (DefaultInstaller != null)
            {
                serviceInstaller1.Installers.Remove(DefaultInstaller);
            }
        }

        private void serviceInstaller1_AfterUninstall(object sender, InstallEventArgs e)
        {
            if (EventLog.SourceExists("WHEAService"))
            {

                EventLog.DeleteEventSource("WHEAService");
                EventLog.Delete("WHEAServiceLog");
            }
        }

        private void serviceInstaller1_AfterInstall(object sender, InstallEventArgs e)
        {
            if (EventLog.SourceExists("WHEAService"))
                EventLog.DeleteEventSource("WHEAService");
            EventLog.CreateEventSource("WHEAService", "WHEAServiceLog");
            EventLog.WriteEntry("WHEAService", "WHEAService Installed");
        }
    }
    public partial class CustomEventLogInstaller : Installer
    {
        private EventLogInstaller customEventLogInstaller;
        public CustomEventLogInstaller()
        {
            customEventLogInstaller = new EventLogInstaller();
            customEventLogInstaller.Source = "WHEAService";
            customEventLogInstaller.Log = "WHEAServiceLog";
            Installers.Add(customEventLogInstaller);
        }
    }

}
