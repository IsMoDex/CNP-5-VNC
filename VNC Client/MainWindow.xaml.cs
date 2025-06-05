using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
using System.Windows.Threading;
using static System.Net.Mime.MediaTypeNames;
using Application = System.Windows.Application;

namespace VNC_Client
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Client client = new Client();
        object locker = new object(); //locker
        int _disposed = 0;
        CancellationTokenSource updateTokenSource;

        public MainWindow()
        {
            InitializeComponent();
            Closing += MainWindow_Closing; // Подписываемся на событие закрытия окна
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                updateTokenSource?.Cancel();
                client.CloseClient();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public async Task Start(string IP, int Port)
        {
            try
            {
                client.Connect(IP, Port);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            updateTokenSource = new CancellationTokenSource();
            _ = Task.Run(() => UpdateLoop(IP, Port, updateTokenSource.Token));

            await Task.CompletedTask;

            do
            {
                //client.UpdateFrame();
                //await Task.Delay(TimeSpan.FromMilliseconds(700)); // Ожидаем задержку
                //ImagePage.Source = client.GetImage;

                //await Task.Run(() =>
                //{
                //    client.UpdateFrame();
                //    Task.Delay(TimeSpan.FromMilliseconds(100)).Wait(); // Ожидаем задержку
                //});

                //Application.Current.Dispatcher.Invoke(() =>
                //{
                //    ImagePage.Source = client.GetImage;
                //});

                BitmapImage receivedImage = await Task.Run(() =>
                {
                    client.UpdateFrame();
                    return client.GetImage;
                });

                BitmapImage clonedImage = CloneBitmapImage(receivedImage); // Клонируем изображение

                ImagePage.Source = clonedImage;

                await Task.Delay(TimeSpan.FromMilliseconds(100)); // Ожидаем задержку


            } while (_disposed == 0);

        }

        private BitmapImage CloneBitmapImage(BitmapImage source)
        {
            if (source == null)
                return null;

            BitmapImage clone = new BitmapImage();

            using (MemoryStream memoryStream = new MemoryStream())
            {
                BitmapEncoder encoder = new PngBitmapEncoder(); // Используйте соответствующий кодировщик для вашего случая
                encoder.Frames.Add(BitmapFrame.Create(source));
                encoder.Save(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);

                clone.BeginInit();
                clone.CacheOption = BitmapCacheOption.OnLoad;
                clone.StreamSource = memoryStream;
                clone.EndInit();
            }

            return clone;
        }

        private async Task UpdateLoop(string ip, int port, CancellationToken token)
        {
            while (_disposed == 0 && ip == client.IP && port == client.Port && !token.IsCancellationRequested)
            {
                try
                {
                    client.UpdateFrame();
                    BitmapImage receivedImage = client.GetImage;
                    BitmapImage clonedImage = CloneBitmapImage(receivedImage);
                    await Dispatcher.InvokeAsync(() => ImagePage.Source = clonedImage, DispatcherPriority.Background, token);
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() => MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error));
                    break;
                }

                try
                {
                    await Task.Delay(100, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            string[] connectionData = ConnectionDataTextBox.Text.Split(':');

            if (connectionData.Length != 2 || !int.TryParse(connectionData[1], out int port))
            {
                MessageBox.Show("Неверные данные для подключения!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                Start(connectionData[0], port);
            }
            catch (Exception ex) 
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }


        private bool isProcessing = false; // Флаг, указывающий, выполняется ли метод в данный момент
        private async void ImagePage_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isProcessing) // Проверяем, выполняется ли метод уже
            {
                isProcessing = true; // Устанавливаем флаг, что метод начал выполнение

                Point positionRelativeToImage = e.GetPosition(ImagePage); // Получаем координаты относительно изображения

                //Размер рабочего стола
                double screenWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
                double screenHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;

                // Позиция мыши относительно изображения
                double xRelativeToImage = positionRelativeToImage.X;
                double yRelativeToImage = positionRelativeToImage.Y;

                //Масштаб, во сколько раз меньше изображение
                double ScaleWidth = screenWidth / ImagePage.ActualWidth;
                double ScaleHeight = screenHeight / ImagePage.ActualHeight;

                client.SendMouseMoveParamentries(new Point(xRelativeToImage * ScaleWidth, yRelativeToImage * ScaleHeight));

                // Добавляем задержку в 0.25 секунд
                await Task.Delay(250);

                isProcessing = false; // Устанавливаем флаг, что метод завершил выполнение
            }
        }

        private void ImagePage_MouseDown(object sender, MouseButtonEventArgs e) => MouseUpDown(e);

        private void ImagePage_MouseUp(object sender, MouseButtonEventArgs e) => MouseUpDown(e);

        private void MouseUpDown(MouseButtonEventArgs e)
        {
            bool leftbuttonDown = e.LeftButton == MouseButtonState.Pressed;
            bool rightbuttonDown = e.RightButton == MouseButtonState.Pressed;
            bool middleButtonDown = e.MiddleButton == MouseButtonState.Pressed;
            bool xButton1Down = e.XButton1 == MouseButtonState.Pressed;
            bool xButton2Down = e.XButton2 == MouseButtonState.Pressed;

            client.SendMouseButtonParamentries(leftbuttonDown, rightbuttonDown, middleButtonDown, xButton1Down, xButton2Down);
        }

        private void ImagePage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            client.SendMouseWheelParamentries(e.Delta);
        }
    }

}
