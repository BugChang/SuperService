using System;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using Fleck;
using Newtonsoft.Json;
using System.Threading;

namespace SuperService
{
    public partial class FrmMain : Form
    {
        #region 全局变量

        private const int Version = 1;
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
            try
            {
                _serialPort =
                                new SerialPort(comName, baudRate, Parity.None, 8, StopBits.One)
                                {
                                    Handshake = Handshake.RequestToSendXOnXOff
                                };
                _serialPort.DataReceived += _serialPort_DataReceived;
                _serialPort.Open();
            }
            catch
            {

            }

        }

        /// <summary>
        ///关闭串口
        /// </summary>
        public static void CloseSerialPort()
        {
            _serialPort.Close();
        }

        /// <summary>
        /// 向串口写数据
        /// </summary>
        /// <param name="text"></param>
        public static void WirteSerialPort(string text)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                //对于中文的话,要先对其进行编码,将其转换成 _Base64String ,否则你得不到中文字符串的 
                byte[] data = Encoding.Unicode.GetBytes(text);
                string str = Convert.ToBase64String(data);
                _serialPort.WriteLine(str);
                CloseSerialPort();
            }
            else
            {
                MessageBox.Show(@"请先打开串口！");
            }

        }

        /// <summary>
        /// 获取串口列表
        /// </summary>
        /// <returns></returns>
        public static string[] GetSerialPortList()
        {
            string[] serialPortNames = SerialPort.GetPortNames();
            return serialPortNames;
        }

        /// <summary>
        /// 串口读取数据事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void _serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            _serialPort.Encoding = Encoding.GetEncoding("GB2312");
            Thread.Sleep(500);
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
        public void StartWebSocket()
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

        public static string GetMacAddress()
        {
            var macList = MacHelper.GetMacList();
            return macList[0];
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

        public void HandleWebSocket(string message)
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

                    #region 获取串口列表

                    var serialPortList = GetSerialPortList();
                    var serialPortListObj = new
                    {
                        Method = "GetSerialPortList",
                        Data = serialPortList
                    };
                    try
                    {
                        _iConnection.Send(JsonConvert.SerializeObject(serialPortListObj));
                    }
                    catch
                    {
                        // ignored
                    }

                    #endregion

                    break;
                case "GetMacAddress":

                    #region 获取Mac地址

                    var macAddress = GetMacAddress();
                    var macAddressObj = new
                    {
                        Method = "GetMacAddress",
                        Data = macAddress
                    };
                    try
                    {
                        _iConnection.Send(JsonConvert.SerializeObject(macAddressObj));
                    }
                    catch
                    {
                        // ignored
                    }

                    #endregion

                    break;
                case "OpenDoor":
                    var no = obj.no.ToString();
                    OpenDoor(no);
                    break;
                case "WriteCpuCard":

                    #region CPU写卡

                    var text = obj.text.ToString();
                    var port = Convert.ToInt16(obj.port);
                    var rate = Convert.ToInt32(obj.rate);
                    var bWrite = WriteCpuCard(text, port, rate);
                    var writeJson = new
                    {
                        Method = "WriteCpuCard",
                        Data = bWrite
                    };

                    try
                    {
                        _iConnection.Send(JsonConvert.SerializeObject(writeJson));
                    }
                    catch
                    {
                        // ignored
                    }

                    #endregion

                    break;
                case "ReadCpuCard":

                    #region CPU读卡

                    var readPort = Convert.ToInt16(obj.port);
                    var readRate = Convert.ToInt32(obj.rate);
                    var readText = ReadCpuCard(readPort, readRate);
                    var readJson = new
                    {
                        Method = "ReadCpuCard",
                        Data = readText
                    };
                    _iConnection.Send(JsonConvert.SerializeObject(readJson));

                    #endregion

                    break;
                case "OpenCpuCom":
                    var port1 = Convert.ToInt16(obj.port);
                    var rate1 = Convert.ToInt32(obj.rate);
                    OpenCpuCom(port1, rate1);
                    break;
                case "CloseCpuCom":
                    CloseCpuPort();
                    break;
                case "OpenFile":
                    var filePath = obj.filePath.ToString();
                    System.Diagnostics.Process.Start(filePath);
                    break;
                case "TaoHong":
                    var wordPath = obj.filePath.ToString();
                    var oldStr = obj.oldStr.ToString().Split('$');
                    var newStr = obj.newStr.ToString().Split('$');
                    WordReplace(wordPath, oldStr, newStr);
                    _iConnection.Send("ok");
                    break;
            }
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            StartWebSocket();
            axCpuCardOCX1.OnCardIn += AxCpuCardOCX1_OnCardIn;
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
            MessageBox.Show(Version.ToString("0.0"));
        }



        #region CPU卡相关操作

        /// <summary>
        /// 打开CPU读卡器端口
        /// </summary>
        /// <param name="port"></param>
        /// <param name="buadRate"></param>
        /// <returns></returns>
        public bool OpenCpuPort(short port, int buadRate)
        {
            WriteLog("CPU读卡器端口号：" + port);
            WriteLog("CPU读卡器波特率：" + buadRate);
            var bOpen = axCpuCardOCX1.OpenPort(port, buadRate);
            WriteLog("CPU读卡器端口打开状态：" + bOpen);
            return bOpen;
        }

        /// <summary>
        /// 关闭CPU读卡器端口
        /// </summary>
        public void CloseCpuPort()
        {
            axCpuCardOCX1.ClosePort();
            WriteLog("CPU读卡器端口关闭！");
        }

        /// <summary>
        /// CPU写卡数据
        /// </summary>
        /// <param name="text"></param>
        /// <param name="port"></param>
        /// <param name="baudRate"></param>
        /// <returns></returns>
        public bool WriteCpuCard(string text, short port, int baudRate)
        {
            try
            {
                var bOpen = OpenCpuPort(port, baudRate);
                if (bOpen)
                {
                    WriteLog("CPU读卡器写卡原始文本：" + text);
                    byte[] data = Encoding.UTF8.GetBytes(text);
                    string str = Convert.ToBase64String(data);
                    WriteLog("CPU读卡器写卡Base64文本：" + str);
                    var result = axCpuCardOCX1.SetFileDataBinBase64(str);
                    WriteLog("CPU读卡器写卡状态：" + result);
                    CloseCpuPort();
                    return result;
                }
            }
            catch (Exception e)
            {
                WriteLog("写卡报错：" + e.Message);
            }
            return false;
        }

        public void OpenCpuCom(short port, int baudRate)
        {
            CloseCpuPort();
            OpenCpuPort(port, baudRate);
        }

        public void CloseCpuCom()
        {
            CloseCpuPort();
        }

        /// <summary>
        /// CPU读卡
        /// </summary>
        /// <returns></returns>
        public string ReadCpuCard(short port, int rate)
        {
            try
            {
                var bOpen = OpenCpuPort(port, rate);
                if (bOpen)
                {
                    var text = axCpuCardOCX1.GetFileDataBinBase64();
                    WriteLog("CPU卡Base64文本：" + text);
                    byte[] outputb = Convert.FromBase64String(text);
                    string orgStr = Encoding.UTF8.GetString(outputb);
                    WriteLog("CPU卡解码后文本：" + orgStr);
                    CloseCpuPort();
                    return orgStr;
                }

            }
            catch (Exception e)
            {
                WriteLog("读卡报错：" + e.Message);
            }
            return "";
        }

        private void AxCpuCardOCX1_OnCardIn(object sender, AxCPUCARDOCXLib._DCpuCardOCXEvents_OnCardInEvent e)
        {
            var carNoJson = new
            {
                Method = "GetCpuCardNo",
                Data = e.cardNO.ToUpper()
            };
            _iConnection.Send(JsonConvert.SerializeObject(carNoJson));
        }

        #endregion

        private delegate void WriteLogDelegate(string message);

        /// <summary>
        /// 记录日志
        /// </summary>
        /// <param name="message"></param>
        private void WriteLog(string message)
        {
            if (rtxtLog.InvokeRequired == false)
                rtxtLog.AppendText(DateTime.Now + "|" + message + "\r\n");
            else
            {
                var writeLogDelegate = new WriteLogDelegate(WriteLog);
                rtxtLog.Invoke(writeLogDelegate, message);
            }
        }

        /// <summary>
        /// 替换word中的文字
        /// </summary>
        /// <param name="filePath">文件的路径</param>
        /// <param name="strOld">查找的文字</param>
        /// <param name="strNew">替换的文字</param>
        private void WordReplace(string filePath, string[] strOld, string[] strNew)
        {
            Microsoft.Office.Interop.Word._Application app = new Microsoft.Office.Interop.Word.ApplicationClass();
            object nullobj = System.Reflection.Missing.Value;
            object file = filePath;
            Microsoft.Office.Interop.Word._Document doc = app.Documents.Open(
            ref file, ref nullobj, ref nullobj,
            ref nullobj, ref nullobj, ref nullobj,
            ref nullobj, ref nullobj, ref nullobj,
            ref nullobj, ref nullobj, ref nullobj,
            ref nullobj, ref nullobj, ref nullobj, ref nullobj) as Microsoft.Office.Interop.Word._Document;
            for (int i = 0; i < strOld.Length; i++)
            {
                app.Selection.Find.ClearFormatting();
                app.Selection.Find.Replacement.ClearFormatting();
                app.Selection.Find.Text = strOld[i];
                app.Selection.Find.Replacement.Text = strNew[i];
                object objReplace = Microsoft.Office.Interop.Word.WdReplace.wdReplaceAll;
                app.Selection.Find.Execute(ref nullobj, ref nullobj, ref nullobj,
                                           ref nullobj, ref nullobj, ref nullobj,
                                           ref nullobj, ref nullobj, ref nullobj,
                                           ref nullobj, ref objReplace, ref nullobj,
                                           ref nullobj, ref nullobj, ref nullobj);
            }

            //格式化
            //doc.Content.AutoFormat();
            //清空Range对象
            //Microsoft.Office.Interop.Word.Range range = null;
            //保存
            doc.Save();
            //Microsoft.Office.Interop.Word.Range range = null;
            doc.Close(ref nullobj, ref nullobj, ref nullobj);
            app.Quit(ref nullobj, ref nullobj, ref nullobj);
        }
    }
}
