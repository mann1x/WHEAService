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

namespace WHEAservice
{

    public partial class WHEAService : ServiceBase
    {
        private static EventLog eventLog2;

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
        public void InitializeEventLog()
        {
            eventLog1 = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists("WHEAServiceV2"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "WHEAServiceV2", "Application");
            }
            eventLog1.Source = "WHEAServiceV2";
            eventLog1.Log = "Application";
        }

        public WHEAService()
        {
            InitializeComponent();
            InitializeEventLog();
        }

        public void StartDbg()
        {
            InitializeComponent();
            InitializeEventLog();
            string[] args = new string[0];
            OnStart(args);
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

            bool connected = false;

            int[] esArray = { 16, 0, 1, 3 , 7 };

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

                    if (!disabled) EventMsg("Failed to disble WHEA error source type " + esType + " ID=" + esIDret);

                }

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
            eventLog2 = new System.Diagnostics.EventLog();
            eventLog2.Source = "WHEAServiceV2";
            eventLog2.Log = "Application";
            eventLog2.WriteEntry(msg);
            Console.WriteLine("INFO " + msg);
        }

        public static void EventErr(string msg)
        {
            eventLog2 = new System.Diagnostics.EventLog();
            eventLog2.Source = "WHEAServiceV2";
            eventLog2.Log = "Application";
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
                int Type;
                int State;
                int MaxRawDataLength;
                int NumRecordsToPreallocate;
                int MaxSectionsPerRecord;
                int ErrorSourceId;
                int PlatformErrorSourceId;
                int Flags;
                byte[] Info;
                int InfoSeek;

                BinaryFormatter bf = new BinaryFormatter();
                using (var ms = new MemoryStream())
                {
                    ms.Write((byte[])outParams["ErrorSourceArray"], 0, sLength);
#if DEBUG
                    WHEAservice.WHEAService.EventErr("MS: " + ms.Length.ToString());
#endif

                    ms.Position = 0;
                    using (BinaryReader reader = new System.IO.BinaryReader(ms))
                    {
                        for (int i = 0; i < sCount; i++)
                        {
                            Length = reader.ReadInt32();
                            InfoSeek = int.Parse(Length.ToString()) - 40;
                            Version = reader.ReadInt32();
                            Type = reader.ReadInt32();
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
                            WHEAservice.WHEAService.EventErr(i + " Type: " + Type.ToString());
                            WHEAservice.WHEAService.EventErr(i + " State: " + State.ToString());
                            WHEAservice.WHEAService.EventErr(i + " MaxRawDataLength: " + MaxRawDataLength.ToString());
                            WHEAservice.WHEAService.EventErr(i + " NumRecordsToPreallocate: " + NumRecordsToPreallocate.ToString());
                            WHEAservice.WHEAService.EventErr(i + " MaxSectionsPerRecord: " + MaxSectionsPerRecord.ToString());
                            WHEAservice.WHEAService.EventErr(i + " ErrorSourceId: " + ErrorSourceId.ToString());
                            WHEAservice.WHEAService.EventErr(i + " PlatformErrorSourceId: " + PlatformErrorSourceId.ToString());
                            WHEAservice.WHEAService.EventErr(i + " Flags: " + Flags.ToString());
                            WHEAservice.WHEAService.EventErr(i + " Info Length: " + Info.Length.ToString());
#endif

                            if (Type.ToString() == esType.ToString()) return (int.Parse(ErrorSourceId.ToString()), int.Parse(State.ToString()));

                        }
                    }
                }

                return (-1, 5);

            }
            catch (ManagementException e)
            {
                WHEAservice.WHEAService.EventErr("Failed GetSourceID, WMI exception thrown: " + e.ToString());
                int[,] empty = new int[4, 2];
                return (-1, 5);
            }
            catch (Exception e)
            {
                WHEAservice.WHEAService.EventErr("Failed GetSourceID, exception thrown: " + e.ToString());
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
                    obj.GetMethodParameters("EnableErrorSourceRtn");

                inParams["ErrorSourceId"] = Int32.Parse(ErrorSourceId.ToString());

                ManagementBaseObject outParams =
                        obj.InvokeMethod("DisableErrorSourceRtn",
                        inParams, null);

                WHEAservice.WHEAService.EventMsg("Status=" + outParams["Status"].ToString());
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
                WHEAservice.WHEAService.EventErr("Failed DisableSource for " + ErrorSourceId + ", exception thrown: " + e.ToString());
                return 5;
            }
        }

    }

}
