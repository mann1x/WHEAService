using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Management;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Security.Principal;
using System.Collections;

namespace WHEAservice
{

    public class EventSource
    {
        private int esType;
        private string esDescription;
        public EventSource(int EsType, string EsDescription)
        {
            esType = EsType;
            esDescription = EsDescription;
        }
        public int EsType
        {
            get { return esType; }
            set { esType = value; }
        }
        public string EsDescription
        {
            get { return esDescription; }
            set { esDescription = value; }
        }
    }
    public class EventSources : IEnumerable
    {
        private EventSource[] eslist;

        public EventSources()
        {
            eslist = new EventSource[17]
            {
                new EventSource(0,"WheaErrSrcTypeMCE = 0x00, Machine Check Exception"),
                new EventSource(1,"WheaErrSrcTypeCMC = 0x01, Corrected Machine Check"),
                new EventSource(2,"WheaErrSrcTypeCPE = 0x02, Corrected Platform Error"),
                new EventSource(3,"WheaErrSrcTypeNMI = 0x03, Non-Maskable Interrupt"),
                new EventSource(4,"WheaErrSrcTypePCIe = 0x04, PCI Express Error"),
                new EventSource(5,"WheaErrSrcTypeGeneric = 0x05, Other types of error sources"),
                new EventSource(6,"WheaErrSrcTypeINIT = 0x06, IA64 INIT Error Source"),
                new EventSource(7,"WheaErrSrcTypeBOOT = 0x07, BOOT Error Source"),
                new EventSource(8,"WheaErrSrcTypeSCIGeneric = 0x08, SCI-based generic error source"),
                new EventSource(9,"WheaErrSrcTypeIPFMCA = 0x09, Itanium Machine Check Abort"),
                new EventSource(10,"WheaErrSrcTypeIPFCMC = 0x0a, Itanium Machine check"),
                new EventSource(11,"WheaErrSrcTypeIPFCPE = 0x0b, Itanium Corrected Platform Error"),
                new EventSource(12,"WheaErrSrcTypeGenericV2 = 0x0c, Other types of error sources v2"),
                new EventSource(13,"WheaErrSrcTypeSCIGenericV2 = 0x0d, SCI-based GHESv2"),
                new EventSource(14,"WheaErrSrcTypeBMC = 0x0e, BMC error info"),
                new EventSource(15,"WheaErrSrcTypePMEM = 0x0f, ARS PMEM Error Source"),
                new EventSource(16,"WheaErrSrcTypeDeviceDriver = 0x10, Device Driver Error Source"),
            };
        }
        public IEnumerable<EventSource> GetAllSources()
        {
            return eslist;
        }
        private class MyEnumerator : IEnumerator
        {
            public EventSource[] eslist;
            int position = -1;

            public MyEnumerator(EventSource[] list)
            {
                eslist = list;
            }
            private IEnumerator getEnumerator()
            {
                return (IEnumerator)this;
            }
            public bool MoveNext()
            {
                position++;
                return (position < eslist.Length);
            }
            public void Reset()
            {
                position = -1;
            }
            public object Current
            {
                get
                {
                    try
                    {
                        return eslist[position];
                    }
                    catch (IndexOutOfRangeException)
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
        }
        public IEnumerator GetEnumerator()
        {
            return new MyEnumerator(eslist);
        }
    }
    class NotElevated : Exception
    {
        public NotElevated(string message)
        {
        }
    }
    public partial class WHEAService : ServiceBase
    {
        private static EventLog eventLog2;
        static string logName = "WHEAService";
        static string logSource = "WHEAServiceLog";

        public enum ServiceState
        {
            SERVICE_STOPPED = 0x00000001,
            SERVICE_START_PENDING = 0x00000002,
            SERVICE_STOP_PENDING = 0x00000003,
            SERVICE_RUNNING = 0x00000004,
            SERVICE_CONTINUE_PENDING = 0x00000005,
            SERVICE_PAUSE_PENDING = 0x00000006,
            SERVICE_PAUSED = 0x00000007,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ServiceStatus
        {
            public int dwServiceType;
            public ServiceState dwCurrentState;
            public int dwControlsAccepted;
            public int dwWin32ExitCode;
            public int dwServiceSpecificExitCode;
            public int dwCheckPoint;
            public int dwWaitHint;
        };

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(System.IntPtr handle, ref ServiceStatus serviceStatus);
        public bool InitializeEventLog()
        {
            
            try
            {
                bool isElevated;

                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
                }

                if (!isElevated) throw new NotElevated(message:"Administrative privileges required");

            }
            catch(Exception e)
            {
                Console.Write(e.Message);
                return false;
            }


            eventLog1 = new EventLog();

            string logByName;

            if (EventLog.SourceExists(logName))
            {
                logByName = EventLog.LogNameFromSourceName(logName, ".");

                if (logByName != logSource) {
                    Console.WriteLine("LogSource=" + logByName);
                    EventLog.DeleteEventSource(logName);
                    if (logByName != "Application" && logByName != "System") { 
                        EventLog.Delete(logByName);
                        Console.WriteLine("LogSource=" + logByName + " deleted.");
                    } else
                    {
                        Console.WriteLine("LogSource=" + logByName + " cannot be deleted.");
                    }

                }

            }
            else
            {
                EventLog.CreateEventSource(logName, logSource);
            }
            eventLog1.Source = logName;
            eventLog1.Log = logSource;

            return true;
        }

        public WHEAService()
        {
            bool initLog;
            AutoLog = false;
            InitializeComponent();
            initLog = InitializeEventLog();
            if (!initLog)
            {
                var timer = new System.Threading.Timer(async (e) =>
                {
                    await Task.Delay(500);
                    StopService();
                }, null, 0, 5000);
            }
        }

        public void StartDbg()
        {
            bool initLog;
            InitializeComponent();
            initLog = InitializeEventLog();
            string[] args = new string[0];
            if (initLog) OnStart(args);
        }

        protected override void OnStart(string[] args)
        {
            eventLog1.WriteEntry("WHEAService starting...");
            // Update the service state to Start Pending.
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            WMI WHEA = new WMI();
            
            ManagementScope Scope = null;

            EventSources esClass = new EventSources();


            bool connected = false;

            int[] esArray = { 16, 0, 1, 3 , 7 };

            var sb = new System.Text.StringBuilder();

            sb.AppendLine("Error Sources targeted to be disabled:");
            sb.AppendLine("");

            foreach (int targetID in esArray)
            {
                bool bFound = false;
                string eDescription = "";
                foreach (var element in esClass.GetAllSources())
                {
                    if (targetID == element.EsType)
                    {
                        eDescription = element.EsDescription;
                        bFound = true;
                    }
                }
                if (!bFound)
                {
                    eDescription = "not found";
                }
                sb.AppendLine("Type: " + String.Format("{0,2}", targetID.ToString()) + " Description: " + eDescription);
            }

            EventMsg(sb.ToString());

            sb = null;

            try
            { 
                Scope = WHEA.Connect();
                connected = true;
            }
            catch
            {
                EventErr("Failed to connect to WMI, exception raised");
            }


            if (connected)
            {

                WHEA.GetSourceInfo(Scope, -999);

                int esIDret;
                int esState;
                bool disabled = false; 
                foreach (int esType in esArray)
                {
                    disabled = false;
                    esIDret = -1;

                    (esIDret, esState) = WHEA.GetSourceInfo(Scope,esType);
                    
                    if (esIDret == -1) {
                        EventMsg("WHEA error source type " + esType + " not found");
                        continue;
                    }

                    if (esState > 1)
                        EventMsg("Disabling WHEA error source type " + esType + " ID=" + esIDret);


                    for (int i = 0; i < 5; i++)
                    {
                        if (esState == 1)
                        {
                            EventMsg("Successfully disabled WHEA error source type " + esType + " ID=" + esIDret);
                            disabled = true;
                            i = 45;
                            continue;
                        }
                        else
                        {

                            try
                            {
                                esState = WHEA.DisableSource(Scope, esIDret);
#if DEBUG
                                EventMsg("SourceID=" + esIDret + " Status=" + esState);
#endif

                            }
                            catch
                            {
                                EventErr("Failed DisableSource, exception raised");
                            }

                        }

                        Thread.Sleep(1000);

                        if (esState == 5)
                            (esIDret, esState) = WHEA.GetSourceInfo(Scope, esType);

                    }

                    if (!disabled) EventMsg("Failed to disable WHEA error source type " + esType + " ID=" + esIDret);

                }

                WHEA.GetSourceInfo(Scope, -999);

            }

            var timer = new System.Threading.Timer(async (e) =>
            {
                await Task.Delay(500);
                StopService();
            }, null, 0, 5000);

        }

        protected void StopService()
        {
            System.ServiceProcess.ServiceController svc = new System.ServiceProcess.ServiceController("WHEAService");
            svc.Stop();
        }

        protected override void OnStop()
        {
            eventLog1.WriteEntry("WHEAService stopping");
            ServiceStatus serviceStatus = new ServiceStatus();
            // Update the service state to Stopped.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED;
            serviceStatus.dwWaitHint = 100000;
            serviceStatus.dwWin32ExitCode = 0;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }

        public static void EventMsg(string msg)
        {
            eventLog2 = new EventLog();
            eventLog2.Source = logName;
            eventLog2.Log = logSource;
            eventLog2.WriteEntry(msg);
            Console.WriteLine("INFO " + msg);
        }

        public static void EventErr(string msg)
        {
            eventLog2 = new EventLog();
            eventLog2.Source = "WHEAService";
            eventLog2.Log = "WHEAServiceLog";
            eventLog2.WriteEntry(msg, EventLogEntryType.Error);
            Console.WriteLine("ERROR " + msg);
        }

    }

    class WMI
    {

        public ManagementScope Connect()
        {
            try
            {
                return new ManagementScope(@"root\WMI");
            }
            catch (System.Management.ManagementException e)
            {
                WHEAservice.WHEAService.EventErr("Failed to connect to WMI: " + e.Message);
                throw;
            }
        }
        public (int,int) GetSourceInfo(ManagementScope scope, int esType)
        {
            try
            {
                ManagementObject obj = new ManagementObject();
                ManagementPath path = new ManagementPath(scope.Path + ":WHEAErrorSourceMethods.InstanceName='WHEA_WMI_PROVIDER0'");

                obj.Path = path;
                obj.Get();

                ManagementBaseObject inParams =
                    obj.GetMethodParameters("GetAllErrorSourcesRtn");

                ManagementBaseObject outParams =
                        obj.InvokeMethod("GetAllErrorSourcesRtn",
                        inParams, null);

                int sCount = int.Parse(outParams["Count"].ToString());
                int sLength = int.Parse(outParams["Length"].ToString());

                int Length;
                int Version;
                int TypeEs;
                int State;
                int MaxRawDataLength;
                int NumRecordsToPreallocate;
                int MaxSectionsPerRecord;
                int ErrorSourceId;
                int PlatformErrorSourceId;
                int Flags;
                byte[] Info;
                int InfoSeek;

                var sb = new System.Text.StringBuilder();
                EventSources esClass = new EventSources();
                BinaryFormatter bf = new BinaryFormatter();

                string GetState(int state)
                {
                    switch (state) { 
                        case 1:
                            return String.Format("{0,-13}", "Stopped");
                        case 2:
                            return String.Format("{0,-13}", "Started");
                        case 3:
                            return String.Format("{0,-13}", "Removed");
                        case 4:
                            return String.Format("{0,-13}", "RemovePending");
                        default:
                            return String.Format("{0,-13}", "Unknown");
                    }
                }

                using (var ms = new MemoryStream())
                {
                    ms.Write((byte[])outParams["ErrorSourceArray"], 0, sLength);
#if DEBUG
                    WHEAservice.WHEAService.EventErr("MS: " + ms.Length.ToString());
#endif

                    if (esType == -999) {
                        sb.AppendLine("Error Sources count is " + sCount.ToString() + ", current status:");
                        sb.AppendLine("");
                    }

                    ms.Position = 0;
                    using (BinaryReader reader = new System.IO.BinaryReader(ms))
                    {
                        for (int i = 0; i < sCount; i++)
                        {
                            Length = reader.ReadInt32();
                            InfoSeek = int.Parse(Length.ToString()) - 40;
                            Version = reader.ReadInt32();
                            TypeEs = reader.ReadInt32();
                            State = reader.ReadInt32();
                            MaxRawDataLength = reader.ReadInt32();
                            NumRecordsToPreallocate = reader.ReadInt32();
                            MaxSectionsPerRecord = reader.ReadInt32();
                            ErrorSourceId = reader.ReadInt32();
                            PlatformErrorSourceId = reader.ReadInt32();
                            Flags = reader.ReadInt32();
                            Info = reader.ReadBytes(InfoSeek);

#if DEBUG
                            WHEAservice.WHEAService.EventErr(i + " Length: " + Length.ToString());
                            WHEAservice.WHEAService.EventErr(i + " Version: " + Version.ToString());
                            WHEAservice.WHEAService.EventErr(i + " Type: " + TypeEs.ToString());
                            WHEAservice.WHEAService.EventErr(i + " State: " + State.ToString());
                            WHEAservice.WHEAService.EventErr(i + " MaxRawDataLength: " + MaxRawDataLength.ToString());
                            WHEAservice.WHEAService.EventErr(i + " NumRecordsToPreallocate: " + NumRecordsToPreallocate.ToString());
                            WHEAservice.WHEAService.EventErr(i + " MaxSectionsPerRecord: " + MaxSectionsPerRecord.ToString());
                            WHEAservice.WHEAService.EventErr(i + " ErrorSourceId: " + ErrorSourceId.ToString());
                            WHEAservice.WHEAService.EventErr(i + " PlatformErrorSourceId: " + PlatformErrorSourceId.ToString());
                            WHEAservice.WHEAService.EventErr(i + " Flags: " + Flags.ToString());
                            WHEAservice.WHEAService.EventErr(i + " Info Length: " + Info.Length.ToString());
#endif
                            if (esType == -999)
                            {
                                bool bFound = false;
                                string eDescription = "";
                                foreach (var element in esClass.GetAllSources()) {
                                    if (TypeEs == element.EsType)
                                    {
                                        eDescription = element.EsDescription;
                                        bFound = true;
                                    }
                                }
                                if (!bFound)
                                {
                                    eDescription = "not found";
                                }
                                sb.AppendLine("ID: " + String.Format("{0,2}", ErrorSourceId.ToString()) + " Type: " + String.Format("{0,2}", TypeEs.ToString()) + " State: " + GetState(State) + " Description: " + eDescription);
                            }

                            if (esType == TypeEs) return (int.Parse(ErrorSourceId.ToString()), int.Parse(State.ToString()));

                        }
                    }

                }

                if (esType == -999)
                {
                    WHEAservice.WHEAService.EventMsg(sb.ToString());
                }

                sb = null;

                obj.Dispose();
                

                return (-1, 5);

            }
            catch (ManagementException e)
            {
                WHEAservice.WHEAService.EventErr("Failed GetSourceInfo, WMI exception thrown: " + e.ToString());
                int[,] empty = new int[4, 2];
                return (-1, 5);
            }
            catch (Exception e)
            {
                WHEAservice.WHEAService.EventErr("Failed GetSourceInfo, exception thrown: " + e.ToString());
                return (-1 ,5);
            }
        }

        public int DisableSource(ManagementScope scope, int ErrorSourceId)
        {
            try 
            {
                ManagementObject obj = new ManagementObject();
                ManagementPath path = new ManagementPath(scope.Path + ":WHEAErrorSourceMethods.InstanceName='WHEA_WMI_PROVIDER0'");

                obj.Path = path;
                obj.Get();

                ManagementBaseObject inParams =
                    obj.GetMethodParameters("DisableErrorSourceRtn");

                inParams["ErrorSourceId"] = Int32.Parse(ErrorSourceId.ToString());

                ManagementBaseObject outParams =
                        obj.InvokeMethod("DisableErrorSourceRtn",
                        inParams, null);

                WHEAservice.WHEAService.EventMsg("Status=" + outParams["Status"].ToString());
                
                obj.Dispose();
                
                return int.Parse(outParams["Status"].ToString());

            }
            catch (ManagementException e)
            {
                #if DEBUG
                    WHEAservice.WHEAService.EventErr("Failed DisableSource for " + ErrorSourceId + ", WMI exception thrown: " + e.ToString());
                #endif
                return 5;
            }
            catch (Exception e)
            {
                WHEAservice.WHEAService.EventErr("Failed DisableSource (this can be normal) for " + ErrorSourceId + ", exception thrown: " + e.ToString());
                return 5;
            }
        }

    }

}
