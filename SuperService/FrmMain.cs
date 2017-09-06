using System;
using System.IO.Ports;
using System.Windows.Forms;
using Fleck;
using Newtonsoft.Json;

namespace SuperService
{
    public partial class FrmMain : Form
    {
        #region 全局变量

        private static SerialPort _serialPort;
        private static IWebSocketConnection _iConnection;

        #endregion
        public FrmMain()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 打开串口
        /// </summary>
        /// <param name="comName">串口名称</param>
        /// <param name="baudRate">波特率</param>
        public static void OpenSerialPort(string comName, int baudRate)
        {
            _serialPort?.Close();
            _serialPort =
                new SerialPort(comName, baudRate, Parity.None, 8, StopBits.One)
                {
                    Handshake = Handshake.RequestToSendXOnXOff
                };
            _serialPort.DataReceived += _serialPort_DataReceived;
            _serialPort.Open();
        }

        /// <summary>
        ///关闭串口
        /// </summary>
        public static void CloseSerialPort()
        {
            _serialPort.Close();
        }

        /// <summary>
        /// 获取串口列表
        /// </summary>
        /// <returns></returns>
        public static void GetSerialPortList()
        {
            string[] serialPortNames = SerialPort.GetPortNames();
            var json = JsonConvert.SerializeObject(serialPortNames);
            _iConnection.Send(json);
        }

        /// <summary>
        /// 串口读取数据事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void _serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var text = _serialPort.ReadExisting();
            var value = text.Replace("\u0002", "").Replace("\r\n\u0003", "");
            _iConnection.Send(value);
        }

        /// <summary>
        /// 启动WebSocket服务监听
        /// </summary>
        public static void StartWebSocket()
        {
            var server = new WebSocketServer("ws://0.0.0.0:8181");
            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    _iConnection = socket;
                };
                socket.OnClose = () => { };
                socket.OnMessage = HandleWebSocket;
            });
        }


        public static void HandleWebSocket(string message)
        {
            var obj= JsonConvert.DeserializeObject<dynamic>(message);
            switch (obj.command.ToString())
            {
                case "OpenSerialPort":
                    var serialPortName = obj.serialPortName.ToString();
                    var baudRate =int.Parse(obj.baudRate.ToString());
                    OpenSerialPort(serialPortName, baudRate);
                    break;
                case "CloseSerialPort":
                    CloseSerialPort();
                    break;
                case "GetSerialPortList":
                    GetSerialPortList();
                    break;
            }
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
           
            StartWebSocket();
        }

        private void FrmMain_Activated(object sender, EventArgs e)
        {
            Hide();
        }
    }
}
