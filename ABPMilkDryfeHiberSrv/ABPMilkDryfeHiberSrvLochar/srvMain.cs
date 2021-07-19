#region Using Statements

using System;
using System.ComponentModel;
using System.ServiceModel;
using System.ServiceProcess;
using System.IO;
using Microsoft.Win32;
using System.Xml;
using System.Xml.Serialization;
using System.Threading;

#endregion Using Statements

namespace ABPMilkDryfeHiberSrv
{
    public partial class srvMain : ServiceBase
    {
        #region Variables & Objects

        private System.Timers.Timer timerMain;
        // FileSysteMWatcher variables for options file
        private bool blHasOptionsFileChanged = false;
        private bool blOptionsFileNeedstoBeLoaded = false;

        // Read XML Options file lock object
        private static Object lockReadOptions = new Object();
        private static FileStream fsXML;
        private static TextWriter twXML;
        private FileSystemWatcher fswShutdownOptionsFile;
        private static ShutdownOptions objShutdownOptions = new ShutdownOptions();
        public static string strOptionsFileName = "ShutdownOptions.xml";

        // WCF Objects
        private static ServiceClient clientMilk;
        private static ServiceClient clientDryfe;
        private static Object lockWriteLog = new Object();
        private static string baseAddrMilk = "net.tcp://milk.phys.strath.ac.uk:10101/BackupService";
        private static string baseAddrDryfe = "net.tcp://dryfe.phys.strath.ac.uk:10102/BackupService";
        private static NetTcpBinding binding = new NetTcpBinding(SecurityMode.None);

        // Logging objects
        private static string strAppFolderPath = AppDomain.CurrentDomain.BaseDirectory + @"\";
        public static string strLogFileName = "ABPMilkDryfeHibernateSrvLocharLog.log";

        #endregion Variables & Objects

        #region Constructor

        public srvMain()
        {
            InitializeComponent();

            // Allow Servicebase.OnPowerEvent
            CanHandlePowerEvent = true;
        }

        #endregion Constructor

        #region Override Functions

        protected override void OnStart(string[] args)
        {
            try
            {
                if (ReadXMLShutdownOptions(strAppFolderPath + strOptionsFileName) == false) // Read Backup Options 
                {
                    WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to load ShutdownOptions.xml, using default values.");
                }

                // Configure FilsystemWatcher for options file                
                fswShutdownOptionsFile = new FileSystemWatcher(AppDomain.CurrentDomain.BaseDirectory, strOptionsFileName);
                fswShutdownOptionsFile.Changed += new FileSystemEventHandler(fswOptionsFile_Changed);
                fswShutdownOptionsFile.Created += new FileSystemEventHandler(fswOptionsFile_Created);
                fswShutdownOptionsFile.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
                fswShutdownOptionsFile.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                WriteToLog(strAppFolderPath + strLogFileName, "Error configuring FileSystemWatcher: " + ex.Message);
            }

            try // try to connect to WCFservice on Milk
            {
                // open client
                clientMilk = new ServiceClient(binding, new EndpointAddress(baseAddrMilk));
            }
            catch (Exception ex) // Catch error
            {
                WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to connect to WCF host Milk: " + ex.Message);
            }
            try // try to connect to WCFservice on Dryfe
            {
                // open client
                clientDryfe = new ServiceClient(binding, new EndpointAddress(baseAddrDryfe));
            }
            catch (Exception ex) // Catch error
            {
                WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to connect to WCF host Dryfe: " + ex.Message);
            }

            // Configure timer for checking WCF connection
            timerMain = new System.Timers.Timer(5000);
            timerMain.Elapsed += new System.Timers.ElapsedEventHandler(timerMain_Tick);
            timerMain.Interval = 5000;
            timerMain.Enabled = true;
        }

        protected override void OnStop()
        {
            try
            {
                fswShutdownOptionsFile.EnableRaisingEvents = false;
                if (fswShutdownOptionsFile != null)
                {
                    fswShutdownOptionsFile.Dispose();
                }
            }
            catch (Exception ex)
            {
                WriteToLog(strAppFolderPath + strLogFileName, "Error disposing FileSystemWatcher objects: " + ex.Message);
            }

            timerMain.Enabled = false;

            if (clientMilk != null)
            {
                if (clientMilk.State != CommunicationState.Closed)
                {
                    try
                    {
                        clientMilk.Close();
                    }
                    catch (Exception ex)
                    {
                        WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to close WCF host Milk: " + ex.Message);
                    }
                }
            }
            if (clientDryfe != null)
            {
                if (clientDryfe.State != CommunicationState.Closed)
                {
                    try
                    {
                        clientDryfe.Close();
                    }
                    catch (Exception ex)
                    {
                        WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to close WCF host Dryfe: " + ex.Message);
                    }
                }
            }
        }

        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            if (powerStatus == PowerBroadcastStatus.Suspend)
            {
                try
                {
                    if (clientMilk == null || clientMilk.State != CommunicationState.Opened)
                    {
                        clientMilk = new ServiceClient(binding, new EndpointAddress(baseAddrMilk));
                    }

                    clientMilk.SendShutdown(); // Send shutdown

                    WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Sending shutdown to milk.");

                }
                catch (Exception ex)
                {
                    WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to connect to WCF host Milk during Power change event: " + ex.Message);
                }
                try
                {
                    if (clientDryfe == null || clientDryfe.State != CommunicationState.Opened)
                    {
                        clientDryfe = new ServiceClient(binding, new EndpointAddress(baseAddrDryfe));
                    }

                    clientDryfe.SendShutdown(); // Send shutdown

                    WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Sending shutdown to Dryfe.");

                }
                catch (Exception ex)
                {
                    WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to connect to WCF host Dryfe during Power change event: " + ex.Message);
                }
            }

            return base.OnPowerEvent(powerStatus);
        }

        #endregion Override Functions

        #region WCF Timer Functions

        private void timerMain_Tick(object sender, EventArgs e)
        {
            try
            {
                ReadXMLShutdownOptions(strAppFolderPath + @"\" + strOptionsFileName);
            }
            catch
            {

            }

            try
            {
                if (clientMilk == null || clientMilk.State != CommunicationState.Opened) { clientMilk = new ServiceClient(binding, new EndpointAddress(baseAddrMilk)); }

            }
            catch (Exception ex)
            {
                WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to connect to WCF host Milk: " + ex.Message);
            }
            try
            {
                if (clientDryfe == null || clientDryfe.State != CommunicationState.Opened) { clientDryfe = new ServiceClient(binding, new EndpointAddress(baseAddrDryfe)); }

            }
            catch (Exception ex)
            {
                WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to connect to WCF host Dryfe: " + ex.Message);
            }
        }

        #endregion WCF Timer Functions

        #region FileSystemWatcher Functions

        private void fswOptionsFile_Created(object sender, FileSystemEventArgs e)
        {
            if (blOptionsFileNeedstoBeLoaded == false)
            {
                blOptionsFileNeedstoBeLoaded = true;

                if (blHasOptionsFileChanged == false) // Only run if not already running
                {
                    blHasOptionsFileChanged = true; // Set flag so this function doesn't run while already running                        
                    Thread.Sleep(2000);
                    ShutdownOptions tmpShutdownOptions = new ShutdownOptions(); // Get backup options before ReadXML()
                    if (ReadXMLShutdownOptions(strAppFolderPath + strOptionsFileName) == true)
                    {  // Read options file successfully                            
                        WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Status,Options file loaded");
                        ShutdownOptions tmpBackupOptions1 = new ShutdownOptions();
                    }
                    else // Did not read options file successfully
                    {
                        WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Status,Unable to load options file");
                    }
                    blHasOptionsFileChanged = false; // Set flag so that function can run again
                }

                blOptionsFileNeedstoBeLoaded = false;
            }
        }

        private void fswOptionsFile_Changed(object sender, FileSystemEventArgs e)
        {
            if (blOptionsFileNeedstoBeLoaded == false)
            {
                blOptionsFileNeedstoBeLoaded = true;

                if (blHasOptionsFileChanged == false) // Only run if not already running
                {
                    blHasOptionsFileChanged = true; // Set flag so this function doesn't run while already running                        
                    Thread.Sleep(2000);
                    ShutdownOptions tmpShutdownOptions = new ShutdownOptions(); // Get backup options before ReadXML()
                    if (ReadXMLShutdownOptions(strAppFolderPath + strOptionsFileName) == true)
                    {  // Read options file successfully                            
                        WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Status,Options file loaded");
                        ShutdownOptions tmpShutdownOptions1 = new ShutdownOptions();
                    }
                    else // Did not read options file successfully
                    {
                        WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Status,Unable to load options file");
                    }
                    blHasOptionsFileChanged = false; // Set flag so that function can run again
                }

                blOptionsFileNeedstoBeLoaded = false;
            }
        }

        #endregion FileSystemWatcher Functions

        #region XML Functions

        /// <summary>
        /// Writes all backup job options to the BackupOptions.xml file located in the application folder
        /// </summary>
        /// <param name="Path">The full path to the BackupOptions.xml file</param>
        /// <returns></returns>
        public static bool ReadXMLShutdownOptions(string Path)
        {
            if (File.Exists(Path) == false)
            {
                WriteXMLShutdownOptions();
                return false;
            }

            try
            {   // Read options from XML File by creating XML Serialization of type BackupOptions and text writer to create the actual XML file
                XmlSerializer serializer = new XmlSerializer(typeof(ShutdownOptions));
                /* If the XML document has been altered with unknown nodes or attributes, handle them with the nknownNode and UnknownAttribute events.*/
                serializer.UnknownNode += new XmlNodeEventHandler(serializer_UnknownNode);
                serializer.UnknownAttribute += new XmlAttributeEventHandler(serializer_UnknownAttribute);
                fsXML = new FileStream(Path, FileMode.Open); // Create a FileStream object to read the XML document.
                /* Use the De-serialize method to restore the object's state with data from the XML document. */
                objShutdownOptions = (ShutdownOptions)serializer.Deserialize(fsXML);
                fsXML.Close(); // Dispose filestream object
                // If shutdown action is unusable as a shutdown command then set to default of Hibernate
                if (objShutdownOptions.ShutdownAction != "Hibernate" || objShutdownOptions.ShutdownAction != "Shutdown" || objShutdownOptions.ShutdownAction != "Restart")
                {
                    objShutdownOptions.ShutdownAction = "Hibernate";
                }
                return true;
            }
            catch (XmlException Xex) // Catch XML specific error
            {
                WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to read ShutdownOptions.xml,Error message: " + Xex.Message);
                if (fsXML != null) { fsXML.Close(); }
                // If shutdown action is unusable as a shutdown command then set to default of Hibernate
                if (objShutdownOptions.ShutdownAction != "Hibernate" || objShutdownOptions.ShutdownAction != "Shutdown" || objShutdownOptions.ShutdownAction != "Restart")
                {
                    objShutdownOptions.ShutdownAction = "Hibernate";
                }
                return false;
            }
            catch (Exception eX) // Catch any other error
            {
                WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to read ShutdownOptions.xml,Error message: " + eX.Message);
                if (fsXML != null) { fsXML.Close(); }
                // If shutdown action is unusable as a shutdown command then set to default of Hibernate
                if (objShutdownOptions.ShutdownAction != "Hibernate" || objShutdownOptions.ShutdownAction != "Shutdown" || objShutdownOptions.ShutdownAction != "Restart")
                {
                    objShutdownOptions.ShutdownAction = "Hibernate";
                }
                return false;
            }
        }
        // <summary>
        /// Writes all shutdown options to the ShutdownOptions.xml file located in the application folder.
        /// </summary>
        private static bool WriteXMLShutdownOptions()
        {
            try
            {   // Write options to XML File by creating XML Serialization of type ShutdownOptions and using text writer to create the actual XML file
                XmlSerializer serializer = new XmlSerializer(typeof(ShutdownOptions));
                twXML = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + @"\ShutdownOptions.xml");
                serializer.Serialize(twXML, objShutdownOptions); // Serialize the BackupOptions object to the TExtWrtire object and write the XML file.
                twXML.Close(); // Close TextWriter object.
                return true;
            }
            catch (XmlException Xex)
            {
                WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to write ShutdownOptions.xml,Error message: " + Xex.Message);
                if (twXML != null) { twXML.Close(); }
                // If shutdown action is unusable as a shutdown command then set to default of Hibernate
                if (objShutdownOptions.ShutdownAction != "Hibernate" || objShutdownOptions.ShutdownAction != "Shutdown" || objShutdownOptions.ShutdownAction != "Restart")
                {
                    objShutdownOptions.ShutdownAction = "Hibernate";
                }
                return false;
            }
            catch (Exception eX)
            {
                WriteToLog(strAppFolderPath + strLogFileName, DateTime.Now + ",Error,Unable to write ShutdownOptions.xml,Error message: " + eX.Message);
                if (twXML != null) { twXML.Close(); }
                // If shutdown action is unusable as a shutdown command then set to default of Hibernate
                if (objShutdownOptions.ShutdownAction != "Hibernate" || objShutdownOptions.ShutdownAction != "Shutdown" || objShutdownOptions.ShutdownAction != "Restart")
                {
                    objShutdownOptions.ShutdownAction = "Hibernate";
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
    }
}
