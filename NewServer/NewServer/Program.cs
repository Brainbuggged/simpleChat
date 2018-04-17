using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NewServer
{
    class Program
    {
        static string Host = Dns.GetHostName();
        static string localAddress = Dns.GetHostEntry(Host).AddressList[0].ToString();
        static List<EndPoint> clientEndPoints = new List<EndPoint>();
        static Socket tcpSocket = new Socket(AddressFamily.InterNetwork,
                                  SocketType.Stream,
                                  ProtocolType.Tcp);
        static List<Socket> clientSockets = new List<Socket>();

        private static void SendServerAddress()
        {
            byte[] bytes = new byte[500];
            var  mcastSocket = new Socket(AddressFamily.InterNetwork,
                                                   SocketType.Dgram,
                                                   ProtocolType.Udp);
            IPAddress mcastAddress = IPAddress.Parse("224.12.12.12");
            var mcastPort = 4000;
            mcastSocket.Bind(new IPEndPoint(IPAddress.Any, mcastPort));
            var mcastOption = new MulticastOption(mcastAddress, IPAddress.Any);
            mcastSocket.SetSocketOption(SocketOptionLevel.IP,
                                                   SocketOptionName.AddMembership,
                                                    mcastOption);
            var clientEndPoint = new IPEndPoint(IPAddress.Any, mcastPort) as EndPoint;
            IPAddress serverTCPAddress = IPAddress.Parse(localAddress);
            var serverTCPSocketEndPoint= new IPEndPoint(serverTCPAddress,mcastPort);

            Task.Run(() =>
                {
                    while (true)
                    {
                       
                       bytes = bytes.Take(mcastSocket.ReceiveFrom(bytes, ref clientEndPoint)).ToArray();
                        var clientAddress = Encoding.UTF8.GetString(bytes);
                       bytes = Encoding.UTF8.GetBytes(serverTCPSocketEndPoint.ToString());
                       clientEndPoints.Add(clientEndPoint);
                       mcastSocket.SendTo(bytes,clientEndPoint);      
                       Console.WriteLine(clientAddress +" подключился к чатику ");
                       
                    }
                });
        }

        private static void ReceiveBroadcastMessages()
        {
            byte[] bytes = new byte[500];
            var mcastPort = 4000;
            var dictionary = new Dictionary<string, string>();
            IPAddress serverTCPAddres = IPAddress.Parse(localAddress);
            var serverTCPSocketEndPoint = new IPEndPoint(serverTCPAddres, mcastPort);
            tcpSocket.Bind(serverTCPSocketEndPoint);
            tcpSocket.Listen(1);
            Task.Run(() =>
                {
                    while (true)
                    {
                        var mySocket = tcpSocket.Accept();
                        clientSockets.Add(mySocket);
                        Task.Run(() =>
                            {
                                while (true)
                                { 
                                foreach(Socket client in clientSockets)
                                
                                    bytes = bytes.Take(client.Receive(bytes)).ToArray();

                                if (bytes.Length == 0)
                                    continue;

                                string message = Encoding.UTF8.GetString(bytes);
                                XDocument xdoc = new XDocument();
                                xdoc = XDocument.Parse(message);

                                    foreach (XElement elem in xdoc.Element("msg").Elements("data"))
                                    {
                                        dictionary.Add(elem.Attribute("key").Value, elem.Attribute("value").Value);
                                    }


                                    if (dictionary["Type"] == "Send")
                                    {
                                        message = " somebody wrote " + dictionary["Text"];
                                        mySocket.Send(Encoding.UTF8.GetBytes(message));
                                        foreach (var address in clientEndPoints)
                                            dictionary.Clear();
                                        bytes = new byte[500];


                                    }
                                }
                            });
                    }
                });
        }

        static void Main(string[] args)
        {
            SendServerAddress();
            ReceiveBroadcastMessages();
            Console.Read();
        }
    }
}
