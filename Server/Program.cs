using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Server
{
    public class Program
    {
        static string jsonFile = "settings.json";

        public static async Task Main(string[] args)
        {
            if (File.Exists(jsonFile))
            {
                while (true)
                {
                    Console.WriteLine("\n'1' - Для запуска клиента" + "\n" + "'2' - Для запуска сервера");
                    Console.Write("\nВаш выбор: ");
                    string selectedInput = Console.ReadLine();

                    switch (selectedInput)
                    {
                        case "1":
                            Console.Write("\nСетевые соединения:\n");
                            List<ServerSettings> servers = GetSettings();
                            foreach (var server in servers)
                            {
                                Console.WriteLine(server.ToString());
                            }
                            TcpClientApp clientApp = new TcpClientApp();
                            await clientApp.StartClient();
                            break;
                        case "2":
                            MultiTcpServerApp multiServerApp = new MultiTcpServerApp();
                            await multiServerApp.StartServers();
                            break;
                        default:
                            Console.WriteLine("Можно выбрать только '1' или '2'");
                            break;
                    }
                    Console.ReadLine();
                }
            }
        }

        public static List<ServerSettings> GetSettings()
        {
            string json = File.ReadAllText(jsonFile);

            List<ServerSettings> serverSettingsList = JsonConvert.DeserializeObject<List<ServerSettings>>(json);
            return serverSettingsList;
        }
    }

    #region Множественный сервер

    public class MultiTcpServerApp
    {
        private List<TcpServerApp> servers;

        public MultiTcpServerApp()
        {
            // Создаём список servers, который будет хранить экземпляры TcpServerApp
            servers = new List<TcpServerApp>();

            List<ServerSettings> settingsList = Program.GetSettings();
            foreach (var settings in settingsList)
            {
                TcpServerApp server = new TcpServerApp(settings.Ip, settings.Port);
                servers.Add(server);
            }
        }

        public async Task StartServers()
        {
            try
            {
                List<Task> tasks = new List<Task>();
                foreach (var server in servers)
                {
                    tasks.Add(Task.Run(() => server.StartServer()));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    #endregion

    #region Сервер
    public class TcpServerApp
    {
        private string _ipAdres;
        private int _port;

        public TcpServerApp(string ipAdres, int port)
        {
            _ipAdres = ipAdres;
            _port = port;
        }

        public async Task StartServer()
        {
            try
            {
                TcpListener listener = new TcpListener(IPAddress.Parse(_ipAdres), _port);
                listener.Start();

                Console.WriteLine($"Сервер {_ipAdres}:{_port} запущен. Прослушивание входящих подключений...");

                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    ProcessClient(client);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private async Task ProcessClient(TcpClient client)
        {
            string clientAddress = client.Client.RemoteEndPoint.ToString();
            Console.WriteLine($"Клиент: {clientAddress} - подключён");

            try
            {
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.ASCII))
                using (StreamWriter writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true })
                {
                    // Получение данных с клиента
                    writer.WriteLine("Введите данные");
                    //string data = await reader.ReadLineAsync();
                    string data = await reader.ReadToEndAsync();

                    // Обработка и отправка данных
                    string result = await ProcessData(data);
                    await writer.WriteLineAsync(result);

                    Console.WriteLine($"Клиент: {clientAddress} - данные обработаны");

                    client.Close();
                    Console.WriteLine($"Клиент: {clientAddress} - отключён");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private async Task<string> ProcessData(string data)
        {
            string[] parts = data.Split(';');

            // Добавляем #27 к служебной информации
            string serviceInfo = "#010102#27";

            // Получаем <Data1> и <Data2>
            string data1 = parts[0].Substring(11);
            string data2 = parts[1].Substring(0, parts[1].Length - 5);

            // Сравнение данных
            List<string> data1List = new List<string>();
            List<string> data2List = new List<string>();

            foreach (var serverSettings in Program.GetSettings())
            {
                TcpClientApp clientApp = new TcpClientApp();
                string serverData = await clientApp.GetData(serverSettings.Ip, serverSettings.Port);
                string[] serverParts = serverData.Split(';');
                data1List.Add(serverParts[0].Substring(11));
                data2List.Add(serverParts[1].Substring(0, serverParts[1].Length - 4));
            }

            bool data1Match = data1List.All(x => x == data1);
            bool data2Match = data2List.All(x => x == data2);

            // Формирование результата
            string result = $"{serviceInfo}{(data1Match ? data1 : "NoRead")};{(data2Match ? data2 : "NoRead")}#91";

            return result;
        }
    }

    #endregion

    #region Клиент
    public class TcpClientApp
    {

        public async Task StartClient()
        {
            try
            {
                Console.Write("\nВведите IP-адрес сервера: ");
                string ipAddress = Console.ReadLine();

                Console.Write("Введите порт сервера: ");
                int port = int.Parse(Console.ReadLine());

                TcpClient client = new TcpClient();
                await client.ConnectAsync(ipAddress, port);
                Console.WriteLine("Клиент подключён к серверу.");

                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.ASCII))
                using (StreamWriter writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true })
                {
                    Console.Write("Введите данные для отправки на сервер: ");
                    //string data = Console.ReadLine();
                    string data = "#" + Console.ReadLine() + "#";

                    // Отправка данных на сервер
                    await writer.WriteLineAsync(data);

                    // Получение ответа от сервера
                    string response = await reader.ReadLineAsync();

                    Console.WriteLine($"Ответ от сервера: {response}");
                }

                client.Close();
                Console.WriteLine("Клиент отключён от сервера.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        public async Task<string> GetData(string ipAddress, int port)
        {
            try
            {
                TcpClient client = new TcpClient();
                await client.ConnectAsync(ipAddress, port);
                Console.WriteLine("Клиент подключён к серверу.");

                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.ASCII))
                using (StreamWriter writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true })
                {
                    // Отправка запроса на сервер для получения данных
                    await writer.WriteLineAsync("GetData");

                    // Получение ответа от сервера
                    //string response = await reader.ReadLineAsync();
                    string response = await reader.ReadToEndAsync();

                    Console.WriteLine($"Ответ от сервера: {response}");

                    return response;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                return null;
            }
        }
    }
    #endregion

    public class ServerSettings
    {
        public int Id { get; set; }
        public string Ip { get; set; }
        public int Port { get; set; }

        public override string ToString()
        {
            return $"'{Id}' - {Ip} {Port}";
        }
    }
}
