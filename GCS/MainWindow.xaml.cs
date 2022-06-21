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
using static GCS.MQTTPublisher;


// TO_USE:
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
        SerialPort port = new SerialPort();

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
            public int teamID, packetCount, flightState;
            public double gpsTime, gpsLatitude, gpsLongitude, gpsAltitude, gpsSats, altitude, temp, voltage;
            public string mode, missionTime, tpReleased, softwareState, cmdEcho, packetType;
        };

        // Payload Packet
        public struct Ppacket
        {
            public int teamID, packetCount;
            public double tpAltitude, tpTemp, tpVoltage, gyroR, gyroP, gyroY, accelR, accelP, accelY, magR, magP, magY;
            public string packetType, missionTime, pointingError, tpSoftwareState;
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
            "MAG_R", "MAG_P", "MAG_Y", "POINTING_ERROR", "TP_SOFTWARE_STATE"
        };

        List<List<string>> ContainerData;
        List<List<string>> PayloadData;

        // Data
        public Cpacket data;
        public Ppacket Pdata;

        // Plotting Charts Data
        ChartValues<double> voltageChartValue = new ChartValues<double> { };
        ChartValues<double> temperatureChartValue = new ChartValues<double> { };
        ChartValues<double> altitudeChartValue = new ChartValues<double> { };
        ChartValues<double> voltageChartPayloadValue = new ChartValues<double> { };
        ChartValues<double> temperatureChartPayloadValue = new ChartValues<double> { };
        ChartValues<double> altitudeChartPayloadValue = new ChartValues<double> { };

        public MainWindow()
        {
            // WPF Initialise component
            InitializeComponent();
            DrawGraphs();

            // Initial States set up
            //rtb.AppendText("✅ Connection Established to GCS Arduino.\n");
            AddAllColumnInContainerDataGrid(containerDataGridColumnName);
            AddAllColumnInPayloadDataGrid(payloadDataGridColumnName);
            AddAllColumnInCustomDataGrid();

            // Setup Date and Time of Mission
            DispatcherTimer timer1 = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.SystemIdle, delegate
            {
                this.MissionTimeButton.Content = DateTime.Now.ToUniversalTime().ToString("HH:mm:ss");
                this.MissionDateButton.Content = DateTime.Now.ToUniversalTime().ToString("d");

                //if (cxon)
                //{
                //    packetCnt++;
                //    PacketCountButton.Content = packetCnt.ToString();
                //}

                //if (pxon)
                //{
                //    packetCnt += 4;
                //    PacketCountButton.Content = packetCnt.ToString();
                //}

            }, this.Dispatcher);

            timer1.Start();

            // Add Ports to Port Combo Box
            //PortComboBox.Items.Add("COM4");
            foreach (string s in SerialPort.GetPortNames())
                PortComboBox.Items.Add(s);

            // Enable Data terminal and request to send
            port.DtrEnable = true;
            this.port.RtsEnable = true;
            try
            {
                Trace.WriteLine("Port Opened!");
                this.port.Open();
            }
            catch
            {
                Trace.WriteLine("Cannot open port!");
            };

            this.port.DataReceived += new SerialDataReceivedEventHandler(this.Port_DataReceived);
        }

        private void ConnectMQTTButton_Click(object sender, RoutedEventArgs e)
        {
            if (noMqttConnection)
                rtb.AppendText("✅ Command Received!\n");

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
            //if (cxon)
            //{
            //    try
            //    {
            //        if (temperatureChartValue.Count > ContainerGraphSize)
            //            temperatureChartValue.RemoveAt(0);

            //        Random rnd = new Random();
            //        double to_add;
            //        // TODO: First temperature increase till 60 then remain almost constant then decrease
            //        if (tempC < 60 && inc)
            //        {
            //            tempC = (rnd.NextDouble() * (1.5) + tempC);
            //            to_add = tempC;
            //        }

            //        else
            //        {
            //            inc = false;
            //            if (packetCnt < 50)
            //                to_add = rnd.NextDouble() * 1.2 + 60;
            //            else
            //            {
            //                tempC -= Math.Abs(rnd.NextDouble() * 0.4) + 0.72;
            //                to_add = tempC;
            //            }
            //        }
            //        temperatureChartValue.Add(Math.Max(32.3, to_add));
            //    }
            //    catch { }
            //}
        }

        private void altitudeChart_UpdaterTick(object sender)
        {
            //if (cxon)
            //{
            //    try
            //    {
            //        if (altitudeChartValue.Count > ContainerGraphSize)
            //            altitudeChartValue.RemoveAt(0);

            //        Random rnd = new Random();
            //        double to_add = rnd.NextDouble() * (0.2) + 1.9;
            //        altitudeChartValue.Add(to_add);
            //    }
            //    catch { }
            //}
        }

        private void voltageChart_UpdaterTick(object sender)
        {
            //if (cxon)
            //{
            //    try
            //    {
            //        if (voltageChartValue.Count > 200)
            //            voltageChartValue.RemoveAt(0);

            //        Random rnd = new Random();

            //        voltageChartValue.Add(voltC);
            //        voltC -= redC;
            //    }
            //    catch { }
            //}
        }

        private void voltageChartPayload_UpdaterTick(object sender)
        {
            //if (pxon)
            //{
            //    try
            //    {
            //        Random rnd = new Random();
            //        int repeat = 4;
            //        while (repeat-- > 0)
            //        {
            //            voltageChartPayloadValue.Add(voltP);
            //            voltP -= redP;
            //            if (voltageChartPayloadValue.Count > 200)
            //                voltageChartPayloadValue.RemoveAt(0);
            //        }
            //    }
            //    catch { }
            //}
        }

        private void temperatureChartPayload_UpdaterTick(object sender)
        {
            //if (pxon)
            //{
            //    try
            //    {
            //        if (temperatureChartValue.Count > ContainerGraphSize)
            //            temperatureChartValue.RemoveAt(0);

            //        Random rnd = new Random();
            //        double to_add;
            //        // TODO: First temperature increase till 60 then remain almost constant then decrease
            //        if (tempC < 60 && inc)
            //        {
            //            tempC = (rnd.NextDouble() * (1.5) + tempC);
            //            to_add = tempC;
            //        }

            //        else
            //        {
            //            inc = false;
            //            if (packetCnt < 50)
            //                to_add = rnd.NextDouble() * 1.2 + 60;
            //            else
            //            {
            //                tempC -= Math.Abs(rnd.NextDouble() * 0.4) + 0.72;
            //                to_add = tempC;
            //            }
            //        }

            //        temperatureChartValue.Add(Math.Max(32.3, to_add));
            //    }
            //catch { }
            //}
        }

        private void altitudeChartPayload_UpdaterTick(object sender)
        {
            //if (pxon)
            //{
            //    try
            //    {
            //        Random rnd = new Random();
            //        int repeat = 4;
            //        while (repeat-- > 0)
            //        {
            //            double to_add = rnd.NextDouble() * (0.2) + 1.9;
            //            altitudeChartPayloadValue.Add(to_add);

            //            if (altitudeChartPayloadValue.Count > PayloadGraphSize)
            //                altitudeChartPayloadValue.RemoveAt(0);
            //        }
            //    }
            //catch { }
            //}
        }
        #endregion
        private void rtb_TextChanged(object sender, TextChangedEventArgs e)
        {
            rtb.ScrollToEnd();
        }

        // COMMAND BOX (ALL PORT COMMAND MUST START AT $ and end with NULL character \0)
        private void CommandSendButton_Click(object sender, RoutedEventArgs e)
        {
            var s = CommandBoxTextBox.Text.ToString();

            if (s.Length == 0)
                return;

            string first = s.ToString().ToLower();

            try
            {
                if ((first == "calibrate"))
                {
                    already_calibrated = true;
                    port.Write("CALIBRATE");
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
                // cmd,1014,cx,on or off
                if (first.Substring(0, 11) == "cmd,1014,cx")
                {
                    port.Write(first.ToUpper());
                }

                if (first.Substring(0, 11) == "cmd,1014,st")
                {
                    var temp = first.ToUpper();
                    port.Write(temp);
                }
            }
            catch { };

            // sim,enable
            try
            {
                if (first == "sim,enable" || first == "sim,on")
                {
                    // TODO: code still not working try fresh again
                }
            }
            catch (Exception err)
            {
                Trace.WriteLine("Unable to start simulation mode: ", err.ToString());
            }

            // restart (Not to use)
            if (first == "restart")
            {
                try
                { 
                    port.Write("$RESTART");
                    file = new StreamWriter(filePath + fileC, true);
                    file.WriteLine("RESTART\n");
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
                    Trace.WriteLine("Unable to open Port! Check again pls");
                }

                rtb.AppendText("✅ Connection Established to GCS Arduino.\n");
            }
            catch
            {
                rtb.AppendText("Unable to connect or already connected! \n");
            }
        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            Dispatcher.Invoke(new Action(() => { rtb.AppendText("✅ Received!\n"); }));

            SerialPort sp = (SerialPort)sender;
            string packet = sp.ReadLine();
            Trace.WriteLine(packet);

            if (packet.Length > 25)
            {
                // Edit According to the data received
                string[] pack = packet.Split(',');
                foreach (var item in pack)
                    Trace.Write($"{item} ");

                // Container Data Received
                if (pack[3] == "C")
                {
                    data.teamID = Convert.ToInt32(pack[0]);
                    data.missionTime = pack[1];
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
                    data.gpsAltitude = Convert.ToDouble(pack[12]);
                    data.gpsSats = Convert.ToDouble(pack[13]);
                    data.softwareState = pack[14];
                    data.cmdEcho = pack[15];

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

                // Payload Data receieved
                else if (pack[3] == "T")
                {
                    Pdata.teamID = Convert.ToInt32(pack[0]);
                    Pdata.missionTime = pack[1];
                    Pdata.packetCount = Convert.ToInt32(pack[2]);
                    Pdata.packetType = pack[3];
                    Pdata.tpAltitude = Convert.ToDouble(pack[4]);
                    Pdata.tpTemp = Convert.ToDouble(pack[5]);
                    Pdata.tpVoltage = Convert.ToDouble(pack[6]);
                    Pdata.gyroR = Convert.ToDouble(pack[7]);
                    Pdata.gyroP = Convert.ToDouble(pack[8]);
                    Pdata.gyroY = Convert.ToDouble(pack[9]);
                    Pdata.accelR = Convert.ToDouble(pack[10]);
                    Pdata.accelP = Convert.ToDouble(pack[11]);
                    Pdata.accelY = Convert.ToDouble(pack[12]);
                    Pdata.magR = Convert.ToDouble(pack[13]);
                    Pdata.magP = Convert.ToDouble(pack[14]);
                    Pdata.magY = Convert.ToDouble(pack[15]);
                    Pdata.pointingError = pack[16];
                    Pdata.tpSoftwareState = pack[17];

                    Dispatcher.Invoke(new Action(() =>
                    {
                        packetCnt += 1;
                        AddInPayloadDG(pack);
                    }));

                    file = new StreamWriter(filePath + fileT, true);
                    string packet1 = packet;
                    file.WriteLine(packet1);
                    file.Close();

                    // Plotting Check Here
                    voltageChartPayloadValue.Add(data.voltage);
                    temperatureChartPayloadValue.Add(data.temp);
                    altitudeChartPayloadValue.Add(data.altitude);
                }
                else
                {
                    var put = new string[5]
                    {
                        pack[0],
                        pack[1],
                        pack[2],
                        pack[3],
                        pack[4],
                    };


                    Dispatcher.Invoke(new Action(() =>
                    {
                        AddInCustomDG(put);
                    }));
                    var f = new StreamWriter(@"C:\Users\ISHWARENDRA\Desktop\log.txt", true);
                    f.Write(packet);
                    f.Close();
                }
            }

            void AddInCustomDG(string[] put)
            {
                try
                {
                    CustomDataGrid.Items.Add(new { TEAM_ID = put[0], MISSION_TIME = put[1], PACKET_COUNT = put[2], PACKET_TYPE = put[3], MESSAGE = put[4] });
                }
                catch
                {
                    Trace.WriteLine("More than 5 maybe");
                }
            }

            void AddInContainerDG(string[] put)
            {
                try
                {
                    ContainerpacketCnt += 1;
                    ContainerDataGrid.Items.Add(new { S_NO = ContainerpacketCnt, TEAM_ID = put[0], MISSION_TIME = put[1], PACKET_COUNT = put[2], PACKET_TYPE = put[3], MODE = put[4], TP_RELEASED = put[5], ALTITUDE = put[6], TEMP = put[7], VOLTAGE = put[8], GPS_TIME = put[9], GPS_LATITUDE = put[10], GPS_LONGITUDE = put[11], GPS_ALTITUDE = put[12], GPS_SATS = put[13], SOFTWARE_STATE = put[14], CMD_ECHO = put[15] });
                }
                catch
                {
                    Trace.WriteLine("Conatiner data smaller or buggy");
                }
            }

            void AddInPayloadDG(string[] put)
            {
                try
                {
                    PayloadpacketCnt += 1;
                    PayloadDataGrid.Items.Add(new { S_NO = PayloadpacketCnt, TEAM_ID = put[0], PACKET_COUNT = put[1], PACKET_TYPE = put[2], TP_ALTITUDE = put[3], TP_TEMP = put[4], TP_VOLTAGE = put[5], GYRO_R = put[6], GYRO_P = put[7], GYRO_Y = put[8], ACCEL_R = put[9], ACCEL_P = put[10], ACCEL_Y = put[11], MAG_R = put[12], MAG_P = put[13], MAG_Y = put[14], POINTING_ERROR = put[15], TP_SOFTWARE_STATE = put[16] });
                }
                catch
                {
                    Trace.WriteLine("Payload data smaller or buggy");
                }
            }

            #region FLIGHTSTATE
            //switch (data.flightState)
            //{
            //    case 2:
            //        CalibratedEllipse.Fill = new SolidColorBrush(Colors.Green);

            //        if (f2 == 0)
            //        {
            //            Dispatcher.Invoke(new Action(() =>
            //            {
            //                rtb.AppendText("✅ Calibrated.. Waiting for Launch.\n");
            //            }));
            //            f2++;
            //        }

            //        break;

            //    case 3:
            //        LaunchEllipse.Fill = new SolidColorBrush(Colors.Green);

            //        if (f3 == 0)
            //        {
            //            Dispatcher.Invoke(new Action(() =>
            //            {
            //                rtb.AppendText("✅ Launch Detected.. Ascending.\n");
            //            }));
            //            f3++;
            //        }

            //        break;

            //    case 4:
            //        ParachuteDeployedEllipse.Fill = new SolidColorBrush(Colors.Green);

            //        if (f4 == 0)
            //        {
            //            Dispatcher.Invoke(new Action(() =>
            //            {
            //                rtb.AppendText("✅ Parachute Deployed.. Descending.\n");
            //            }));
            //            f4++;
            //        }

            //        break;


            //    case 5:
            //        PayloadReleasedEllipse.Fill = new SolidColorBrush(Colors.Green);

            //        if (f5 == 0)
            //        {
            //            Dispatcher.Invoke(new Action(() =>
            //            {
            //                rtb.AppendText("✅ Parachute Deployed.. Descending.\n");
            //            }));
            //            f5++;
            //        }

            //        break;

            //    case 6:
            //        LandedEllipse.Fill = new SolidColorBrush(Colors.Green);

            //        if (f6 == 0)
            //        {
            //            Dispatcher.Invoke(new Action(() =>
            //            {
            //                rtb.AppendText("✅ Landed!! 🎉\n");
            //            }));
            //            f6++;
            //        }

            //        break;

            //    default:
            //        break;
            //}
            #endregion
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

        private void AddAllColumnInCustomDataGrid()
        {
            var ColName = new List<string> { "TEAM_ID", "MISSION_TIME", "PACKET_COUNT", "PACKET_TYPE", "MESSAGE" };

            foreach (var header in ColName)
            {
                DataGridTextColumn textColumn = new DataGridTextColumn();
                textColumn.Header = header;
                textColumn.Binding = new Binding(header);
                CustomDataGrid.Columns.Add(textColumn);
            }
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
