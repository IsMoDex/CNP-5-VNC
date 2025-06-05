using System;
using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using WindowsInput;

namespace VNC_Server
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Server server;

        

        public MainWindow()
        {
            InitializeComponent();

            Closing += MainWindow_Closing;

            Start();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                server.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void Start()
        {
            try
            {
                IPAddress ip = IPAddress.Any;
                int port = 9000;

                server = new Server(ip, port);

                Label_IPPort.Text = $"{port}";

                // Создаем экземпляр сервера и запускаем его
                server.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        static string GetLocalIPAddress()
        {
            // Получаем имя текущего хоста
            string hostName = Dns.GetHostName();

            // Получаем информацию о текущем хосте
            IPHostEntry hostEntry = Dns.GetHostEntry(hostName);

            // Ищем IPv4 адреса, которые не являются адресами петли
            foreach (IPAddress ipAddress in hostEntry.AddressList)
            {
                if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(ipAddress))
                {
                    return ipAddress.ToString();
                }
            }

            return null;
        }

    }
}