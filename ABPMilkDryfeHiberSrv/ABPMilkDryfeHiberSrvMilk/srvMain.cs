#region Using Statements

using System;
using System.ServiceProcess;
using System.ServiceModel;
using System.IO;
using Microsoft.Win32;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

#endregion Using Statements

namespace ABPMilkDryfeHiberSrv
{
    public partial class srvMain : ServiceBase
    {
        #region Variables, Constants & Objects

        // Shutdown variables
        private static bool blShutdownSystem = false;
        private static ShutdownActivationState objShutdownActivationState = new ShutdownActivationState();

        // FileSysteMWatcher variables for Activation state file
        private static bool blHasShutdownActivationStateFileChanged = false;
        private static FileStream fsXML;
        private static TextWriter twXML;

        // WCF variables
        private ServiceHost WCFHost;
        private System.Timers.Timer timerMain;

        // Logging variables
        private static Object lockWriteLog = new Object();
        private static string strAppFolderPath = AppDomain.CurrentDomain.BaseDirectory + @"\";
        private static string strLogFileName = "ABPMilkDryfeHibernateSrvDryfeLog.log";
        private static string strShutdownActivationStateFileName = "ShutdownActivationState.xml";

        #endregion Variables, Constants & Objects

        #region Constructor

        public srvMain()
        {
            InitializeComponent();
        }

        #endregion Constructor

        #region Override Functions

        protected override void OnStart(string[] args)
        {
            if (ReadXMLShutdownActivationState(strAppFolderPath + strShutdownActivationStateFileName) == false) // Read Shutdown Activation State
            {
                WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to load ShutdownActivationState.xml, using default values.");
            }

            try
            { // Create the host and binding to be used by the service.
                string baseAddress = "net.tcp://localhost:10102/BackupService";
                NetTcpBinding binding1 = new NetTcpBinding(SecurityMode.None);
                WCFHost = new ServiceHost(typeof(Service));
                WCFHost.AddServiceEndpoint(typeof(IService), binding1, baseAddress);

                WCFHost.Open(); // Start the service

            }
            catch (Exception ex)
            {
                WriteToLog(strAppFolderPath + strLogFileName, ex.Message);
            }


            // Configure timer for opening WCF end point
            timerMain = new System.Timers.Timer(5000);
            timerMain.Elapsed += new System.Timers.ElapsedEventHandler(timerMain_Tick);
            timerMain.Interval = 5000;
            timerMain.Enabled = true;
        }

        protected override void OnStop()
        {
            try
            {
                WCFHost.Close();
            }
            catch (Exception ex)
            {
                WriteToLog(strAppFolderPath + strLogFileName, "Error closing WCF host object " + ex.Message);
            }
        }

        #endregion Override Functions

        #region WCF Timer Functions

        private void timerMain_Tick(object sender, EventArgs e)
        {
            if (WCFHost == null)
            {
                try
                { // Create the host and binding to be used by the service.
                    string baseAddress = "net.tcp://localhost:10102/BackupService";
                    NetTcpBinding binding1 = new NetTcpBinding(SecurityMode.None);
                    WCFHost = new ServiceHost(typeof(Service));
                    WCFHost.AddServiceEndpoint(typeof(IService), binding1, baseAddress);

                    WCFHost.Open(); // Start the service

                }
                catch (Exception ex)
                {
                    WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to open WCF end point: " + ex.Message);
                }
            }
        }

        #endregion WCF Timer Functions

        #region FileSystemWatcher Functions

        private void fswShutdownActivationStateFile_Created(object sender, FileSystemEventArgs e)
        {
            if (blHasShutdownActivationStateFileChanged == false) // Only run if not already running
            {
                blHasShutdownActivationStateFileChanged = true; // Set flag so this function doesn't run while already running                        
                Thread.Sleep(2000);
                ShutdownActivationState tmpShutdownActivationState = new ShutdownActivationState(); // Get Shutdown Activation State before ReadXML()
                if (ReadXMLShutdownActivationState(strAppFolderPath + strShutdownActivationStateFileName) == true)
                {  // Read options file successfully                            
                    WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Status,Shutdown activation state file loaded");
                    ShutdownActivationState tmpShutdownActivationState1 = new ShutdownActivationState();
                }
                else // Did not read Shutdown activation state file successfully
                {
                    WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Status,Unable to load options file");
                }
                blHasShutdownActivationStateFileChanged = false; // Set flag so that function can run again
            }
            blHasShutdownActivationStateFileChanged = false;

        }

        private void fswShutdownActivationStateFile_Changed(object sender, FileSystemEventArgs e)
        {
            if (blHasShutdownActivationStateFileChanged == false) // Only run if not already running
            {
                blHasShutdownActivationStateFileChanged = true; // Set flag so this function doesn't run while already running                        
                Thread.Sleep(2000);
                ShutdownActivationState tmpShutdownActivationState = new ShutdownActivationState(); // Get Shutdown Activation State before ReadXML()
                if (ReadXMLShutdownActivationState(strAppFolderPath + strShutdownActivationStateFileName) == true)
                {  // Read options file successfully                            
                    WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Status,Shutdown activation state file loaded");
                    ShutdownActivationState tmpShutdownActivationState1 = new ShutdownActivationState();
                }
                else // Did not read Shutdown activation state file successfully
                {
                    WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Status,Unable to load options file");
                }
                blHasShutdownActivationStateFileChanged = false; // Set flag so that function can run again
            }
            blHasShutdownActivationStateFileChanged = false;
        }

        #endregion FileSystemWatcher Functions

        #region XML Functions

        /// <summary>
        /// Writes all backup job options to the BackupOptions.xml file located in the application folder
        /// </summary>
        /// <param name="Path">The full path to the BackupOptions.xml file</param>
        /// <returns></returns>
        public static bool ReadXMLShutdownActivationState(string Path)
        {
            if (File.Exists(Path) == false)
            {
                WriteXMLShutdownActivationState();
                return true;
            }
            else
            {
                try
                {   // Read options from XML File by creating XML Serialization of type BackupOptions and text writer to create the actual XML file
                    XmlSerializer serializer = new XmlSerializer(typeof(ShutdownActivationState));
                    /* If the XML document has been altered with unknown nodes or attributes, handle them with the nknownNode and UnknownAttribute events.*/
                    serializer.UnknownNode += new XmlNodeEventHandler(serializer_UnknownNode);
                    serializer.UnknownAttribute += new XmlAttributeEventHandler(serializer_UnknownAttribute);
                    fsXML = new FileStream(Path, FileMode.Open); // Create a FileStream object to read the XML document.
                    /* Use the De-serialize method to restore the object's state with data from the XML document. */
                    objShutdownActivationState = (ShutdownActivationState)serializer.Deserialize(fsXML);
                    fsXML.Close(); // Dispose filestream object
                    // If shutdown action is unusable as a shutdown command then set to default of Hibernate
                    if (objShutdownActivationState.CurrentShutdownActivationState != "true" || objShutdownActivationState.CurrentShutdownActivationState != "false")
                    {
                        objShutdownActivationState.CurrentShutdownActivationState = "true";
                    }
                    return true;
                }
                catch (XmlException Xex) // Catch XML specific error
                {
                    WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to read ShutdownActivationState.xml,Error message: " + Xex.Message);
                    if (fsXML != null) { fsXML.Close(); }
                    // If shutdown action is unusable as a shutdown command then set to default of Hibernate
                    if (objShutdownActivationState.CurrentShutdownActivationState != "true" || objShutdownActivationState.CurrentShutdownActivationState != "false")
                    {
                        objShutdownActivationState.CurrentShutdownActivationState = "true";
                    }
                    return false;
                }
                catch (Exception eX) // Catch any other error
                {
                    WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to read ShutdownActivationState.xml,Error message: " + eX.Message);
                    if (fsXML != null) { fsXML.Close(); }
                    // If shutdown action is unusable as a shutdown command then set to default of Hibernate
                    if (objShutdownActivationState.CurrentShutdownActivationState != "true" || objShutdownActivationState.CurrentShutdownActivationState != "false")
                    {
                        objShutdownActivationState.CurrentShutdownActivationState = "true";
                    }
                    return false;
                }
            }
        }

        /// <summary>
        /// Writes all shutdown options to the ShutdownOptions.xml file located in the application folder.
        /// </summary>
        private static bool WriteXMLShutdownActivationState()
        {
            try
            {   // Write options to XML File by creating XML Serialization of type ShutdownOptions and using text writer to create the actual XML file
                XmlSerializer serializer = new XmlSerializer(typeof(ShutdownActivationState));
                twXML = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + @"\ShutdownActivationState.xml");
                serializer.Serialize(twXML, objShutdownActivationState); // Serialize the BackupOptions object to the TExtWrtire object and write the XML file.
                twXML.Close(); // Close TextWriter object.
                return true;
            }
            catch (XmlException Xex)
            {
                WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to write ShutdownActivationState.xml,Error message: " + Xex.Message);
                if (twXML != null) { twXML.Close(); }
                // If shutdown action is unusable as a shutdown command then set to default of Hibernate
                if (objShutdownActivationState.CurrentShutdownActivationState != "true" || objShutdownActivationState.CurrentShutdownActivationState != "false")
                {
                    objShutdownActivationState.CurrentShutdownActivationState = "true";
                }
                return false;
            }
            catch (Exception eX)
            {
                WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to write ShutdownActivationState.xml,Error message: " + eX.Message);
                if (twXML != null) { twXML.Close(); }
                // If shutdown action is unusable as a shutdown command then set to default of Hibernate
                if (objShutdownActivationState.CurrentShutdownActivationState != "true" || objShutdownActivationState.CurrentShutdownActivationState != "false")
                {
                    objShutdownActivationState.CurrentShutdownActivationState = "true";
                }
                return false;
            }
        }

        private static void serializer_UnknownNode(object sender, XmlNodeEventArgs e)
        {
            // Does nothing but wont stop reading on unknown attribute
            ;
        }

        private static void serializer_UnknownAttribute(object sender, XmlAttributeEventArgs e)
        {
            // Does nothing but wont stop reading on unknown attribute
            ;
        }

        #endregion XML Functions

        #region Log Functions

        /// <summary>
        /// Write data to the application log
        /// </summary>
        /// <param name="strLogString">The string variable to be written to the log</param>
        public static void WriteToLog(string strLogFilePath, string strLogString)
        {
            // if file is over 5 Mb then delete and start new log
            try
            {
                FileInfo FITmp = new FileInfo(strLogFilePath);
                if (FITmp.Length > 5 * 1024 * 1024) { File.Delete(strLogFilePath); }
            }
            catch
            {

            } 

            lock (lockWriteLog) // Lock this section of code to prevent different threads accessing log file at same time
            {
                StreamWriter swLog = new StreamWriter(strLogFilePath, true);
                try
                {
                    if (File.Exists(strLogFilePath) == false)
                    {
                        swLog = File.CreateText(strLogFilePath);
                    }
                    swLog.AutoFlush = true;
                    swLog.WriteLine(strLogString);
                    swLog.Close();
                }
                catch
                {
                    swLog.Close(); // Cannot write to file so catch error and continue
                }
            }
        }

        #endregion Log Functions

        #region Properties

        public static bool HibernateSystem
        {
            get { return blShutdownSystem; }
            set { blShutdownSystem = value; }
        }

        public static string ApplicationFolder
        {
            get { return strAppFolderPath; }
            set { strAppFolderPath = value; }
        }

        public static string LogFileName
        {
            get { return strLogFileName; }
            set { strLogFileName = value; }
        }

        public static string ShutdownActiavtionStateFileName
        {
            get { return strShutdownActivationStateFileName; }
            set { strShutdownActivationStateFileName = value; }
        }

        #endregion Properties
    }
}
