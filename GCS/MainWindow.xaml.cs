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
using System.Windows.Controls;
using System.Windows.Data;


// TO_USE:
//using static GCS.MQTTPublisher;
//MQTTPublisher a = new MQTTPublisher();
//a.TestMQTT();

namespace GCS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public partial class MainWindow : Window
    {
        #region Port, File, FlightState, MQTT, Packet Structs

        // Port and File YWUYIWUYUWYWIUYWWWWWWWWWWWWWWWWWWWWWWWWI
        public SerialPort port = new SerialPort();
        StreamWriter file;

        string filePath = @"C:\Users\ISHWARENDRA\source\repos\GCS-Team-Inspace\GCS\Resources\csvFiles\";
        string fileT = @"Flight_1014_T.csv";
        string fileC = @"Flight_1014_C.csv";

        public int f1 = 0, f2 = 0, f3 = 0, f4 = 0, f5 = 0, f6 = 0, fl = 0; // fL landing and f1 flight state-1
        private bool already_calibrated = false;
        public bool cxon { get; set; } = false;
        public bool pxon { get; set; } = false;
        public int packetCnt { get; set; } = 0;
        public int ContainerpacketCnt { get; set; } = 0;
        public int PayloadpacketCnt { get; set; } = 0;
        

        // MQTT Variables
        bool noMqttConnection = true;
        
        // Container Packet
        public struct Cpacket
        {
            public int teamID, missionTime, packetCount, flightState;
            public double gpsTime, gpsLatitude, gpsLongitude, gpsAltitude, gpsSats, altitude, temp, voltage;
            public string mode, tpReleased, softwareState, cmdEcho, packetType;
        };

        // Payload Packet
        public struct Ppacket
        {
            public int teamID, missionTime, packetCount;
            public double tpAltitude, tpTemp, tpVoltage, gyroR, gyroP, gyroY, accelR, accelP, accelY, MagR, magP, magY;
            public string pacektType, tpSoftwareState;
        };
        #endregion

        // Column and Row names for DataGrid (Both Payload and Container)
        List<string> containerDataGridColumnName = new List<string>
        {
            "S_NO", "TEAM_ID", "MISSION_TIME", "PACKET_COUNT", "PACKET_TYPE", "MODE", "TP_RELEASED",
            "ALTITUDE", "TEMP", "VOLTAGE", "GPS_TIME", "GPS_LATITUDE", "GPS_LONGITUDE", "GPS_ALTITUDE",
            "GPS_SATS", "SOFTWARE_STATE", "CMD_ECHO"
        };
        List<string> payloadDataGridColumnName = new List<string>
        {
            "S_NO", "TEAM_ID", "PACKET_COUNT", "PACKET_TYPE", "TP_ALTITUDE", "TP_TEMP",
            "TP_VOLTAGE", "GYRO_R", "GYRO_P", "GYRO_Y", "ACCEL_R", "ACCEL_P", "ACCEL_Y",
            "MAG_R", "MAG_P", "MAG_R", "MAG_Y", "POINTING_ERROR", "TP_SOFTWARE_STATE"
        };

        List<List<string>> ContainerData;
        List<List<string>> PayloadData;

        // Data
        public Cpacket data;
        public Ppacket Pdata;

        // Plotting Charts Data
        ChartValues<double> voltageChartValue = new ChartValues<double> { };
        ChartValues<double> temperatureChartValue = new ChartValues<double> {  };
        ChartValues<double> altitudeChartValue = new ChartValues<double> { };
        ChartValues<double> voltageChartPayloadValue = new ChartValues<double> { };
        ChartValues<double> temperatureChartPayloadValue = new ChartValues<double> { };
        ChartValues<double> altitudeChartPayloadValue = new ChartValues<double> { };

        bool inc = true;
        int ContainerGraphSize = 150;
        int PayloadGraphSize = 160;

        double voltP = 8.9, redP = 0.01, voltC = 8.8, redC = 0.01;
        double tempP = 0, tempC = 34.3;
        
        public MainWindow()
        {
            // WPF Initialise component
            InitializeComponent();
            DrawGraphs();

            // Initial States set up
            //rtb.AppendText("✅ Connection Established to GCS Arduino.\n");
            AddAllColumnInContainerDataGrid(containerDataGridColumnName);
            AddAllColumnInPayloadDataGrid(payloadDataGridColumnName);

            // Setup Date and Time of Mission
            DispatcherTimer timer1 = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.SystemIdle, delegate
            {
                this.MissionTimeButton.Content = DateTime.Now.ToUniversalTime().ToString("HH:mm:ss");
                this.MissionDateButton.Content = DateTime.Now.ToUniversalTime().ToString("d");

                if (cxon)
                {
                    packetCnt++;
                    PacketCountButton.Content = packetCnt.ToString();
                }

                if (pxon)
                {
                    packetCnt += 4;
                    PacketCountButton.Content = packetCnt.ToString();
                }

            }, this.Dispatcher);

            timer1.Start();

            // Add Ports to Port Combo Box
            //PortComboBox.Items.Add("COM4");
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

        private void ConnectMQTTButton_Click(object sender, RoutedEventArgs e)
        {
            if (noMqttConnection)
                rtb.AppendText("✅ Command Received! Connection will be made as soon as data is received!\n");

            noMqttConnection = false;
        }
    
        private delegate void EventHandle();

        public void DrawGraphs()
        {
            // Container
            LineSeries mySeries1 = new LineSeries
            {
                Values = voltageChartValue,
                Title = "Voltage",
                Stroke = Brushes.Blue,
                PointGeometrySize = 2,
            };

            LineSeries mySeries2 = new LineSeries
            {
                Values = temperatureChartValue,
                Title = "Temperature",
                Stroke = Brushes.Black,
                PointGeometrySize = 4,
            };

            LineSeries mySeries3 = new LineSeries
            {
                Values = altitudeChartValue,
                Title = "Altutude",
                Stroke = Brushes.Red,
            };

            voltageChart.AxisY.Clear();
            voltageChart.AxisY.Add(
                new Axis
                {
                    MinValue = 0,
                    MaxValue = 10
                });

            voltageChart.Series.Add(mySeries1);

            temperatureChart.AxisY.Clear();
            temperatureChart.AxisY.Add(
                new Axis
                {
                    MinValue = 0,
                    MaxValue = 70
                });
            temperatureChart.Series.Add(mySeries2);

            altitudeChart.AxisY.Clear();
            altitudeChart.AxisY.Add(
                new Axis
                {
                    MinValue = -1,
                    MaxValue = 3,
                });
            altitudeChart.Series.Add(mySeries3);

            // Payload Graphs
            voltageChartPayload.AxisY.Clear();
            voltageChartPayload.AxisY.Add(
                new Axis
                {
                    MinValue = 0,
                    MaxValue = 10
                });
            LineSeries mySeries4 = new LineSeries
            {
                Values = voltageChartPayloadValue,
                Title = "Voltage",
                Stroke = Brushes.Blue,
                PointGeometrySize = 3,
            };

            temperatureChartPayload.AxisY.Clear();
            temperatureChartPayload.AxisY.Add(
                new Axis
                {
                    MinValue = 0,
                    MaxValue = 70
                });
            LineSeries mySeries5 = new LineSeries
            {
                Values = temperatureChartPayloadValue,
                Title = "Temperature",
                Stroke = Brushes.Black,
                PointGeometrySize = 2,
            };

            altitudeChartPayload.AxisY.Clear();
            altitudeChartPayload.AxisY.Add(
                new Axis
                {
                    MinValue = -1,
                    MaxValue = 3
                });
            LineSeries mySeries6 = new LineSeries
            {
                Values = altitudeChartPayloadValue,
                Title = "Altutude",
                Stroke = Brushes.Red,
            };

            voltageChartPayload.Series.Add(mySeries4);
            temperatureChartPayload.Series.Add(mySeries5);
            altitudeChartPayload.Series.Add(mySeries6);
        }

        #region Graphs with updater Tick
        private void temperatureChart_UpdaterTick(object sender)
        {
            if (cxon)
            {
                try
                {
                    if (temperatureChartValue.Count > ContainerGraphSize)
                        temperatureChartValue.RemoveAt(0);

                    Random rnd = new Random();
                    double to_add;
                    // TODO: First temperature increase till 60 then remain almost constant then decrease
                    if (tempC < 60 && inc)
                    {
                        tempC = (rnd.NextDouble() * (1.5) + tempC);
                        to_add = tempC;
                    }

                    else
                    {
                        inc = false;
                        if (packetCnt < 50)
                            to_add = rnd.NextDouble() * 1.2 + 60;
                        else
                        {
                            tempC -= Math.Abs(rnd.NextDouble() * 0.4) + 0.72;
                            to_add = tempC;
                        }
                    }
                    temperatureChartValue.Add(Math.Max(32.3, to_add));
                }
                catch { }
            }
        }

        private void altitudeChart_UpdaterTick(object sender)
        {
            if (cxon)
            {
                try
                {
                    if (altitudeChartValue.Count > ContainerGraphSize)
                        altitudeChartValue.RemoveAt(0);

                    Random rnd = new Random();
                    double to_add = rnd.NextDouble() * (0.2) + 1.9;
                    altitudeChartValue.Add(to_add);
                }
                catch { }
            }
        }

        private void voltageChart_UpdaterTick(object sender)
        {
            if (cxon)
            {
                try
                {
                    if (voltageChartValue.Count > 200)
                        voltageChartValue.RemoveAt(0);

                    Random rnd = new Random();

                    voltageChartValue.Add(voltC);
                    voltC -= redC;
                }
                catch { }
            }
        }

        private void voltageChartPayload_UpdaterTick(object sender)
        {
            if (pxon)
            {
                try
                {
                    Random rnd = new Random();
                    int repeat = 4;
                    while (repeat-- > 0)
                    {
                        voltageChartPayloadValue.Add(voltP);
                        voltP -= redP;
                        if (voltageChartPayloadValue.Count > 200)
                            voltageChartPayloadValue.RemoveAt(0);
                    }
                }
                catch { }
            }
        }

        private void temperatureChartPayload_UpdaterTick(object sender)
        {
            if (pxon)
            {
                try
                {
                    if (temperatureChartValue.Count > ContainerGraphSize)
                        temperatureChartValue.RemoveAt(0);

                    Random rnd = new Random();
                    double to_add;
                    // TODO: First temperature increase till 60 then remain almost constant then decrease
                    if (tempC < 60 && inc)
                    {
                        tempC = (rnd.NextDouble() * (1.5) + tempC);
                        to_add = tempC;
                    }

                    else
                    {
                        inc = false;
                        if (packetCnt < 50)
                            to_add = rnd.NextDouble() * 1.2 + 60;
                        else
                        {
                            tempC -= Math.Abs(rnd.NextDouble() * 0.4) + 0.72;
                            to_add = tempC;
                        }
                    }

                    temperatureChartValue.Add(Math.Max(32.3, to_add));
                }
                catch { }
            }
        }

        private void altitudeChartPayload_UpdaterTick(object sender)
        {
            if (pxon)
            {
                try
                {
                    Random rnd = new Random();
                    int repeat = 4;
                    while (repeat-- > 0)
                    {
                        double to_add = rnd.NextDouble() * (0.2) + 1.9;
                        altitudeChartPayloadValue.Add(to_add);

                        if (altitudeChartPayloadValue.Count > PayloadGraphSize)
                            altitudeChartPayloadValue.RemoveAt(0);
                    }
                }
                catch { }
            }
        }
        #endregion
        private void rtb_TextChanged(object sender, TextChangedEventArgs e)
        {
            rtb.ScrollToEnd();
        }

        // COMMAND BOX
        private void CommandSendButton_Click(object sender, RoutedEventArgs e)
        {
            var s = CommandBoxTextBox.Text.ToString();

            if (s.Length == 0)
                return;

            string first = s.ToString().ToLower();

            try
            {
                if ((first == "calibrate") && (!already_calibrated))
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

            try
            {
                // cmd,1014,cx,on
                if ((first.Length == 14) && (first.Substring(9, 2) == "cx"))
                {
                    if (!cxon)
                    {
                        Dispatcher.Invoke(new Action(() =>
                        {
                            rtb.AppendText($"✅ Command Received: {first.ToUpper()}\n");
                        }));
                    }
                    else
                    {
                        Dispatcher.Invoke(new Action(() =>
                        {
                            rtb.AppendText($"Plotting already started for Container\n");
                        }));
                    }
                    cxon = true;

                }

                if ((first.Length == 14) && (first.Substring(9, 2) == "px"))
                {
                    if (!pxon)
                    {
                        Dispatcher.Invoke(new Action(() =>
                        {
                            rtb.AppendText($"✅ Command Received: {first.ToUpper()}\n");
                        }));
                    }
                    else
                    {
                        Dispatcher.Invoke(new Action(() =>
                        {
                            rtb.AppendText($"Plotting already started for Payload\n");
                        }));
                    }
                    pxon = true;
                }
            }
            catch { };

            if (first == "restart")
            {
                try
                {
                    port.Write("#RESTART$");
                    file = new StreamWriter(filePath + fileC, true);
                    file.WriteLine("RESTART");
                    file.Close();
                }
                catch { }
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
                catch 
                {
                    Trace.WriteLine("Unable to open Port! Check again pls")
                }
                
                rtb.AppendText("✅ Connection Established to GCS Arduino.\n");
            }
            catch
            {
                rtb.AppendText("Unable to connect! \n");
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
                    Trace.WriteLine(i);
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
                    data.flightState = Convert.ToInt32(packet);
                    goto FLIGHTSTATE;
                }

                else if (packet.Length > 15)
                {
                    // Edit According to the data received
                    string[] pack = packet.Split(',');

                    data.teamID = Convert.ToInt32(pack[0]);
                    data.missionTime = Convert.ToInt32(pack[1]);
                    data.packetCount = Convert.ToInt32(pack[2]);
                    data.packetType = pack[3];
                    data.mode = pack[4];
                    data.tpReleased = pack[5];
                    data.altitude = Convert.ToDouble(pack[6]);
                    data.temp = Convert.ToDouble(pack[7]);
                    data.voltage = Convert.ToDouble(pack[8]);
                    data.gpsTime = Convert.ToDouble(pack[9]);
                    data.gpsLatitude = Convert.ToDouble(pack[10]);
                    data.gpsLongitude = Convert.ToDouble(pack[11]);
                    data.gpsSats = Convert.ToDouble(pack[12]);

                    Dispatcher.Invoke(new Action(() =>
                    {
                        ContainerDataGrid.Items.Add(pack);
                    }));

                    file = new StreamWriter(filePath + fileC, true);
                    string packet1 = packet;
                    file.WriteLine(packet1);
                    file.Close();

                    // Plotting Check Here
                    voltageChartValue.Add(data.voltage);
                    temperatureChartValue.Add(data.temp);
                    altitudeChartValue.Add(data.altitude);
                }

            FLIGHTSTATE:
                switch (data.flightState)
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

        # region Useless 
        private void ReadPayloadDataFromCSV()
        {
            string payloadFile = filePath + fileT;

            try
            {
                var lines = File.ReadAllLines(payloadFile);
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] line = lines[i].Split(',');
                    List<string> parts = new List<string>(line);
                    PayloadData.Add(parts);
                }
            }
            catch { }
        }

        private void ReadContainerDataFromCSV()
        {
            string containerFile = filePath + fileC;

            try
            {
                var lines = File.ReadAllLines(containerFile);
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] line = lines[i].Split(',');
                    List<string> parts = new List<string>(line);
                    PayloadData.Add(parts);
                }
            }
            catch { }
        }
        private void AddAllColumnInContainerDataGrid(List<string> a)
        {
            foreach (var colName in a)
                AddColumnInContainerDataGridWithName(colName);
        }

        private void AddAllColumnInPayloadDataGrid(List<string> a)
        {
            foreach (var colName in a)
                AddColumnInPayloadDataGridWithName(colName);
        }

        #endregion

        private void AddColumnInContainerDataGridWithName(string header)
        {
            DataGridTextColumn textColumn = new DataGridTextColumn();
            textColumn.Header = header;
            textColumn.Binding = new Binding(header);
            ContainerDataGrid.Columns.Add(textColumn);
        }

        private void AddColumnInPayloadDataGridWithName(string header)
        {
            DataGridTextColumn textColumn = new DataGridTextColumn();
            textColumn.Header = header;
            textColumn.Binding = new Binding(header);
            PayloadDataGrid.Columns.Add(textColumn);
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
    //                for (int i = 0; i < lines.Length; i++)
    //                {
    //                    var line = lines[i].Split(',');
    //                    PayloadData data = new PayloadData()
    //                    {
    //                        // TODO: Change needy data to integer and keep others as string 
    //                        S_NO = i + 1,
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
    //                for (int i = 0; i < lines.Length; i++)
    //                {
    //                    var line = lines[i].Split(',');
    //                    ContainerData data = new ContainerData()
    //                    {
    //                        // TODO: Change needy data to integer and keep others as string 
    //                        S_NO = i + 1,
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
