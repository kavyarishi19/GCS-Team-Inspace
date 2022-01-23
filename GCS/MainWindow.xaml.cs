using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace GCS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
    }

    #region Data Template for Payload and Container Data
    public class PayloadData
    {
        public int S_NO { get; set; }
        public string TEAM_ID { get; set; }
        public string MISSION_TIME { get; set; }
        public string PACKET_COUNT { get; set; }
        public string PACKET_TYPE { get; set; }
        public string TP_ALTITUDE { get; set; }
        public string TP_TEMP { get; set; }
        public string TP_VOLTAGE { get; set; }
        public string GYRO_R { get; set; }
        public string GYRO_P { get; set; }
        public string GYRO_Y { get; set; }
        public string ACCEL_R { get; set; }
        public string ACCEL_P { get; set; }
        public string ACCEL_Y { get; set; }
        public string MAG_R { get; set; }
        public string MAG_P { get; set; }
        public string MAG_Y { get; set; }
        public string POINTING_ERROR { get; set; }
        public string TP_SOFTWARE_STATE { get; set; }
    }
    public class ContainerData
    {
        public int S_NO { get; set; }
        public string TEAM_ID { get; set; }
        public string MISSION_TIME { get; set; }
        public string PACKET_COUNT { get; set; }
        public string PACKET_TYPE { get; set; }
        public string MODE { get; set; }
        public string TP_RELEASED { get; set; }
        public string ALTITUDE { get; set; }
        public string TEMP { get; set; }
        public string VOLTAGE { get; set; }
        public string GPS_TIME { get; set; }
        public string GPS_LATITUDE { get; set; }
        public string GPS_LONGITUDE { get; set; }
        public string GPS_ALTITUDE { get; set; }
        public string GPS_SATS { get; set; }
        public string SOFTWARE_STATE { get; set; }
        public string CMD_ECHO { get; set; }
    }

    #endregion

    #region Container and PayLoad Data Load in GCS

    // Set Container and Payload Data to show up in GCS (Tab-3 and Tab-4)
    public class DataListSetUp
    {
        public List<PayloadData> payloadData { get; set; } = SetPayloadData();
        public List<ContainerData> containerData { get; set; } = SetContainerData();
        public static List<PayloadData> SetPayloadData()
        {
            // Local Path of File
            string filePath = @"C:\Users\ISHWARENDRA\source\repos\GCS-Team-Inspace\GCS\Resources\csvFiles\";
            string fileName = @"Flight_1014_T.csv";

            string file = filePath + fileName;

            var list = new List<PayloadData>();
            if (File.Exists(file))
            {
                var lines = File.ReadAllLines(file);
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i].Split(',');
                    PayloadData data = new PayloadData()
                    {
                        // TODO: Change needy data to integer and keep others as string 
                        S_NO = i,
                        TEAM_ID = line[0],
                        MISSION_TIME = line[1],
                        PACKET_COUNT = line[2],
                        PACKET_TYPE = line[3],
                        TP_ALTITUDE = line[4],
                        TP_TEMP = line[5],
                        TP_VOLTAGE = line[6],
                        GYRO_R = line[7],
                        GYRO_P = line[8],
                        GYRO_Y = line[9],
                        ACCEL_R = line[10],
                        ACCEL_P = line[11],
                        ACCEL_Y = line[12],
                        MAG_R = line[13],
                        MAG_P = line[14],
                        MAG_Y = line[15],
                        POINTING_ERROR = line[16],
                        TP_SOFTWARE_STATE = line[17]
                    };

                    list.Add(data);
                }
            }
            else
            {
                File.Create(file);
            }

            return list;
        }
        public static List<ContainerData> SetContainerData()
        {
            string filePath = @"C:\Users\ISHWARENDRA\source\repos\GCS-Team-Inspace\GCS\Resources\csvFiles\";
            string fileName = @"Flight_1014_C.csv";

            string file = filePath + fileName;

            var list = new List<ContainerData>();
            if (File.Exists(file))
            {
                var lines = File.ReadAllLines(file);
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i].Split(',');
                    ContainerData data = new ContainerData()
                    {
                        // TODO: Change needy data to integer and keep others as string 
                        S_NO = i,
                        TEAM_ID = line[0],
                        MISSION_TIME = line[1],
                        PACKET_COUNT = line[2],
                        PACKET_TYPE = line[3],
                        MODE = line[4],
                        TP_RELEASED = line[5],
                        ALTITUDE = line[6],
                        TEMP = line[7],
                        VOLTAGE = line[8],
                        GPS_TIME = line[9],
                        GPS_LATITUDE = line[10],
                        GPS_LONGITUDE = line[11],
                        GPS_ALTITUDE = line[12],
                        GPS_SATS = line[13],
                        SOFTWARE_STATE = line[14],
                        CMD_ECHO = line[15]
                    };

                    list.Add(data);
                }
            }
            else
            {
                File.Create(file);
            }

            return list;
        }
    }
    #endregion
}
