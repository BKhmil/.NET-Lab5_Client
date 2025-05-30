using System;
using System.IO.Pipes;
using System.Threading;
using System.Windows.Forms;

namespace Lab5_Client
{
    public partial class Form1 : Form
    {   // Константа для імені іменованого каналу
        private const string PIPE_NAME = "circle_pipe";

        // Змінні для координат (x, y) та радіусу (r) кола
        private int x = -1, y = -1, r = -1;

        // Прапорець для контролю роботи потоку зчитування
        private volatile bool isRunning;

        // Об'єкт клієнта іменованого каналу
        private NamedPipeClientStream? pipeClient;

        // Потік для зчитування даних
        private Thread? receiveThread;

        public Form1()
        {
            InitializeComponent();

            // Увімкнення подвійної буферизації для плавного малювання
            DoubleBuffered = true;

            // Запуск клієнта для з'єднання з сервером
            StartClient();
        }

        // Метод для запуску клієнта та зчитування даних з іменованого каналу
        // Створює фоновий потік, який:
        // -> намагається підключитися до сервера через іменований канал
        // -> зчитує рядки з даними про коло (x, y, r)
        // -> парсить отримані дані та оновлює координати для малювання
        private void StartClient()
        {
            isRunning = true;
            receiveThread = new Thread(() =>
            {
                while (isRunning)
                {
                    try
                    {
                        using (pipeClient = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.In))
                        {
                            // Підключення до сервера з таймаутом 5 секунд
                            pipeClient.Connect(5000);
                            using (StreamReader reader = new StreamReader(pipeClient))
                            {
                                while (isRunning && pipeClient.IsConnected)
                                {
                                    // Зчитування рядка з даними
                                    string? line = reader.ReadLine();
                                    if (line == null) break;

                                    // Парсинг даних та оновлення координат
                                    if (ParseCircleData(line, out int parsedX, out int parsedY, out int parsedR))
                                    {
                                        // Оновлення UI в основному потоці
                                        Invoke((Action)(() =>
                                        {
                                            x = parsedX;
                                            y = parsedY;
                                            r = parsedR;
                                            Invalidate(); // Перемалювання форми
                                        }));
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Затримка перед повторною спробою підключення
                        if (isRunning) Thread.Sleep(1000);
                    }
                }
            });
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }

        // Метод для парсингу рядка з даними про коло
        // Очікує формат "x,y,r", повертає true, якщо парсинг успішний
        private bool ParseCircleData(string line, out int x, out int y, out int r)
        {
            x = y = r = -1;
            string[] parts = line.Split(',');
            return parts.Length == 3 &&
                   int.TryParse(parts[0], out x) &&
                   int.TryParse(parts[1], out y) &&
                   int.TryParse(parts[2], out r);
        }

        // Метод для малювання кола на формі
        // Малює коло з координатами (x, y) та радіусом r, якщо вони валідні
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (x >= 0 && y >= 0 && r > 0)
            {
                using (Pen pen = new Pen(Color.Blue, 3))
                {
                    // Малювання кола з центром (x, y) та діаметром 2*r
                    e.Graphics.DrawEllipse(pen, x - r, y - r, r * 2, r * 2);
                }
            }
        }

        // Обробник закриття форми
        // Зупиняє потік зчитування та закриває канал
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            isRunning = false;
            pipeClient?.Close();
            pipeClient?.Dispose();
        }
    }

}