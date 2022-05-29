using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using HslCommunication.MQTT;
using HslCommunication;
using System.Xml.Linq;
using System.Windows;
using System.Threading;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System.Collections;
using Newtonsoft.Json.Linq;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using LiveCharts;
using LiveCharts.Configurations;
using System.Threading.Tasks;
using AS_MQTTClient.ViewModel;
using AS_MQTTClient.Model;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Windows.Controls;
using Excel = Microsoft.Office.Interop.Excel;
using System.Windows.Input;
using iTextSharp;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;
using Microsoft.Win32;

namespace AS_MQTTClient.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        //ModelImporter import = new ModelImporter();

       MainViewModel mainVM = new MainViewModel();
        
       DispatcherTimer TimerArchive_analog = new DispatcherTimer();
       DispatcherTimer TimerArchive_modbus = new DispatcherTimer();
       string modbusTable = "Data_Modbus_test";
       string analogTable = "Data_Analog_test";
       string sqlconnectstring = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=AS_MQTTClient;Integrated Security=True";
       DataTable tableData = new DataTable();


        public MainWindow()
        {
            InitializeComponent();
           
            //--------------------------------  REAL TIME DATA------------------------------
            //To handle live data easily, in this case we built a specialized type
            //the MeasureModel class, it only contains 2 properties
            //DateTime and Value
            //We need to configure LiveCharts to handle MeasureModel class
            //The next code configures MeasureModel  globally, this means
            //that LiveCharts learns to plot MeasureModel and will use this config every time
            //a IChartValues instance uses this type.
            //this code ideally should only run once
            //you can configure series in many ways, learn more at 
            //http://lvcharts.net/App/examples/v1/wpf/Types%20and%20Configuration

            //var mapper = Mappers.Xy<MeasureModel>()
            //    .X(model => model.DateTime.Ticks)   //use DateTime.Ticks as X
            //    .Y(model => model.Value);           //use the value property as Y

            ////lets save the mapper globally.
            //Charting.For<MeasureModel>(mapper);

            ////the values property will store our values array
            //ChartValues = new ChartValues<MeasureModel>();

            ////lets set how to display the X Labels
            //DateTimeFormatter = value => new DateTime((long)value).ToString("mm:ss");

            ////AxisStep forces the distance between each separator in the X axis
            //AxisStep = TimeSpan.FromSeconds(1).Ticks;
            ////AxisUnit forces lets the axis know that we are plotting seconds
            ////this is not always necessary, but it can prevent wrong labeling
            //AxisUnit = TimeSpan.TicksPerSecond;

            //SetAxisLimits(DateTime.Now);

            //The next code simulates data changes every 300 ms

           
         
           // DataContext = this;
           

        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
         
            // linechart
            hslCurve4.SetLeftCurve("A", null, Colors.LightSkyBlue);
            hslCurve4.SetLeftCurve("B", null, Colors.Tomato);
            hslCurve4.SetRightCurve("C", null, Colors.LimeGreen);
            hslCurve4.SetRightCurve("D", null, Colors.Orchid);
            // barchart
            hslBarChart1.SetDataSource(new int[] { 100, 200, 400 }, new string[] { "Nitrogen", "Phosphous", "Potassium"});

        }
     
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            btnDisconnect.IsEnabled = false;
            GridTopic.IsEnabled = false;
            GridMessage.IsEnabled = false;
            GridPublish.IsEnabled = false;
            GridReceive.IsEnabled = false;
            groupDisplay.IsEnabled = false;

        }
        private MqttClient mqttClient;


        #region connection and display
        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            //  connect, use encrytion bằng fasle dể test trc
            MqttConnectionOptions options = new MqttConnectionOptions()
            {
                IpAddress = txtIPAddress.Text,
                Port = int.Parse(txtPort.Text),
                ClientId = txtClientID.Text,
                KeepAlivePeriod = TimeSpan.FromSeconds(int.Parse(txtKeepAlive.Text)),
                UseRSAProvider = (bool)checkEncryption.IsChecked,
            };
            if (!string.IsNullOrEmpty(txtUserName.Text) || !string.IsNullOrEmpty(txtPassWord.Password))
            {
                options.Credentials = new MqttCredential(txtUserName.Text, txtPassWord.Password);
            }


            btnConnect.IsEnabled = false;
            mqttClient?.ConnectClose();
            mqttClient = new MqttClient(options);
            mqttClient.LogNet = new HslCommunication.LogNet.LogNetSingle(string.Empty);
            mqttClient.LogNet.BeforeSaveToFile += LogNet_BeforeSaveToFile;
            mqttClient.OnMqttMessageReceived += MqttClient_OnMqttMessageReceived;

            OperateResult connect = await mqttClient.ConnectServerAsync();

            if (connect.IsSuccess)
            {
                // enable when connect success

                GridTopic.IsEnabled = true;
                GridMessage.IsEnabled = true;
                GridPublish.IsEnabled = true;
                GridReceive.IsEnabled = true;

                groupDisplay.IsEnabled = true;
                btnConnect.IsEnabled = false;
                btnDisconnect.IsEnabled = true;
             
                mainVM.IsReading = true; // cho phép vẽ đồ thị
                MessageBox.Show("Kết nối thành công đến broker");
            }
            else
            {
                mqttClient = null;
                btnConnect.IsEnabled = true;
                MessageBox.Show("Kết nối false, check lại xem thử có sai gì không, xem lại IP thử nào!");
            }
        }

        private void MqttClient_OnNetworkError(object sender, EventArgs e)
        {
            // Triggered when the network is abnormal,  can reconnect to the server here
            if (sender is MqttClient client)
            {
                // Start to reconnect to the server until the connection is successful
                client.LogNet?.WriteInfo("The network is abnormal, please reconnect after 10 seconds.");
                while (true)
                {
                    // Reconnect every 10 seconds 
                    System.Threading.Thread.Sleep(10_000);
                    client.LogNet?.WriteInfo("Ready to reconnect to the server...");

                    // Before reconnecting,need to determine whether the Client is closed, and the exceptions that you rewrite need to be handled manually by yourself
                    OperateResult connect = client.ConnectServer();
                    if (connect.IsSuccess)
                    {
                        // After the connection is successful, subscribe before the break below, or initialize the data
                        client.LogNet?.WriteInfo("Successfully connected to the server!");
                        break;
                    }
                    client.LogNet?.WriteInfo("The connection failed. Reconnect after 10 seconds of preparation.");
                }
            }
        }

        private long receiveCount = 0;
        public void MqttClient_OnMqttMessageReceived(MqttClient client, string topic, byte[] payload)
        {
            try
            {
                Dispatcher.Invoke(new System.Action(() =>
                  {
                      receiveCount++;
                      tblockReceivecount.Text = "receive Count:" + receiveCount;
                      string msg = Encoding.UTF8.GetString(payload);
                      if (radBtnXML.IsChecked == true)
                      {
                          try
                          {
                              msg = XElement.Parse(msg).ToString();
                          }
                          catch
                          {

                          }
                      }
                      else if (radBtnJson.IsChecked == true)
                      {
                          try
                          {   
                              msg = JObject.Parse(msg).ToString();
                              
                              Relay stateofrelay = JsonConvert.DeserializeObject<Relay>(msg);

                              Analog analog = JsonConvert.DeserializeObject<Analog>(msg);

                              Digital digital = JsonConvert.DeserializeObject<Digital>(msg);

                              Dictionary<int,int> modbus = JsonConvert.DeserializeObject<Dictionary<int, int>>(msg);                             
                                  // relay
                                  if (stateofrelay.relay != null)
                                  {
                                      rl1.IsChecked = (bool)stateofrelay.relay[0];
                                      rl2.IsChecked = (bool)stateofrelay.relay[1];
                                      rl3.IsChecked = (bool)stateofrelay.relay[2];
                                      rl4.IsChecked = (bool)stateofrelay.relay[3];
                                  }
                                 //analog
                              if (analog.processed_value != null)
                              {
                                  gauseA0.Value = (double)analog.processed_value[0];
                                  gauseA1.Value = (double)analog.processed_value[1];
                                  gauseA2.Value = (double)analog.processed_value[2];
                                  gauseA3.Value = (double)analog.processed_value[3];
                                  hslCurve4.AddCurveData(new string[] { "A", "B", "C", "D" },
                                 new float[]
                                     {
                                    (float)analog.processed_value[0],
                                    (float)analog.processed_value[1],
                                    (float)analog.processed_value[2],
                                     (float)analog.processed_value[3],
                                     }
                                        );
                                  // insert
                                  TimerArchive_analog.Interval = new TimeSpan(0, 0, 30);
                                  TimerArchive_analog.Tick += (s, e) =>
                                  {
                                      InsertAnalog(analog.raw_value[1], analog.processed_value[1]);
                                  };
                                  TimerArchive_analog.Start();                                      
                              }
                                 // digital
                              if ((digital.counter != null) && (digital.cur_counter != null) && (digital.digital_input != null))
                              {
                                  counter0.Text = digital.counter[0].ToString();
                                  counter1.Text = digital.counter[1].ToString();
                                  counter2.Text = digital.counter[2].ToString();
                                  counter3.Text = digital.counter[3].ToString();
                                  cur_counter0.Text = digital.cur_counter[0].ToString();
                                  cur_counter1.Text = digital.cur_counter[1].ToString();
                                  cur_counter2.Text = digital.cur_counter[2].ToString();
                                  cur_counter3.Text = digital.cur_counter[3].ToString();
                                  digitalstate1.IsChecked = digital.digital_input[0];
                                  digitalstate2.IsChecked = digital.digital_input[1];
                                  digitalstate3.IsChecked = digital.digital_input[2];
                                  digitalstate4.IsChecked = digital.digital_input[3];
                              }
                              // modbus
                              if(modbus!=null)
                              {
                                  mainVM.test1 = modbus[0];
                                  HslThermometer_1.Value = (float)modbus[1];
                                  HslThermometer_2.Value = (float)modbus[2];
                                  HslThermometer_3.Value = (float)modbus[3];

                                  TimerArchive_modbus.Interval = new TimeSpan(0,0,30);
                                  TimerArchive_modbus.Tick += (s, e) =>
                                  {
                                      InsertModbus(modbus[1]);
                                  };
                                  TimerArchive_modbus.Start();
                              }                                 
                          }
                          catch
                          {
                              MessageBox.Show("Lỗi rồi");
                          }                      
                      }
                      if (RaBtnAddDisplay.IsChecked == true)
                          txtReceive.AppendText($"Topic[{topic}] " + msg + Environment.NewLine);
                      else if (RabtnOverPlayDisPlay.IsChecked == true)
                          txtReceive.Text = $"Topic[{topic}] " + msg;
                  }));

            }
            catch
            {

            }
        }
        private void LogNet_BeforeSaveToFile(object sender, HslCommunication.LogNet.HslEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(new System.Action(() =>
                {
                    if (RaBtnAddDisplay.IsChecked == true)
                        txtReceive.AppendText(e.HslMessage.ToString() + Environment.NewLine);
                }));
            }
            catch
            {

            }
        }
        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            // Disconnect
            MessageBox.Show(" Disconnected from Broker");
            btnLeastOne.IsEnabled = true;
            btnConnect.IsEnabled = true;
            btnDisconnect.IsEnabled = false;
            GridTopic.IsEnabled = false;
            GridMessage.IsEnabled = false;
            GridPublish.IsEnabled = false;
            GridReceive.IsEnabled = false;

            mqttClient.ConnectClose();
        }

        private void btnMostOne_Click(object sender, RoutedEventArgs e)
        {
            // At most once
            OperateResult send = mqttClient.PublishMessage(new MqttApplicationMessage()
            {
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
                Topic = txtTopic.Text,
                Payload = Encoding.UTF8.GetBytes(txtPayLoad.Text),
                Retain = (bool)checkRetain.IsChecked               // If TRUE, the message will be stored and stored on the server, which is convenient for the client to push immediately after connecting
            });

            if (!send.IsSuccess) MessageBox.Show("Send Failed:" + send.Message);
        }

        private void btnOnlyTransfer_Click(object sender, RoutedEventArgs e)
        {
            // Only push and not publish, only valid for HSL MQTT SERVER
            OperateResult send = mqttClient.PublishMessage(new MqttApplicationMessage()
            {
                QualityOfServiceLevel = MqttQualityOfServiceLevel.OnlyTransfer,
                Topic = txtTopic.Text,
                Payload = Encoding.UTF8.GetBytes(txtPayLoad.Text)
            });

            if (!send.IsSuccess) MessageBox.Show("Send Failed:" + send.Message);
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            // Empty
            txtReceive.Clear();
        }

        private void btnLeastOne_Click(object sender, RoutedEventArgs e)
        {
            // At least once
            OperateResult send = mqttClient.PublishMessage(new MqttApplicationMessage()
            {
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
                Topic = txtTopic.Text,
                Payload = Encoding.UTF8.GetBytes(txtPayLoad.Text),
                Retain = (bool)checkRetain.IsChecked               // If TRUE, the message will be stored and stored on the server, which is convenient for the client to push immediately after connecting
            });

            if (!send.IsSuccess) MessageBox.Show("Send Failed:" + send.Message);
        }

        private void btnExactOne_Click(object sender, RoutedEventArgs e)
        {
            OperateResult send = mqttClient.PublishMessage(new MqttApplicationMessage()
            {
                QualityOfServiceLevel = MqttQualityOfServiceLevel.ExactlyOnce,
                Topic = txtTopic.Text,
                Payload = Encoding.UTF8.GetBytes(txtPayLoad.Text),
                Retain = (bool)checkRetain.IsChecked               // If TRUE, the message will be stored and stored on the server, which is convenient for the client to push immediately after connecting
            });

            if (!send.IsSuccess) MessageBox.Show("Send Failed:" + send.Message);
        }

        private void btnSubcrible_Click(object sender, RoutedEventArgs e)
        {

            OperateResult send = mqttClient.SubscribeMessage(new string[] { txtTopic.Text });

            if (!send.IsSuccess) MessageBox.Show("Send Failed:" + send.Message);


        }

        private void btnUnsubCribe_Click(object sender, RoutedEventArgs e)
        {
            OperateResult send = mqttClient.UnSubscribeMessage(txtTopic.Text);


            if (!send.IsSuccess) MessageBox.Show("Send Failed:" + send.Message);
        }



        private async void ThreadPoolSendTest(object obj)
        {
            if (obj is MqttApplicationMessage message)
            {
                for (int i = 0; i < 100; i++)
                {
                    await mqttClient.PublishMessageAsync(message);
                }
            }
        }
        #endregion


        // lưu trữ data, đặt tên phải giống cấu trúc chuỗi json nếu k bị lỗi 
        // save the data, class name must be equal json.name
        public class Relay
        {
            public List<bool> relay { get; set; }
        }
        public class Analog 
        {
            public List<int> raw_value { get; set; }
            public List<double> processed_value { get; set; }
        }
        public class Digital
        {
            public List<int> counter { get; set; }
            public List<int> cur_counter { get; set;}
            public List<bool> digital_input { get; set;}
        }
        #region lưu dữ liệu vào database
        // data analog
        void InsertAnalog (int rawvalue, double processvalue )
        {
            using (var db = DataProvider.Ins.DB)
            {
                var data_analog = db.Data_Analog_test;
                data_analog.Add(new Data_Analog_test
                {
                    
                    Ngay = DateTime.Now,
                    RawValue = rawvalue,
                    ProcessValue = (float)processvalue

                });
                db.SaveChanges();
            }
        }
        // modbus
        void InsertModbus (double modbus)
        {
            using (var db = DataProvider.Ins.DB)
            {
                var data_modbus = db.Data_Modbus_test;
                data_modbus.Add(new Data_Modbus_test
                {
                   
                    Ngay = DateTime.Now,
                    EnergyTotal = modbus,
                });


                db.SaveChanges();
            }
        }
        #endregion
        
        //public int test1;

        // here code send json to gateway

        #region điều khiển bật tắt relay
        private void rl1_Click(object sender, RoutedEventArgs e)
        {
            string topic = "70804dd7d1e23c7e/RL0";
            if (rl1.IsChecked==true)
            {
                ControlRelay(topic, "ON");
            }    
            else
            {
                ControlRelay(topic, "OFF");
            }    
        }

        private void rl2_Click(object sender, RoutedEventArgs e)
        {
            string topic = "70804dd7d1e23c7e/RL1";
            if (rl2.IsChecked == true)
            {
                ControlRelay(topic, "ON");
            }
            else
            {
                ControlRelay(topic, "OFF");
            }
        }

        private void rl3_Click(object sender, RoutedEventArgs e)
        {
            string topic = "70804dd7d1e23c7e/RL2";
            if (rl3.IsChecked == true)
            {
                ControlRelay(topic, "ON");
            }
            else
            {
                ControlRelay(topic, "OFF");
            }
        }

        private void rl4_Click(object sender, RoutedEventArgs e)
        {
            string topic = "70804dd7d1e23c7e/RL3";
            if (rl4.IsChecked == true)
            {
                ControlRelay(topic,"ON");
            }
            else
            {
                ControlRelay(topic, "OFF");
            }
        }

      private void ControlRelay( string topic, string state)
        {
            OperateResult send = mqttClient.PublishMessage(new MqttApplicationMessage()
            {
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
                Topic = topic,
                Payload = Encoding.UTF8.GetBytes(state),
                Retain = (bool)checkRetain.IsChecked               // If TRUE, the message will be stored and stored on the server, which is convenient for the client to push immediately after connecting
            });

            if (!send.IsSuccess) MessageBox.Show("Send Failed:" + send.Message);
        }
        #endregion

        //#region real time data using live chart

        //private double _axisMax;
        //private double _axisMin;
        //private double _trend;
        //public ChartValues<MeasureModel> ChartValues { get; set; }
        //public Func<double, string> DateTimeFormatter { get; set; }
        //public double AxisStep { get; set; }
        //public double AxisUnit { get; set; }

        //public double AxisMax
        //{
        //    get { return _axisMax; }
        //    set
        //    {
        //        _axisMax = value;
        //        OnPropertyChanged("AxisMax");
        //    }
        //}
        //public double AxisMin
        //{
        //    get { return _axisMin; }
        //    set
        //    {
        //        _axisMin = value;
        //        OnPropertyChanged("AxisMin");
        //    }
        //}

        //public bool IsReading { get; set; }

        //private void Read()
        //{
        ////    var r = test1 ;

        //    while (IsReading)
        //    {
        //        var r = test1;
        //        Thread.Sleep(1000);
        //        var now = DateTime.Now;

        //        _trend = r;

        //        ChartValues.Add(new MeasureModel
        //        {
        //            DateTime = now,
        //            Value = _trend
        //        });

        //        SetAxisLimits(now);

        //        //lets only use the last 150 values
        //        if (ChartValues.Count > 1000) ChartValues.RemoveAt(0);
        //    }
        //}

        //private void SetAxisLimits(DateTime now)
        //{
        //    AxisMax = now.Ticks + TimeSpan.FromSeconds(1).Ticks; // lets force the axis to be 1 second ahead
        //    AxisMin = now.Ticks - TimeSpan.FromSeconds(8).Ticks; // and 8 seconds behind
        //}

        ////private void InjectStopOnClick(object sender, RoutedEventArgs e)
        ////{
        ////    IsReading = !IsReading;
        ////    if (IsReading) Task.Factory.StartNew(Read);
        ////}
      
        //#endregion

     


        // load data live chart
       
        
        private void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            //IsReading = !IsReading;
            if (mainVM.IsReading)

                Task.Factory.StartNew(mainVM.Read);
        }
                    
        #region show data sql
        
        private void btnReadSQL_Click(object sender, RoutedEventArgs e)
        {
            cbDatabase.Items.Clear();
                 
            using (var sqlConnection = new SqlConnection(sqlconnectstring))
            {
                // chọn tất cả database có trong sql
                string cmdText = @"SELECT name FROM master.dbo.sysdatabases WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb')";                
                SqlCommand cmd = new SqlCommand(cmdText, sqlConnection);
                sqlConnection.Open();
                SqlDataReader reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {
                    while(reader.Read())
                    {
                        cbDatabase.Items.Add(reader.GetString(0));
                    }
                }         
             }

        }
        
        private void cbDatabase_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            cbDataTable.Items.Clear();
            var table = cbDatabase.SelectedItem as string;
            string catalog = "AS_MQTTClient";
           
            if (table == catalog)
            {
                using (var sqlConnection = new SqlConnection(sqlconnectstring))
                {
                    // chọn tất cả database có trong sql
                    string cmdText = @"SELECT Table_Name  FROM INFORMATION_SCHEMA.TABLES";
                    SqlCommand cmd = new SqlCommand(cmdText, sqlConnection);
                    sqlConnection.Open();
                    SqlDataReader reader = cmd.ExecuteReader();
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            cbDataTable.Items.Add(reader.GetString(0));
                        }
                    }
                }

            }
            else
            {
                MessageBox.Show("Chọn lại cho đúng database MQTT!");
            }

        }

        private void btnShowData_Click(object sender, RoutedEventArgs e)
        {
            var db = DataProvider.Ins.DB;
            dgDataTest.ItemsSource = null;
            string data = cbDataTable.SelectedItem as string;
            tableData.Clear();
            if (data == analogTable)
            {
                try
                {

                    if (DpFromDate.SelectedDate < DpToDate.SelectedDate && DpFromDate.SelectedDate != null && DpToDate.SelectedDate != null)
                    {

                        var query =
                        from s in db.Data_Analog_test
                        where s.Ngay >= DpFromDate.SelectedDate && s.Ngay <= DpToDate.SelectedDate
                        select s;
                        //dgDataTest.Items.Add(query.ToList());
                        dgDataTest.ItemsSource = query.ToList();
                      
                    }
                    else
                    {
                        MessageBox.Show("Chọn lại ngày, ngày bắt đầu xem phải trước ngày kết thúc", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

            }
         
            if (data == modbusTable)
            {
                try
                {

                    if (DpFromDate.SelectedDate < DpToDate.SelectedDate && DpFromDate.SelectedDate != null && DpToDate.SelectedDate != null)
                    {

                        var query =
                        from s in db.Data_Modbus_test
                        where s.Ngay >= DpFromDate.SelectedDate && s.Ngay <= DpToDate.SelectedDate
                        select s;
                        //dgDataTest.Items.Add(query.ToList());
                        dgDataTest.ItemsSource = query.ToList();

                    }

                    else
                    {
                        MessageBox.Show("Chọn lại ngày, ngày bắt đầu xem phải trước ngày kết thúc", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

            }

        }
        private void btnExcel_Click(object sender, RoutedEventArgs e)
        {
            tableData = DataGridtoDataTable(dgDataTest);
            ReportExcel(tableData, "Report from MQTT Client", "From: " + ConvertDatePicker(DpFromDate, "dd/MM/yyyy") + " To: " + ConvertDatePicker(DpToDate, "dd/MM/yyyy"));
        }

        private void btnPDF_Click(object sender, RoutedEventArgs e)
        {
            tableData = DataGridtoDataTable(dgDataTest);
            ReportPDF(tableData, cbSizePage.Text , "Report from MQTT Client", "From: " + ConvertDatePicker(DpFromDate, "dd/MM/yyyy") + " To: " + ConvertDatePicker(DpToDate, "dd/MM/yyyy"));
        }

        #endregion
        private void ReportExcel(DataTable dataTable, string header, string day)
        {

            Microsoft.Office.Interop.Excel._Application app = new Microsoft.Office.Interop.Excel.Application();
            Microsoft.Office.Interop.Excel._Workbook workbook = app.Workbooks.Add(Type.Missing);
            Microsoft.Office.Interop.Excel._Worksheet worksheet = null;
            app.Visible = true;
            worksheet = workbook.Sheets["Sheet1"];
            worksheet = workbook.ActiveSheet;

            // phần header
            Excel.Range Header1 = (Excel.Range)worksheet.Cells[1, 1];
            Excel.Range Header2 = (Excel.Range)worksheet.Cells[1, dataTable.Columns.Count];
            Excel.Range Header = worksheet.get_Range(Header1, Header2);
            Header.MergeCells = true;
            Header.Value2 = header;
            Header.Font.Bold = true;
            Header.Font.Name = "Times New Roman";
            Header.Font.Size = "18";
            Header.Font.ColorIndex = 30;
            Header.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

            // phần time
            Excel.Range PartTime1 = (Excel.Range)worksheet.Cells[2, 1];
            Excel.Range PartTime2 = (Excel.Range)worksheet.Cells[2, dataTable.Columns.Count]; //nếu có cột tổng: + 1
            Excel.Range PartTime = worksheet.get_Range(PartTime1, PartTime2);
            PartTime.MergeCells = true;
            PartTime.Value2 = day; // "Từ " + dateTimePicker01.Value.ToString("dd/MM/yyyy HH:mm:ss") + " đến " + dateTimePicker02.Value.ToString("dd/MM/yyyy HH:mm:ss");
            PartTime.Font.Bold = false;
            PartTime.Font.Name = "Times New Roman";
            PartTime.Font.Size = "12";
            PartTime.Font.ColorIndex = 26;
            PartTime.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

            // phần data
            Excel.Range PartData1 = (Excel.Range)worksheet.Cells[3, 1];
            Excel.Range PartnData2 = (Excel.Range)worksheet.Cells[dataTable.Rows.Count + 3, dataTable.Columns.Count];
            Excel.Range PartData = worksheet.get_Range(PartData1, PartnData2);
            PartData.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
            PartData.Font.Bold = false;
            PartData.Font.Name = "Times New Roman";
            PartData.Font.Size = "12";

            Excel.Range Area1 = (Excel.Range)worksheet.Cells[1, 1];
            Excel.Range Area2 = (Excel.Range)worksheet.Cells[dataTable.Rows.Count + 3, dataTable.Columns.Count];
            Excel.Range Area = worksheet.get_Range(Area1, Area2);
            Area.Borders.LineStyle = Excel.Constants.xlSolid;

            //data
            for (int i = 0; i < dataTable.Columns.Count; i++)
            {
                worksheet.Cells[3, i + 1] = dataTable.Columns[i].ColumnName;
            }
            //worksheet.Cells[3, dataGridView.Columns.Count + 1] = "TỔNG";

            for (int i = 0; i < dataTable.Rows.Count; i++)
            {
                for (int j = 0; j < dataTable.Columns.Count; j++)
                {
                    if (dataTable.Rows[i][j] != null)
                    {
                        worksheet.Cells[i + 4, j + 1] = dataTable.Rows[i][j].ToString();
                    }
                    else
                    {
                        worksheet.Cells[i + 4, j] = "";
                    }
                }
            }


            System.Runtime.InteropServices.Marshal.ReleaseComObject(workbook);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(app);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(worksheet);

        }

        private void ReportPDF(DataTable dataTable, string SizePage, string header, string day )
        {
            iTextSharp.text.Font Times = FontFactory.GetFont("Times");
            // Creating iTextSharp Table from DataTable data
            PdfPTable pdfTable = new PdfPTable(dataTable.Columns.Count);
            pdfTable.DefaultCell.Padding = 10;
            pdfTable.WidthPercentage = 100;
            pdfTable.HorizontalAlignment = Element.ALIGN_CENTER;
            pdfTable.DefaultCell.BorderWidth = 1;

            PdfPCell cellName = new PdfPCell(new Phrase(header, Times));
            cellName.BackgroundColor = new iTextSharp.text.BaseColor(128, 255, 255);
            cellName.HorizontalAlignment = 1; //0=Left, 1=Centre, 2=Right
            cellName.Colspan = dataTable.Columns.Count;
            pdfTable.AddCell(cellName);

            PdfPCell cellDay = new PdfPCell(new Phrase(day, Times));
            cellDay.BackgroundColor = new iTextSharp.text.BaseColor(255, 255, 128);
            cellDay.HorizontalAlignment = 1; //0=Left, 1=Centre, 2=Right
            cellDay.Colspan = dataTable.Columns.Count;
            pdfTable.AddCell(cellDay);

            for (int i = 0; i < dataTable.Columns.Count; i++)
            {
                PdfPCell cellHeader = new PdfPCell(new Phrase(dataTable.Columns[i].ColumnName, Times));
                cellHeader.BackgroundColor = new iTextSharp.text.BaseColor(240, 240, 240);
                cellHeader.HorizontalAlignment = 1; //0=Left, 1=Centre, 2=Right
                pdfTable.AddCell(cellHeader);
            }

            // Add data
            for (int i = 0; i < dataTable.Rows.Count; i++)
            {
                for (int j = 0; j < dataTable.Columns.Count; j++)
                {
                    if (dataTable.Rows[i][j] != null)
                    {
                        PdfPCell cellData = new PdfPCell(new Phrase(dataTable.Rows[i][j].ToString(), Times));
                        cellData.BackgroundColor = new iTextSharp.text.BaseColor(255, 255, 255);
                        cellData.HorizontalAlignment = 1; //0=Left, 1=Centre, 2=Right
                        pdfTable.AddCell(cellData);
                    }
                }
            }
            // Exporting to PDF
            SaveFileDialog path = new SaveFileDialog();
            path.Title = "Export Setting";
            path.Filter = "Text file (pdf)|*.pdf|All file (*.*)|*.*";

            iTextSharp.text.Rectangle Size = PageSize.A4;
            if (SizePage == "A0") { Size = PageSize.A0; }
            if (SizePage == "A1") { Size = PageSize.A1; }
            if (SizePage == "A2") { Size = PageSize.A2; }
            if (SizePage == "A3") { Size = PageSize.A3; }
            if (SizePage == "A4") { Size = PageSize.A4; }
            if (SizePage == "A5") { Size = PageSize.A5; }

            if (path.ShowDialog() == true)
            {
                using (FileStream stream = new FileStream(path.FileName, FileMode.Create))
                {
                    Document pdfDoc = new Document(Size, 10f, 10f, 10f, 10f);
                    PdfWriter.GetInstance(pdfDoc, stream);
                    pdfDoc.Open();
                    pdfDoc.Add(pdfTable);
                    pdfDoc.Close();
                }
            }
        }
       
        #region 
        // convert datepicker
        public string ConvertDatePicker(DatePicker value, string Type)
        {
            DateTime? selectedDate = value.SelectedDate;

            if (selectedDate.HasValue)
            {
                return selectedDate.Value.ToString(Type, System.Globalization.CultureInfo.InvariantCulture);
            }

            return DateTime.Now.ToString(Type, System.Globalization.CultureInfo.InvariantCulture);
        }

        // datagrid to datatable
        public static DataTable DataGridtoDataTable(DataGrid dg)
        {
            dg.SelectAllCells();
            dg.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
            ApplicationCommands.Copy.Execute(null, dg);
            dg.UnselectAllCells();
            String result = (string)Clipboard.GetData(DataFormats.CommaSeparatedValue);
            string[] Lines = result.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
            string[] Fields;
            Fields = Lines[0].Split(new char[] { ',' });
            int Cols = Fields.GetLength(0);
            DataTable dt = new DataTable();
            //1st row must be column names; force lower case to ensure matching later on.
            for (int i = 0; i < Cols; i++)
                dt.Columns.Add(Fields[i].ToUpper(), typeof(string));
            DataRow Row;
            for (int i = 1; i < Lines.GetLength(0) - 1; i++)
            {
                Fields = Lines[i].Split(new char[] { ',' });
                Row = dt.NewRow();
                for (int f = 0; f < Cols; f++)
                {
                    Row[f] = Fields[f];
                }
                dt.Rows.Add(Row);
            }
            return dt;

        }

        #endregion

        #region INotifyPropertyChanged implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            if (PropertyChanged != null)
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

    }
}
