using System;
using System.ServiceModel;

namespace ABPMilkDryfeHiberSrv
{
    public class Service : IService
    {

        /// <summary>
        /// This function is initiated by the master service on Lochar. It commands Milk hibernate
        /// </summary>
        public void SendShutdown()
        {
            srvMain.ReadXMLShutdownActivationState(srvMain.ApplicationFolder + srvMain.ShutdownActiavtionStateFileName) ; // Read Shutdown Activation State file

            ShutdownActivationState tmpSAS = new ShutdownActivationState();
            if (tmpSAS.CurrentShutdownActivationState == "true")
            {
                srvMain.HibernateSystem = true;
                srvMain.WriteToLog(srvMain.ApplicationFolder + srvMain.LogFileName, DateTime.Now + ",Hibernating System....");
                System.Windows.Forms.Application.SetSuspendState(System.Windows.Forms.PowerState.Hibernate, true, true);
            }
        }
    }
}

