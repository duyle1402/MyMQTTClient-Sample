﻿using System;
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

namespace AS_MQTTClient.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        //ModelImporter import = new ModelImporter();


        MainViewModel mainVM = new MainViewModel();
        

       DispatcherTimer TimerArchive = new DispatcherTimer();
        public MainWindow()
        {
            InitializeComponent();

            // import 3D model
            //ObjReader myHelixObjReader = new ObjReader();
            //Model3DGroup MyModel = import.Load(@".\model\magnolia.stl");        
            //model.Content = MyModel;
            //helixControl.ZoomExtents();

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

            // timer
            //DispatcherTimer timer = new DispatcherTimer();
            //timer.Interval = TimeSpan.FromMilliseconds(300);
            //timer.Tick += timer_Tick;
            //timer.Start();

            // linechart
            hslCurve4.SetLeftCurve("A", null, Colors.LightSkyBlue);
            hslCurve4.SetLeftCurve("B", null, Colors.Tomato);
            hslCurve4.SetRightCurve("C", null, Colors.LimeGreen);
            hslCurve4.SetRightCurve("D", null, Colors.Orchid);
            // barchart
            hslBarChart1.SetDataSource(new int[] { 100, 200, 400 }, new string[] { "Nitrogen", "Phosphous", "Potassium"});

        }
        //private void timer_Tick(object sender, EventArgs e)
        //{
        //    count_tick++;

        //    if (count_tick > 10000) count_tick = 0;
        //}

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
            if (!string.IsNullOrEmpty(txtUserName.Text) || !string.IsNullOrEmpty(txtPassWord.Text))
            {
                options.Credentials = new MqttCredential(txtUserName.Text, txtPassWord.Text);
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
            // Triggered when the network is abnormal, you can reconnect to the server here
            if (sender is MqttClient client)
            {
                // Start to reconnect to the server until the connection is successful
                client.LogNet?.WriteInfo("The network is abnormal, please reconnect after 10 seconds.");
                while (true)
                {
                    // Reconnect every 10 seconds 
                    System.Threading.Thread.Sleep(10_000);
                    client.LogNet?.WriteInfo("Ready to reconnect to the server...");

                    // Before reconnecting, you need to determine whether the Client is closed, and the exceptions that you rewrite need to be handled manually by yourself
                    OperateResult connect = client.ConnectServer();
                    if (connect.IsSuccess)
                    {
                        // After the connection is successful, you can subscribe before the break below, or initialize the data
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
                Dispatcher.Invoke(new Action(() =>
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
                                  TimerArchive.Interval = new TimeSpan(0, 0, 30);
                                  TimerArchive.Tick += (s, e) =>
                                  {
                                      InsertAnalog(analog.raw_value[1], analog.processed_value[1]);
                                  };
                                  TimerArchive.Start();
                                       


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

                                  InsertModbus(modbus[1]);
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
                Dispatcher.Invoke(new Action(() =>
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
                    Id = Guid.NewGuid(),
                    Ngay = DateTime.Now,
                    Raw_value = rawvalue,
                    Process_value = (float)processvalue

                });
                db.SaveChanges();
            }
        }
        void InsertModbus (double modbus)
        {
            using (var db = DataProvider.Ins.DB)
            {
                var data_modbus = db.Data_modbus_test;
                data_modbus.Add(new Data_modbus_test
                {
                    Id = Guid.NewGuid(),
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
            
            if(rl1.IsChecked==true)
            {
                OperateResult send = mqttClient.PublishMessage(new MqttApplicationMessage()
                {
                    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
                    Topic = "70804dd7d1e23c7e/RL0",
                    Payload = Encoding.UTF8.GetBytes("ON"),
                    Retain = (bool)checkRetain.IsChecked               // If TRUE, the message will be stored and stored on the server, which is convenient for the client to push immediately after connecting
                });

                if (!send.IsSuccess) MessageBox.Show("Send Failed:" + send.Message);
            }    
            else
            {
                OperateResult send = mqttClient.PublishMessage(new MqttApplicationMessage()
                {
                    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
                    Topic = "70804dd7d1e23c7e/RL0",
                    Payload = Encoding.UTF8.GetBytes("OFF"),
                    Retain = (bool)checkRetain.IsChecked               // If TRUE, the message will be stored and stored on the server, which is convenient for the client to push immediately after connecting
                });

                if (!send.IsSuccess) MessageBox.Show("Send Failed:" + send.Message);
            }    
        }

        private void rl2_Click(object sender, RoutedEventArgs e)
        {
            if (rl2.IsChecked == true)
            {
                OperateResult send = mqttClient.PublishMessage(new MqttApplicationMessage()
                {
                    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
                    Topic = "70804dd7d1e23c7e/RL1",
                    Payload = Encoding.UTF8.GetBytes("ON"),
                    Retain = (bool)checkRetain.IsChecked               // If TRUE, the message will be stored and stored on the server, which is convenient for the client to push immediately after connecting
                });

                if (!send.IsSuccess) MessageBox.Show("Send Failed:" + send.Message);
            }
            else
            {
                OperateResult send = mqttClient.PublishMessage(new MqttApplicationMessage()
                {
                    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
                    Topic = "70804dd7d1e23c7e/RL1",
                    Payload = Encoding.UTF8.GetBytes("OFF"),
                    Retain = (bool)checkRetain.IsChecked               // If TRUE, the message will be stored and stored on the server, which is convenient for the client to push immediately after connecting
                });

                if (!send.IsSuccess) MessageBox.Show("Send Failed:" + send.Message);
            }
        }

        private void rl3_Click(object sender, RoutedEventArgs e)
        {
            if (rl3.IsChecked == true)
            {
                OperateResult send = mqttClient.PublishMessage(new MqttApplicationMessage()
                {
                    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
                    Topic = "70804dd7d1e23c7e/RL2",
                    Payload = Encoding.UTF8.GetBytes("ON"),
                    Retain = (bool)checkRetain.IsChecked               // If TRUE, the message will be stored and stored on the server, which is convenient for the client to push immediately after connecting
                });

                if (!send.IsSuccess) MessageBox.Show("Send Failed:" + send.Message);
            }
            else
            {
                OperateResult send = mqttClient.PublishMessage(new MqttApplicationMessage()
                {
                    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
                    Topic = "70804dd7d1e23c7e/RL2",
                    Payload = Encoding.UTF8.GetBytes("OFF"),
                    Retain = (bool)checkRetain.IsChecked               // If TRUE, the message will be stored and stored on the server, which is convenient for the client to push immediately after connecting
                });

                if (!send.IsSuccess) MessageBox.Show("Send Failed:" + send.Message);
            }
        }

        private void rl4_Click(object sender, RoutedEventArgs e)
        {
            if (rl4.IsChecked == true)
            {
                OperateResult send = mqttClient.PublishMessage(new MqttApplicationMessage()
                {
                    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
                    Topic = "70804dd7d1e23c7e/RL3",
                    Payload = Encoding.UTF8.GetBytes("ON"),
                    Retain = (bool)checkRetain.IsChecked               // If TRUE, the message will be stored and stored on the server, which is convenient for the client to push immediately after connecting
                });

                if (!send.IsSuccess) MessageBox.Show("Send Failed:" + send.Message);
            }
            else
            {
                OperateResult send = mqttClient.PublishMessage(new MqttApplicationMessage()
                {
                    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
                    Topic = "70804dd7d1e23c7e/RL3",
                    Payload = Encoding.UTF8.GetBytes("OFF"),
                    Retain = (bool)checkRetain.IsChecked               // If TRUE, the message will be stored and stored on the server, which is convenient for the client to push immediately after connecting
                });

                if (!send.IsSuccess) MessageBox.Show("Send Failed:" + send.Message);
            }
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

        #region INotifyPropertyChanged implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            if (PropertyChanged != null)
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion


        // load data live chart
        private void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            //IsReading = !IsReading;
            if (mainVM.IsReading)

                Task.Factory.StartNew(mainVM.Read);
        }

      
    }
}
