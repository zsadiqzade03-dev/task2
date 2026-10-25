
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Server
{
    internal class Program
    {
        private const int Port = 28001;

        private static readonly object LockObject = new object();

        private static readonly string FilePath =
            Path.Combine(AppContext.BaseDirectory, "users.json");

        private static readonly List<User> Users = new List<User>();

        private static readonly Dictionary<string, ClientSession> ActiveUsers =
            new Dictionary<string, ClientSession>(
                StringComparer.OrdinalIgnoreCase);

        static async Task Main()
        {
            LoadUsers();

            TcpListener listener = new TcpListener(IPAddress.Any, Port);

            try
            {
                listener.Start();

                Console.WriteLine("SERVER IS RUNNING");
                Console.WriteLine("Port: " + Port);
                Console.WriteLine("Waiting for clients...\n");

                ShowUsers();

                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();

                    _ = Task.Run(() => HandleClient(client));
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine("Server socket xetasi: " + ex.Message);
            }
            finally
            {
                listener.Stop();
            }
        }

        private static void LoadUsers()
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);

                List<User>? loaded =
                    JsonSerializer.Deserialize<List<User>>(json);

                if (loaded != null)
                    Users.AddRange(loaded);
            }
            else
            {
                Users.Add(new User
                {
                    Username = "Mahmud",
                    Password = "12345"
                });

                Users.Add(new User
                {
                    Username = "Leyla",
                    Password = "abc123"
                });

                Users.Add(new User
                {
                    Username = "Ali",
                    Password = "ali123"
                });

                Users.Add(new User
                {
                    Username = "Aysel",
                    Password = "aysel123"
                });
            }

            foreach (User user in Users)
                user.IsOnline = false;

            SaveUsers();
        }

        private static void SaveUsers()
        {
            string json = JsonSerializer.Serialize(
                Users,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            File.WriteAllText(FilePath, json);
        }

        private static void HandleClient(TcpClient client)
        {
            string username = "";

            try
            {
                string ip =
                    ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();

                NetworkStream stream = client.GetStream();

                using BinaryReader reader =
                    new BinaryReader(stream, Encoding.UTF8, true);

                using BinaryWriter writer =
                    new BinaryWriter(stream, Encoding.UTF8, true);

                string enteredUsername = reader.ReadString();
                string enteredPassword = reader.ReadString();

                string response;

                lock (LockObject)
                {
                    User? user = Users.FirstOrDefault(u =>
                        u.Username.Equals(
                            enteredUsername,
                            StringComparison.OrdinalIgnoreCase)
                        && u.Password == enteredPassword);

                    if (user == null)
                    {
                        response = "REJECTED|Username ve ya password yanlisdir.";
                    }
                    else if (ActiveUsers.ContainsKey(user.Username))
                    {
                        response = "REJECTED|Bu istifadeci artiq online-dir.";
                    }
                    else
                    {
                        username = user.Username;
                        user.LastIP = ip;
                        user.IsOnline = true;

                        ActiveUsers.Add(
                            username,
                            new ClientSession(client, writer));

                        SaveUsers();

                        response = "SUCCESS";
                    }
                }

                
                writer.Write(response);
                writer.Flush();

                if (response != "SUCCESS")
                {
                    Console.WriteLine(
                        $"Giris redd edildi: {enteredUsername} | IP: {ip}");

                    return;
                }

                Console.WriteLine(
                    $"{username} daxil oldu | IP: {ip}");

                BroadcastUserList();

               
                while (true)
                {
                    string message = reader.ReadString();

                    if (message.Equals(
                        "EXIT",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                }
            }
            catch (EndOfStreamException)
            {
                Console.WriteLine("Client baglantini bagladi.");
            }
            catch (IOException ex)
            {
                Console.WriteLine("Baglanti kesildi: " + ex.Message);
            }
            catch (SocketException ex)
            {
                Console.WriteLine("Socket xetasi: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Xeta: " + ex.Message);
            }
            finally
            {
                if (username != "")
                {
                    lock (LockObject)
                    {
                        ActiveUsers.Remove(username);

                        User? user = Users.FirstOrDefault(u =>
                            u.Username.Equals(
                                username,
                                StringComparison.OrdinalIgnoreCase));

                        if (user != null)
                        {
                            user.IsOnline = false;
                            .
                        }

                        SaveUsers();
                    }

                    Console.WriteLine(username + " offline oldu.");

                    BroadcastUserList();
                }

                client.Close();
            }
        }

        private static void BroadcastUserList()
        {
            string message;

            lock (LockObject)
            {
                message = "USER_LIST\n" +
                    string.Join("\n", Users.Select(u =>
                        $"{u.Username} | IP: {u.LastIP ?? "Yoxdur"} | " +
                        (u.IsOnline ? "Online" : "Offline")));
            }

            List<ClientSession> sessions;

            lock (LockObject)
            {
                sessions = ActiveUsers.Values.ToList();
            }

            foreach (ClientSession session in sessions)
            {
                try
                {
                    lock (session.WriteLock)
                    {
                        session.Writer.Write(message);
                        session.Writer.Flush();
                    }
                }
                catch (IOException)
                {
                    // Baglanti baglanibsa diger clientlere davam et
                }
                catch (ObjectDisposedException)
                {
                    // Client artiq baglanib
                }
            }

            Console.WriteLine("\n--- ISTIFADECI SIYAHISI ---");
            Console.WriteLine(message.Substring("USER_LIST\n".Length));
            Console.WriteLine("---------------------------\n");
        }

        private static void ShowUsers()
        {
            lock (LockObject)
            {
                foreach (User user in Users)
                {
                    Console.WriteLine(
                        $"{user.Username} | IP: {user.LastIP ?? "Yoxdur"} | " +
                        (user.IsOnline ? "Online" : "Offline"));
                }
            }

            Console.WriteLine();
        }
    }

    internal class User
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string? LastIP { get; set; }
        public bool IsOnline { get; set; }
    }

    internal class ClientSession
    {
        public TcpClient Client { get; }
        public BinaryWriter Writer { get; }
        public object WriteLock { get; } = new object();

        public ClientSession(TcpClient client, BinaryWriter writer)
        {
            Client = client;
            Writer = writer;
        }
    }
}
