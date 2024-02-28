using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsInput;

namespace VNC_Server
{
    class Server
    {
        const int SIZE_BUFFER = 1024;

        int _disposed = 0;
        int _clientCount = 0;
        TcpListener listener;

        InputSimulator simulator;

        public Server() : this(IPAddress.Any, 12346)
        {

        }
        public Server(IPAddress address, int port)
        {
            // Создаем TCP слушатель
            listener = new TcpListener(address, port);
            simulator = new InputSimulator();
        }

        public async void Start()
        {
            listener.Start();
            Console.WriteLine("Сервер запущен. Ожидание подключений...");

            await Task.Run(() =>
            {
                while (_disposed == 0)
                {
                    TcpClient client = null;

                    // Принимаем клиента
                    try
                    {
                        client = listener.AcceptTcpClient();
                        Console.WriteLine("Подключен клиент");

                    }
                    catch (SocketException ex)
                    {
                        Console.WriteLine(ex.ToString());
                        return;
                    }

                    //// Получаем изображение
                    //BitmapImage image = CaptureScreen();

                    //// Отправляем изображение клиенту
                    //SendImage(client.GetStream(), image);

                    //// Закрываем соединение с клиентом
                    //client.Close();

                    Thread clientThread = new Thread(new ParameterizedThreadStart(HandlerClient));
                    clientThread.Start(client);
                }
            });

        }

        public void Stop()
        {
            Interlocked.Increment(ref _disposed);
            listener.Stop();
        }

        private void HandlerClient(object obj)
        {
            Interlocked.Increment(ref _clientCount);

            TcpClient client = (TcpClient)obj;
            IPEndPoint endPointClient = client.Client.RemoteEndPoint as IPEndPoint;

            NetworkStream stream = client.GetStream();
            //stream.ReadTimeout = 300000; //5 Минут

            try
            {
                while (client.Connected && _disposed == 0)
                {
                    //byte[] data = new byte[SIZE_BUFFER];
                    //int bytes = 0;

                    //StringBuilder builder = new StringBuilder();

                    //do
                    //{
                    //    bytes = stream.Read(data, 0, data.Length);

                    //    builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                    //} while (stream.DataAvailable);

                    string message = ReadMessageStrigFromClient(stream, SIZE_BUFFER);

                    switch (message/*builder.ToString()*/)
                    {
                        case "FramebufferUpdateRequest":
                            // Получаем изображение
                            BitmapImage image = CaptureScreen();
                            // Отправляем изображение клиенту
                            SendImage(client.GetStream(), image);
                            break;

                        case "MouseEventMoveUpdateRequest":
                            //MouseEventMoveUpdateRequest(client.GetStream());
                            break;

                        case "MouseEventButtonUpdateRequest":
                            MouseEventClickUpdateRequest(client.GetStream());
                            break;
                    }

                }

            }
            catch (System.IO.IOException ex) when (ex.InnerException is SocketException se && se.ErrorCode == 10060)
            {
                string errorMessage = "Ошибка связи с клиентом: Клиент долго не отвечал.";

                MessageBox.Show(errorMessage, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine(errorMessage + "\n" + ex.Message);
            }
            catch (System.IO.IOException ex) when (ex.InnerException is SocketException se && se.SocketErrorCode == SocketError.ConnectionReset)
            {
                string errorMessage = "Ошибка связи с клиентом: Клиент принудительно разорвал соединение.";

                MessageBox.Show(errorMessage, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine(errorMessage + "\n" + ex.Message);
            }
            catch (Exception ex)
            {
                string errorMessage = "Ошибка чтения данных: " + ex.Message;

                MessageBox.Show(errorMessage, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine(errorMessage);
            }
            finally
            {
                string errorMessage = $"Соединение с клиентом {endPointClient.Address}:{endPointClient.Port} было разорвано!";

                client.Close();

                MessageBox.Show(errorMessage, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine(errorMessage);

                Interlocked.Decrement(ref _clientCount);
            }

            //client.Close();

        }

        private void MouseEventClickUpdateRequest(NetworkStream stream)
        {
            string message = ReadMessageStrigFromClient(stream, SIZE_BUFFER);

            // Разбор сообщения
            string[] parts = message.Split(',');
            bool leftButtonDown = Convert.ToBoolean(parts[0]);
            bool rightButtonDown = Convert.ToBoolean(parts[1]);

            Action ResetClickMouseOperation = () =>
            {
                if (leftButtonDown && Mouse.LeftButton != MouseButtonState.Pressed)
                {
                    simulator.Mouse.LeftButtonDown();
                }
                else if (!leftButtonDown && Mouse.LeftButton == MouseButtonState.Pressed)
                {
                    simulator.Mouse.LeftButtonUp();
                }

                if (rightButtonDown && Mouse.RightButton != MouseButtonState.Pressed)
                {
                    simulator.Mouse.RightButtonDown();
                }
                else if (!rightButtonDown && Mouse.RightButton == MouseButtonState.Pressed)
                {
                    simulator.Mouse.RightButtonUp();
                }
            };

            if (App.Current.Dispatcher.CheckAccess())
            {
                ResetClickMouseOperation();
            }
            else
            {
                App.Current.Dispatcher.Invoke(ResetClickMouseOperation);
            }

        }

        private void MouseEventMoveUpdateRequest(NetworkStream stream)
        {
            string message = ReadMessageStrigFromClient(stream, SIZE_BUFFER);

            // Разбор сообщения
            string[] parts = message.Split(',');
            //bool leftButtonDown = Convert.ToBoolean(parts[0]);
            //bool rightButtonDown = Convert.ToBoolean(parts[1]);
            int mouseX = Convert.ToInt32($"{parts[0]}");
            int mouseY = Convert.ToInt32($"{parts[2]}");

            //if (leftButtonDown)
            //    simulator.Mouse.LeftButtonDown();
            //else
            //    simulator.Mouse.LeftButtonUp();

            //if (rightButtonDown)
            //    simulator.Mouse.RightButtonDown();
            //else
            //    simulator.Mouse.RightButtonUp();

            MouseOperator.SetCursorPos(mouseX, mouseY);
            //simulator.Mouse.MoveMouseToPositionOnVirtualDesktop(mouseX, mouseY);
        }

        private string ReadMessageStrigFromClient(NetworkStream stream, int BufferSize)
        {
            byte[] data = new byte[SIZE_BUFFER];
            int bytes = 0;

            StringBuilder builder = new StringBuilder();

            do
            {
                bytes = stream.Read(data, 0, data.Length);

                builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
            }
            while (stream.DataAvailable);

            return builder.ToString();
        }

        private BitmapImage CaptureScreen()
        {
            // Получаем параметры главного экрана
            System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.PrimaryScreen;
            int screenWidth = screen.Bounds.Width;
            int screenHeight = screen.Bounds.Height;

            // Создаем объект Bitmap для захвата изображения экрана
            Bitmap bitmap = new Bitmap(screenWidth, screenHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(screenWidth, screenHeight), CopyPixelOperation.SourceCopy);

            // Конвертируем Bitmap в BitmapImage
            using (MemoryStream memoryStream = new MemoryStream())
            {
                bitmap.Save(memoryStream, ImageFormat.Png);
                memoryStream.Position = 0;

                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.EndInit();

                return bitmapImage;
            }
        }

        private void SendImage(NetworkStream stream, BitmapImage image)
        {
            // Преобразовываем BitmapImage в массив байтов
            byte[] imageData;
            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (MemoryStream memoryStream = new MemoryStream())
            {
                encoder.Save(memoryStream);
                imageData = memoryStream.ToArray();
            }

            // Отправляем размер изображения
            byte[] sizeData = BitConverter.GetBytes(imageData.Length);
            stream.Write(sizeData, 0, sizeData.Length);

            // Отправляем само изображение
            stream.Write(imageData, 0, imageData.Length);
        }
    }
}
