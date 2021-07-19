using System;
using System.Xml.Serialization;

namespace ABPMilkDryfeHiberSrv
{
    public class ShutdownOptions
    {
        #region Variables & Constants

        [XmlElementAttribute("Guid", IsNullable = false)]
        private Guid guidOptions = new Guid();
        [XmlElementAttribute("ShutdownAction", IsNullable = false)]
        private string strShutdownAction = "Hibernate";

        #endregion

        #region Properties

        /// <summary>
        /// The globally unique identifier that is used to make sure the signal sent to Milk and Dryfe is not random noise
        /// </summary>
        public Guid Guid
        {
            get { return guidOptions; }
            set { guidOptions = value; }
        }

        /// <summary>
        /// The type of shutdown to initiate in Milk and Dryfe, Shutdown, Hibernate or Restart. The default is Hibernate
        /// </summary>
        public string ShutdownAction
        {
            set { strShutdownAction = value; }
            get { return strShutdownAction; }
        }

        #endregion
    }
}
