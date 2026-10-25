
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    internal class Program
    {
      
        private const string ServerIP = "192.168.0.108";
        private const int Port = 28001;

        static async Task Main()
        {
            using TcpClient client = new TcpClient();

            try
            {
                Console.WriteLine("Servere qosuluruq...");

                await client.ConnectAsync(ServerIP, Port);

                Console.WriteLine("Servere qosuldu...\n");

                NetworkStream stream = client.GetStream();

                using BinaryReader reader =
                    new BinaryReader(stream, Encoding.UTF8, true);

                using BinaryWriter writer =
                    new BinaryWriter(stream, Encoding.UTF8, true);

                Console.Write("Username: ");
                string username = Console.ReadLine() ?? "";

                Console.Write("Password: ");
                string password = Console.ReadLine() ?? "";

                
                writer.Write(username);
                writer.Write(password);
                writer.Flush();

                // serverin cavabini gozleyirik
                string response = reader.ReadString();

                if (response.StartsWith("REJECTED|"))
                {
                    Console.WriteLine("\nGiris redd edildi:");
                    Console.WriteLine(
                        response.Substring("REJECTED|".Length));

                    return;
                }

                if (response != "SUCCESS")
                {
                    Console.WriteLine("Serverden duzgun cavab gelmedi.");
                    return;
                }

                Console.WriteLine("\nGiris ugurludur...");
                Console.WriteLine("Cixmaq ucun EXIT yaz...\n");

                // serverden gelen siyahini oxuyan ayrica task
                Task receiveTask = Task.Run(() =>
                {
                    try
                    {
                        while (true)
                        {
                            string message = reader.ReadString();

                            if (message.StartsWith("USER_LIST\n"))
                            {
                                Console.WriteLine(
                                    "\n=== ISTIFADECI SIYAHISI ===");

                                Console.WriteLine(
                                    message.Substring("USER_LIST\n".Length));

                                Console.WriteLine(
                                    "===========================\n");
                            }
                            else
                            {
                                Console.WriteLine(message);
                            }
                        }
                    }
                    catch (EndOfStreamException)
                    {
                        // server baglantini baglayib
                    }
                    catch (IOException)
                    {
                        // elaqe kesilib
                    }
                    catch (ObjectDisposedException)
                    {
                        // client baglanib
                    }
                });

                while (true)
                {
                    string command = Console.ReadLine() ?? "";

                    if (command.Equals(
                        "EXIT",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        writer.Write("EXIT");
                        writer.Flush();
                        break;
                    }

                    Console.WriteLine("Cixmaq ucun EXIT yaz.");
                }

                client.Close();

                await receiveTask;

                Console.WriteLine("Client baglandi.");
            }
            catch (SocketException ex)
            {
                Console.WriteLine("\nServere qosulmaq olmadi.");
                Console.WriteLine(ex.Message);

                Console.WriteLine(
                    "Serverin aciq oldugunu, IP ve portu yoxla.");
            }
            catch (EndOfStreamException)
            {
                Console.WriteLine(
                    "Server giris cavabi gondermeden baglandi.");
            }
            catch (IOException ex)
            {
                Console.WriteLine("Elaqe xetasi: " + ex.Message);
            }
        }
    }
}
