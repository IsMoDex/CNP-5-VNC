using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace VNC_Client
{
    class Client
    {
        TcpClient client;

        int _dispouse = 0;
        bool HandleContinue = false;

        List<string> StackRequests = new List<string>();

        public string IP { get; private set; }
        public int Port { get; private set; }
        public BitmapImage GetImage { get; private set; }

        public bool Connected
        {
            get
            {
                try
                {
                    return client.Connected;
                } 
                catch
                {
                    return false;
                }
            }
        }

        public Client()
        {
            client = new TcpClient();
        }

        public void StartHandle()
        {
            if (HandleContinue)
                return;

            HandleContinue = true;

            Thread thred = new Thread(HandleOperation);
            thred.Start();
        }

        public void StopHandle() => HandleContinue = false;

        private void HandleOperation()
        {
            int CountTimeOut = 0;

            while(_dispouse == 0 && HandleContinue)
            {
                if (StackRequests.Count == 0)
                    continue;

                var command = StackRequests.First();

                Console.WriteLine("Количество запросов: " + StackRequests.Count);

                try
                {
                    switch (command)
                    {
                        case "FramebufferUpdateRequest":
                            UpdateFrame_Request();
                            break;

                        case "MouseEventMoveUpdateRequest":
                            SendMouseMoveParamentries_Request(StackRequests[1]);
                            StackRequests.RemoveAt(1);
                            break;

                        case "MouseEventButtonUpdateRequest":
                            SendMouseButtonParamentries_Request(StackRequests[1]);
                            StackRequests.RemoveAt(1);
                            break;

                        default: return;
                    }

                    CountTimeOut = 0;
                }
                catch (IOException ex) when (ex.InnerException is SocketException socketException && socketException.SocketErrorCode == SocketError.TimedOut)
                {
                    CountTimeOut++;
                    // Обработка ошибки таймаута
                    Console.WriteLine($"Произошел таймаут №{CountTimeOut} при чтении/записи данных из/в NetworkStream.");

                    if (CountTimeOut > 10)
                        return;
                }
                finally
                {
                    StackRequests.Remove(command);
                }

            }

            Console.WriteLine();
        }

        public void Connect(string serverIp, int port)
        {
            IP = serverIp;
            Port = port;

            // Подключаемся к серверу
            client.Connect(serverIp, port);
            StartHandle();
            Console.WriteLine("Подключено к серверу");
        }

        public void CloseClient()
        {
            StopHandle();
            // Закрываем соединение
            client.Close();
        }

        string OldRequest = string.Empty;

        private BitmapImage ReceiveImage()
        {
            NetworkStream stream = client.GetStream();
            stream.ReadTimeout = 1500;

            // Читаем размер изображения из потока
            byte[] sizeData = new byte[4];
            stream.Read(sizeData, 0, sizeData.Length);
            int imageSize = BitConverter.ToInt32(sizeData, 0);

            // Читаем данные изображения из потока
            byte[] imageData = new byte[imageSize];
            int totalBytesRead = 0;
            while (totalBytesRead < imageSize)
            {
                int bytesRead = stream.Read(imageData, totalBytesRead, imageSize - totalBytesRead);
                if (bytesRead == 0)
                    throw new IOException("Недостаточно данных для чтения");
                totalBytesRead += bytesRead;
            }

            // Создаем BitmapImage из полученных данных
            BitmapImage image = new BitmapImage();
            using (MemoryStream memoryStream = new MemoryStream(imageData))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = memoryStream;
                image.EndInit();
            }

            return image;
        }

        public void UpdateFrame() => AddInStackRequest("FramebufferUpdateRequest");

        private void UpdateFrame_Request()
        {
            SendMessageToServer("FramebufferUpdateRequest"); ///Request

            // Получаем изображение от сервера
            BitmapImage receivedImage = ReceiveImage();

            GetImage = receivedImage;
        }

        private string GetMessageForServer()
        {
            NetworkStream stream = client.GetStream();

            byte[] buffer = new byte[1024];

            stream.Read(buffer, 0, buffer.Length);

            string message = Encoding.Unicode.GetString(buffer);

            return message;
        }

        private void SendMessageToServer(string message)
        {
            byte[] data = Encoding.Unicode.GetBytes(message);

            SendMessageToServer(data);
        }

        private void SendMessageToServer(byte[] data)
        {
            NetworkStream stream = client.GetStream();
            stream.Write(data, 0, data.Length);
        }

        public void SendMouseMoveParamentries(Point MousePosition)
        {
            string message = $"{MousePosition.X},{MousePosition.Y}";
            
            AddInStackRequest("MouseEventMoveUpdateRequest");
            AddInStackRequest(message);
        }

        private void SendMouseMoveParamentries_Request(string message) 
        {
            // Отправка сообщения на сервер
            SendMessageToServer("MouseEventMoveUpdateRequest"); ///Request
            SendMessageToServer(message);

            if (GetMessageForServer() != "yes")
                throw new ArgumentException("Ожидаемый ответ от сервера не получен!");
        }

        public void SendMouseButtonParamentries(bool leftButtonDown, bool rightButtonDown)
        {
            string message = $"{leftButtonDown},{rightButtonDown}";

            AddInStackRequest("MouseEventButtonUpdateRequest");
            AddInStackRequest(message);
        }

        private void SendMouseButtonParamentries_Request(string message)
        {
            // Отправка сообщения на сервер
            SendMessageToServer("MouseEventButtonUpdateRequest"); ///Request
            SendMessageToServer(message);

            if (GetMessageForServer() != "yes")
                throw new ArgumentException("Ожидаемый ответ от сервера не получен!");
        }

        private void AddInStackRequest(string request) => StackRequests.Add(request);

        ~Client() => _dispouse = 1;
    }
}
