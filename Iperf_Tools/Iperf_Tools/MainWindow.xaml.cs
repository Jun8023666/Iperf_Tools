using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;    
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Diagnostics;

namespace Iperf_Tools
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public static bool PC_State = false;
        public static string IP_Adress_Str = "";
        public static int IP_PortNum = 00;
        public MainWindow()
        {
            InitializeComponent();
            IP_Com.Items.Clear();
            IPAddress[] IP = IP_Refresh();
            foreach (IPAddress ip in IP)
            {
                if (ip.ToString().Contains("."))
                {
                    IP_Com.Items.Add(ip.ToString());
                    IP_Com.Text = ip.ToString();
                }
            }
            Thread MainThread = new Thread(MainThread_Windows);
            MainThread.Start();
            Infor_Text.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
            Infor_Text.Text = "Welcome To GTS, tools open successful!\r\n\r\n";
        }
        static void CallWithTimeout(Action action, int timeoutMilliseconds)
        {
            Thread threadToKill = null;
            Action wrappedAction = () =>
            {
                threadToKill = Thread.CurrentThread;
                action();
            };

            IAsyncResult result = wrappedAction.BeginInvoke(null, null);
            if (result.AsyncWaitHandle.WaitOne(timeoutMilliseconds))
            {
                wrappedAction.EndInvoke(result);
            }
            else
            {
                threadToKill.Abort();
                //throw new TimeoutException();
            }
        }
        public static string ReadLine_str = "";
        private void MainThread_Windows()
        {
            while (true)
            {
                bool IsComing = false;
                string Str="";
                if (PC_State == true)
                {
                    while (!IsComing)
                    {
                        try
                        {
                            Str = Communication_Receive(11111);
                            if (Str.Contains("&&"))
                                IsComing = true;
                            else continue;
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    string Pre_IP = Str.Substring(Str.IndexOf("//") + 2, Str.IndexOf("&&") - Str.IndexOf("//")-2);
                    string mode = Str.Substring(Str.IndexOf("&&") + 2, Str.Length - Str.IndexOf("&&") - 2);
                    Str = Str.Substring(0,Str.IndexOf("//"));
                    TimeSpan nowtime;
                    this.Dispatcher.Invoke(
                        new Action(
                            delegate
                            {
                                Infor_Text.Text += Str + "\r\n";
                                Infor_Text.Text += "Communication Start..." + "(time:" + DateTime.Now.ToString("hh:mm:ss")+")\r\n";
                                Infor_Text.ScrollToEnd();
                            })
                        );
                    
                    if (mode.Contains("Server"))
                    {
                        KillALLProcess("", "iperf"); KillALLProcess("", "cmd");
                        Process p_server = new Process();
                        p_server.StartInfo.FileName = "cmd.exe";
                        //p_server.StartInfo.WorkingDirectory = "..\\Debug\\iperf2";
                        p_server.StartInfo.UseShellExecute = false;        //是否使用操作系统shell启动
                        p_server.StartInfo.RedirectStandardInput = true;   //接受来自调用程序的输入信息
                        p_server.StartInfo.RedirectStandardOutput = true;  //由调用程序获取输出信息
                        p_server.StartInfo.RedirectStandardError = true;   //重定向标准错误输出
                        p_server.StartInfo.CreateNoWindow = true;          //不显示程序窗口
                        p_server.Start();//启动程序
                        p_server.StandardInput.WriteLine(Str);

                        // 判断服务器是否开启成功
                        string server_str = p_server.StandardOutput.ReadLine();
                        while (true)
                        {
                            string str = p_server.StandardOutput.ReadLine();
                            if (str == null) break;
                            server_str += str;
                            if (server_str.Contains("failed") || server_str.Contains("listening on"))
                                break;
                        }
                        if (!server_str.Contains("listening on"))
                        {
                            KillALLProcess("", "iperf");
                        }

                        Communication_Send("PC_Over", Pre_IP, 11111);
                        //CallWithTimeout(ReadBuf, 2000);// for end
                        this.Dispatcher.Invoke(
                        new Action(
                            delegate
                            {
                                //Infor_Text.Text += Str;
                                Infor_Text.Text += "Communication Successful..." + "(time:" + DateTime.Now.ToString("hh:mm:ss") + ")\r\n\r\n";
                                Infor_Text.ScrollToEnd();
                            })
                        );
                        
                        if (Str.Contains("Over"))
                        { KillALLProcess("", "iperf"); KillALLProcess("", "cmd"); }
                    }
                    else if (mode.Contains("Client"))
                    {
                        Communication_Send("OK to client", Pre_IP, 11111);
                        using (Process p_client = new Process())
                        {
                            p_client.StartInfo.FileName = "cmd.exe";
                            //p_client.StartInfo.WorkingDirectory = "..\\Debug\\iperf2\\";
                            p_client.StartInfo.UseShellExecute = false;        //是否使用操作系统shell启动
                            p_client.StartInfo.RedirectStandardInput = true;   //接受来自调用程序的输入信息
                            p_client.StartInfo.RedirectStandardOutput = true;  //由调用程序获取输出信息
                            p_client.StartInfo.RedirectStandardError = true;   //重定向标准错误输出
                            p_client.StartInfo.CreateNoWindow = true;          //不显示程序窗口
                            p_client.Start();//启动程序

                            //improve 
                            p_client.OutputDataReceived += new DataReceivedEventHandler(SortOutputHandler);
                            p_client.BeginOutputReadLine();

                            p_client.StandardInput.WriteLine(Str);
                            int exit_cnt = 2;
                            string output = "";
                            List<string> ReturnStr = new List<string>();
                            bool IsRunAgain = false;
                            while (true)
                            {
                                ReadLine_str = "";
                                try
                                {
                                    string str = Communication_Receive(11111);
                                    if (str == "stop")
                                    {
                                        IsRunAgain = true;
                                        break;
                                    }
                                }
                                catch
                                {

                                }
                                output = ReadLine_str;
                                //string str = p_server.StandardOutput.ReadLine();
                                if ((output != "") && (output != null))
                                {
                                    string[] sArray = output.Split('&');
                                    foreach (string str in sArray)
                                    {
                                        ReturnStr.Add(str);
                                        Communication_Send(str.Replace("&",""), Pre_IP, 11111);
                                    }
                                }
                                else if (output == "")
                                {
                                    exit_cnt--;
                                    if (exit_cnt == 0)
                                    {
                                        break;
                                    }
                                }
                            }
                            if (IsRunAgain == true) continue;
                            Thread.Sleep(100);
                            Communication_Send("Over", Pre_IP, 11111);
                            Thread.Sleep(100);
                            p_client.WaitForExit();//等待程序执行完退出进程
                            //p_client.Kill();
                            p_client.Close();
                            KillALLProcess("", "iperf"); KillALLProcess("", "cmd");
                            //p_client.CloseMainWindow();
                            int i = 1;
                            while (i != 0)
                            {
                                ReturnStr.Add("Measurement Over");
                                i--;
                            }
                            foreach(string str in ReturnStr)
                            {
                                Communication_Send(str, Pre_IP, 11111);
                                Thread.Sleep(30);
                            }
                            this.Dispatcher.Invoke(
                            new Action(
                            delegate
                            {
                                Infor_Text.Text += "Communication Successful..." + "(time:" + DateTime.Now.ToString("hh:mm:ss") + ")\r\n\r\n";
                                Infor_Text.ScrollToEnd();
                            })
                            );
                        }
                    }
                }
                Thread.Sleep(10);
            }
        }
        private static void SortOutputHandler(object sendingProcess,DataReceivedEventArgs outLine)
        {
            try
            {
                ReadLine_str += outLine.Data.ToString() + "&";
            }
            catch
            {

            }
        }
        public static void KillALLProcess(string processName1, string processName2)
        {
            try
            {
                Process[] myproc = Process.GetProcesses();
                foreach (Process item in myproc)
                {
                    if ((item.ProcessName == processName1) || (item.ProcessName == processName2))
                    {
                        item.Kill();
                    }
                }
                Thread.Sleep(50);
                client = new UdpClient(11111);
                client.Client.ReceiveTimeout = 1500;
            }
            catch
            {

            }
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if(StartButton.Content.ToString() == "Start")
            {
                PC_State = true;
                StartButton.Content = "Stop";
                IP_Com.IsEnabled = false;
                IP_PORT.IsEnabled = false;
                IP_Adress_Str = IP_Com.Text;
                IP_PortNum = Convert.ToInt32(IP_PORT.Text);
                Infor_Text.Text += "Open Net and Port successful!\r\n";
            }
            else 
            {
                if (MessageBox.Show("Stop the Iperf Tools?", "Confirm Message", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    PC_State = false;
                    StartButton.Content = "Start";
                    IP_Com.IsEnabled = true;
                    IP_PORT.IsEnabled = true;
                    KillALLProcess("", "iperf"); KillALLProcess("", "cmd");
                    Infor_Text.Text += "Close Net and Port successful!\r\n";

                    client.Close();
                    client = null;
                }
            }
        }

        public static UdpClient client;
        private string Communication_Receive(int IP_Port)
        {

            string receiveStr = null;
            byte[] receiveData = null;

            IPEndPoint remotePoint = new IPEndPoint(IPAddress.Any, IP_Port);

            try
            {
                receiveData = client.Receive(ref remotePoint);
            }
            catch
            {
                this.Dispatcher.Invoke(
                new Action(
                            delegate
                            {
                                Infor_Text.Text += "Newwork Connection with DUT fail, Please check" + "\r\n\r\n";
                                Infor_Text.ScrollToEnd();
                            }));
            }

            receiveStr = Encoding.Default.GetString(receiveData);
            remotePoint = null;
            return receiveStr;
        }

        public static void Communication_Send(string SendInfor, string IpAdress, int IP_Port)
        {
            string sendStr = null;
            byte[] sendData = null;
            UdpClient client = null;

            IPAddress remoteIP = IPAddress.Parse(IpAdress);
            int remotePort = IP_Port;
            IPEndPoint remotePoint = new IPEndPoint(remoteIP, remotePort);

            sendStr = SendInfor;
            sendData = Encoding.Default.GetBytes(sendStr);

            client = new UdpClient();
            client.Send(sendData, sendData.Length, remotePoint);
            client.Close();
        }


        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            IP_Com.Items.Clear();
            IPAddress[] IP = IP_Refresh();
            foreach (IPAddress ip in IP)
            {
                if (ip.ToString().Contains("."))
                {
                    IP_Com.Items.Add(ip.ToString());
                    IP_Com.Text = ip.ToString();
                }
            }
        }

        public static IPAddress[] IP_Refresh()
        {
            IPAddress[] IP = Dns.GetHostAddresses(Dns.GetHostName());
            return IP;
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            Infor_Text.Text = "Welcome To GTS, tools open successful!\r\n\r\n";
        }


    }
}
