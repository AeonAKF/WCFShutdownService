using System;
using System.Xml.Serialization;

namespace ABPMilkDryfeHiberSrv
{
    public class ShutdownActivationState
    {
        #region Variables & Constants

        [XmlElementAttribute("IsActive", IsNullable = false)]
        private string strShutdownActivationState = "true";

        #endregion

        #region Properties

        /// <summary>
        /// Determines whether the hibernation service is active. If false, no shutdown will occur.
        /// </summary>
        public string CurrentShutdownActivationState
        {
            set { strShutdownActivationState = value; }
            get { return strShutdownActivationState; }
        }

        #endregion
    }
}
