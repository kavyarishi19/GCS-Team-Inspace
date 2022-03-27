using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.IO.Ports;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;

namespace GCS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public partial class MainWindow : Window
    {
        // Port and File
        public SerialPort port = new SerialPort();
        StreamWriter file;
        
        string filePath = @"C:\Users\ISHWARENDRA\source\repos\GCS-Team-Inspace\GCS\Resources\csvFiles\";
        string fileT = @"Flight_1014_T.csv";
        string fileC = @"Flight_1014_C.csv";

        // Flight State Variables
        public int f1 = 0, f2 = 0, f3 = 0, f4 = 0, f5 = 0, f6 = 0, fl = 0; // fL and f1 don't get confused 
        private bool already_calibrated = false;

        // Data Struct
        public new struct packets
        {
            public int teamID, missiontime, packetcount, pressure, flightstate, pitch, roll;
            public decimal temperature, voltage, gpsalt, gpslat, gpslong, altitude;
        };
        
        // Data
        public packets data;

        // Plotting Charts Data

        public MainWindow()
        {
            InitializeComponent();

            // Setup Date and Time of Mission
            DispatcherTimer timer = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.Normal, delegate
            {
                this.MissionTimeButton.Content = DateTime.Now.ToUniversalTime().ToString("HH:mm:ss");
                this.MissionDateButton.Content = DateTime.Now.ToUniversalTime().ToString("d");
            }, this.Dispatcher);

            // Add Ports to Port Combo Box
            foreach (string s in SerialPort.GetPortNames())
                PortComboBox.Items.Add(s);

            // Enable Data terminal and request to send
            this.port.DtrEnable = true;
            this.port.RtsEnable = true;
            try
            {
                Trace.WriteLine("Port Opened!");
                this.port.Open();
            }
            catch {
                Trace.WriteLine("Cannot open port!");
            };

            this.port.DataReceived += new SerialDataReceivedEventHandler(this.Port_DataReceived);
        }

        private void PressureChart_SourceUpdated(object sender, System.Windows.Data.DataTransferEventArgs e)
        {

        }

        private delegate void EventHandle();

        private void Form1_Load(object sender, EventArgs e)
        {
            file = new StreamWriter(filePath + fileC);
            file.WriteLine("Team ID,Mission Time (seconds),Packet Count,Altitude (m),Pressure (Pa),Temperature (C),Voltage (V),GPS Time (HH:MM:SS),GPS Latitude (Degrees),GPS Longitude (Degrees),GPS Altitude (m), GPS Sats.,Pitch (Degrees),Roll (Degrees),Blade Spin Rate (RPM),Software State, Camera Direction (Degrees)");
            file.Close();
        }

        private void rtb_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            rtb.ScrollToEnd();
        }

        private void CommandSendButton_Click(object sender, RoutedEventArgs e)
        {
            var s = CommandBoxTextBox.Text.ToString();

            if (s.Length == 0)
                return;

            string first = s[0].ToString();

            try
            {
                if ((first == "c") || (first == "C") && (!already_calibrated))
                {
                    already_calibrated = true;
                    port.Write("$CALIBRATE#");
                }
            }
            catch 
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    rtb.AppendText("❌ Error Calibrating!");
                }));
            }
            if (first == "r" || first == "R")
            {
                port.Write("#RESTART$");
                file = new StreamWriter(filePath + fileC, true);
                file.WriteLine("RESTART");
                file.Close();
            }
        }

        private void ConnectArduinoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                port.PortName = PortComboBox.Text;

                try
                {
                    port.Open();
                }
                catch { }
                
                rtb.AppendText("✅ Connection Established to GCS Arduino.\n");
            }
            catch
            {
                rtb.AppendText("❌ Error Connnecting to GCS Arduino.\n");
            }
        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string packet = "";

            Dispatcher.Invoke(new Action(() => { rtb.AppendText("✅ Received!\n"); }));
            fl++;

            if (fl < 4)
            {
                var i = port.ReadExisting().ToString();
                return;
            }

            // Break when # is encountered
            while (true)
            {
                var c = port.ReadChar();
                if (c == '#')
                    break;
            }

            packet = port.ReadTo("$");

            if (packet.Length < 4)
            {
                data.flightstate = Convert.ToInt32(packet);
                goto FLIGHTSTATE;
            }

            else if (packet.Length > 40)
            {
                // Edit According to the data received
                string[] pack = packet.Split(',');

                data.missiontime = Convert.ToUInt16(pack[1]);

                data.packetcount = Convert.ToInt32(pack[2]);
                data.altitude = Convert.ToDecimal(pack[3]);
                data.pressure = Convert.ToInt32(pack[4]);
                data.temperature = Convert.ToDecimal(pack[5]);
                data.voltage = Convert.ToDecimal(pack[6]);
                data.gpslat = Convert.ToDecimal(pack[8]);
                data.gpslong = Convert.ToDecimal(pack[9]);
                data.gpslong = Convert.ToDecimal(pack[10]);
                data.roll = Convert.ToInt32(pack[12]);
                data.pitch = Convert.ToInt32(pack[13]);
                data.flightstate = Convert.ToInt32(pack[15]);

                Dispatcher.Invoke(new Action(() =>
                {
                    ContainerDataGrid.Items.Add(pack);
                }));

                file = new StreamWriter(filePath + fileC, true);
                file.WriteLine(packet);
                file.Close();

                // Plotting Check Here
            }

            FLIGHTSTATE:
            switch (data.flightstate)
            {
                case 2:
                    CalibratedEllipse.Fill = new SolidColorBrush(Colors.Green);

                    if (f2 == 0)
                    {
                        Dispatcher.Invoke(new Action(() =>
                        {
                            rtb.AppendText("✅ Calibrated.. Waiting for Launch.\n");
                        }));
                        f2++;
                    }

                    break;

                case 3:
                    LaunchEllipse.Fill = new SolidColorBrush(Colors.Green);

                    if (f3 == 0)
                    {
                        Dispatcher.Invoke(new Action(() =>
                        {
                            rtb.AppendText("✅ Launch Detected.. Ascending.\n");
                        }));
                        f3++;
                    }

                    break;

                case 4:
                    ParachuteDeployedEllipse.Fill = new SolidColorBrush(Colors.Green);

                    if (f4 == 0)
                    {
                        Dispatcher.Invoke(new Action(() =>
                        {
                            rtb.AppendText("✅ Parachute Deployed.. Descending.\n");
                        }));
                        f4++;
                    }

                    break;

                
                case 5:
                    PayloadReleasedEllipse.Fill = new SolidColorBrush(Colors.Green);

                    if (f5 == 0)
                    {
                        Dispatcher.Invoke(new Action(() =>
                        {
                            rtb.AppendText("✅ Parachute Deployed.. Descending.\n");
                        }));
                        f5++;
                    }

                    break;

                case 6:
                    LandedEllipse.Fill = new SolidColorBrush(Colors.Green);

                    if (f6 == 0)
                    {
                        Dispatcher.Invoke(new Action(() =>
                        {
                            rtb.AppendText("✅ Landed!! 🎉\n");
                        }));
                        f6++;
                    }

                    break;

                default:
                    break;
            }
        }
    }

    #region Data Template for Payload and Container Data
    //public class PayloadData
    //{
    //    public int S_NO { get; set; } = 1;
    //    public string TEAM_ID { get; set; } = "1014";
    //    public string MISSION_TIME { get; set; } = "00:00:00";
    //    public string PACKET_COUNT { get; set; } = "1";
    //    public string PACKET_TYPE { get; set; } = "T";
    //    public string TP_ALTITUDE { get; set; } = "0";
    //    public string TP_TEMP { get; set; } = "0";
    //    public string TP_VOLTAGE { get; set; }= "0";
    //    public string GYRO_R { get; set; }    = "0";
    //    public string GYRO_P { get; set; }    = "0";
    //    public string GYRO_Y { get; set; }    = "0";
    //    public string ACCEL_R { get; set; }   = "0";
    //    public string ACCEL_P { get; set; }   = "0";
    //    public string ACCEL_Y { get; set; }   = "0";
    //    public string MAG_R { get; set; }     = "0";
    //    public string MAG_P { get; set; }     = "0";
    //    public string MAG_Y { get; set; }     = "0";
    //    public string POINTING_ERROR { get; set; } = "0";
    //    public string TP_SOFTWARE_STATE { get; set; } = "0";
    //}
    //public class ContainerData
    //{
    //    public int S_NO { get; set; } = 1;
    //    public string TEAM_ID { get; set; } = "1014";
    //    public string MISSION_TIME { get; set; } = "00:00:00";
    //    public string PACKET_COUNT { get; set; } = "1";
    //    public string PACKET_TYPE { get; set; } = "C";
    //    public string MODE { get; set; } = "0";
    //    public string TP_RELEASED { get; set; }   = "0";
    //    public string ALTITUDE { get; set; }  = "0";
    //    public string TEMP { get; set; }   = "0";
    //    public string VOLTAGE { get; set; }  = "0";
    //    public string GPS_TIME { get; set; }  = "0";
    //    public string GPS_LATITUDE { get; set; }  = "0";
    //    public string GPS_LONGITUDE { get; set; } = "0";
    //    public string GPS_ALTITUDE { get; set; }  = "0";
    //    public string GPS_SATS { get; set; }  = "0";
    //    public string SOFTWARE_STATE { get; set; }= "0";
    //    public string CMD_ECHO { get; set; } = "0";
    //}

    #endregion

    #region Container and PayLoad Data Load in GCS

    // Set Container and Payload Data to show up in GCS (Tab-3 and Tab-4)
    //    public class DataListSetUp
    //    {
    //        public List<PayloadData> payloadData { get; set; } = SetPayloadData();
    //        public List<ContainerData> containerData { get; set; } = SetContainerData();

    //        public static int CurrentContainerLineNum = 0;
    //        public static int CurrentPayloadLineNum = 0;

    //        public static List<PayloadData> SetPayloadData()
    //        {
    //            // Local Path of File
    //            string filePath = @"C:\Users\ISHWARENDRA\source\repos\GCS-Team-Inspace\GCS\Resources\csvFiles\";
    //            string fileName = @"Flight_1014_T.csv";

    //            string file = filePath + fileName;

    //            var list = new List<PayloadData>();
    //            if (File.Exists(file))
    //            {
    //                var lines = File.ReadAllLines(file);
    //                for (int i = 1; i < lines.Length; i++)
    //                {
    //                    var line = lines[i].Split(',');
    //                    PayloadData data = new PayloadData()
    //                    {
    //                        // TODO: Change needy data to integer and keep others as string 
    //                        S_NO = i,
    //                        TEAM_ID = line[0],
    //                        MISSION_TIME = line[1],
    //                        PACKET_COUNT = line[2],
    //                        PACKET_TYPE = line[3],
    //                        TP_ALTITUDE = line[4],
    //                        TP_TEMP = line[5],
    //                        TP_VOLTAGE = line[6],
    //                        GYRO_R = line[7],
    //                        GYRO_P = line[8],
    //                        GYRO_Y = line[9],
    //                        ACCEL_R = line[10],
    //                        ACCEL_P = line[11],
    //                        ACCEL_Y = line[12],
    //                        MAG_R = line[13],
    //                        MAG_P = line[14],
    //                        MAG_Y = line[15],
    //                        POINTING_ERROR = line[16],
    //                        TP_SOFTWARE_STATE = line[17]
    //                    };

    //                    CurrentPayloadLineNum += 1;

    //                    list.Add(data);
    //                }
    //            }
    //            else
    //            {
    //                File.Create(file);
    //            }

    //            //Trace.WriteLine("Added Payload Data!");

    //            return list;
    //        }
    //        public static List<ContainerData> SetContainerData()
    //        {
    //            string filePath = @"C:\Users\ISHWARENDRA\source\repos\GCS-Team-Inspace\GCS\Resources\csvFiles\";
    //            string fileName = @"Flight_1014_C.csv";

    //            string file = filePath + fileName;

    //            var list = new List<ContainerData>();
    //            if (File.Exists(file))
    //            {
    //                var lines = File.ReadAllLines(file);
    //                for (int i = 1; i < lines.Length; i++)
    //                {
    //                    var line = lines[i].Split(',');
    //                    ContainerData data = new ContainerData()
    //                    {
    //                        // TODO: Change needy data to integer and keep others as string 
    //                        S_NO = i,
    //                        TEAM_ID = line[0],
    //                        MISSION_TIME = line[1],
    //                        PACKET_COUNT = line[2],
    //                        PACKET_TYPE = line[3],
    //                        MODE = line[4],
    //                        TP_RELEASED = line[5],
    //                        ALTITUDE = line[6],
    //                        TEMP = line[7],
    //                        VOLTAGE = line[8],
    //                        GPS_TIME = line[9],
    //                        GPS_LATITUDE = line[10],
    //                        GPS_LONGITUDE = line[11],
    //                        GPS_ALTITUDE = line[12],
    //                        GPS_SATS = line[13],
    //                        SOFTWARE_STATE = line[14],
    //                        CMD_ECHO = line[15]
    //                    };

    //                    CurrentContainerLineNum += 1;

    //                    list.Add(data);
    //                }
    //            }
    //            else
    //            {
    //                File.Create(file);
    //            }

    //            //Trace.WriteLine("Added Container Data!");
    //            return list;
    //        }

    //        public static void RefreshContainerData1()
    //        {
    //            string filePath = @"C:\Users\ISHWARENDRA\source\repos\GCS-Team-Inspace\GCS\Resources\csvFiles\";
    //            string fileName = @"Flight_1014_C.csv";

    //            string file = filePath + fileName;

    //            var list = new List<ContainerData>();

    //            //SetContainerData();
    //            if (File.Exists(file))
    //            {
    //                var lines = File.ReadAllLines(file);
    //                for (int i = 1; i < lines.Length; i++)
    //                {
    //                    var line = lines[i].Split(',');
    //                    ContainerData data = new ContainerData()
    //                    {
    //                        // TODO: Change needy data to integer and keep others as string 
    //                        S_NO = i,
    //                        TEAM_ID = line[0],
    //                        MISSION_TIME = line[1],
    //                        PACKET_COUNT = line[2],
    //                        PACKET_TYPE = line[3],
    //                        MODE = line[4],
    //                        TP_RELEASED = line[5],
    //                        ALTITUDE = line[6],
    //                        TEMP = line[7],
    //                        VOLTAGE = line[8],
    //                        GPS_TIME = line[9],
    //                        GPS_LATITUDE = line[10],
    //                        GPS_LONGITUDE = line[11],
    //                        GPS_ALTITUDE = line[12],
    //                        GPS_SATS = line[13],
    //                        SOFTWARE_STATE = line[14],
    //                        CMD_ECHO = line[15]
    //                    };

    //                    CurrentContainerLineNum += 1;

    //                    list.Add(data);
    //                }
    //            }
    //            Trace.WriteLine("Refreshed Container Data!");
    //        }
    //    }
    #endregion
}
