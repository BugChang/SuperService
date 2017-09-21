using System;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using Fleck;
using Newtonsoft.Json;

namespace SuperService
{
    public partial class FrmMain : Form
    {
        #region 全局变量

        private static readonly int _version = 1;
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

        public static void WirteSerialPort(string text)
        {
            if (_serialPort != null)
            {
                //对于中文的话,要先对其进行编码,将其转换成 _Base64String ,否则你得不到中文字符串的 
                byte[] data = Encoding.Unicode.GetBytes(text);
                string str = Convert.ToBase64String(data);
                _serialPort.WriteLine(str);
                CloseSerialPort();
            }

        }

        /// <summary>
        /// 获取串口列表
        /// </summary>
        /// <returns></returns>
        public static void GetSerialPortList()
        {
            string[] serialPortNames = SerialPort.GetPortNames();
            var obj = new
            {
                Method = "GetSerialPortList",
                Data = serialPortNames
            };
            var json = JsonConvert.SerializeObject(obj);
            try
            {
                _iConnection.Send(json);
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// 串口读取数据事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void _serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var text = _serialPort.ReadExisting();
            var value = text.Replace("\u0002", "").Replace("\u0003", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");
            var obj = new
            {
                Method = "SerialPortReceived",
                Data = value
            };
            var json = JsonConvert.SerializeObject(obj);
            try
            {
                _iConnection.Send(json);
            }
            catch
            {
                // ignored
            }
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

        public static void GetMacAddress()
        {
            var macList = MacHelper.GetMacList();
            var obj = new
            {
                Method = "GetMacAddress",
                Data = macList[0]
            };

            var json = JsonConvert.SerializeObject(obj);
            try
            {
                _iConnection.Send(json);
            }
            catch
            {
                // ignored
            }
        }

        public static void OpenDoor(string no)
        {
            var strCommand = "Open-Door-Request";

            //通信协议
            TcpClient objTcpClient = new TcpClient();
            try
            {
                IPAddress serverAddr = IPAddress.Parse("127.0.0.1");// 取本机地址
                objTcpClient.Connect(serverAddr, int.Parse("56789"));
            }
            catch
            {
                // ignored
            }
            NetworkStream objNetworkStream = objTcpClient.GetStream();

            //构成XML,满足通讯协议
            StringBuilder xmlBuilder = new StringBuilder();

            string strHead = "<?xml version=\"1.0\" ?><farShine ver=\"1.0\" xmlns=\"http://www.farshine.com\"><farShine ver=\"1.0\"><Operation name=\"" + strCommand + "\">";
            string strCauda = "</Operation></farShine></farShine>";

            string strBoxNumber = "<Box NO = \"" + no + "\" bFront=\"true\" />";
            xmlBuilder.Append(strHead);
            xmlBuilder.Append(strBoxNumber);
            xmlBuilder.Append(strCauda);

            String strSend = xmlBuilder.ToString();

            try
            {
                //'组XML字符串
                // 转换字符串为字符数组，准备发送的内容
                var data = Encoding.ASCII.GetBytes(strSend);
                objNetworkStream.Write(data, 0, data.Length);
                data[0] = 0;
                objNetworkStream.Write(data, 0, 1);
            }
            catch (Exception)
            {
                objTcpClient.Close();
            }
            try
            {
                if (objNetworkStream.CanRead)
                {
                    byte[] dataa = new Byte[256];
                    // 接收回答报文用的存储变量

                    objNetworkStream.Read(dataa, 0, dataa.Length);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public static void HandleWebSocket(string message)
        {
            var obj = JsonConvert.DeserializeObject<dynamic>(message);
            switch (obj.command.ToString())
            {
                case "OpenSerialPort":
                    var serialPortName = obj.serialPortName.ToString();
                    var baudRate = int.Parse(obj.baudRate.ToString());
                    OpenSerialPort(serialPortName, baudRate);
                    break;
                case "CloseSerialPort":
                    CloseSerialPort();
                    break;
                case "GetSerialPortList":
                    GetSerialPortList();
                    break;
                case "GetMacAddress":
                    GetMacAddress();
                    break;
                case "OpenDoor":
                    var no = obj.no.ToString();
                    OpenDoor(no);
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

        private void 退出ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult dialogResult = MessageBox.Show(@"确定退出？", @"系统提示", MessageBoxButtons.OKCancel);
            if (dialogResult == DialogResult.OK)
            {
                Application.Exit();
            }
        }

        private void 关于ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(_version.ToString("0.0"));
        }
    }
}
