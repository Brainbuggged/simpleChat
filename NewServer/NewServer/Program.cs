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
         IPAddress localAddress = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0];

        Socket tcpSocket = new Socket(AddressFamily.InterNetwork,
            SocketType.Stream,
            ProtocolType.Tcp);

        Dictionary<Socket, string> DictionaryOfClientsAndGuids = new Dictionary<Socket, string>();

        static void Main(string[] args)
        {
            var program = new Program();
            program.SendServerAddress();
            program.ReceiveBroadcastMessages();
            Console.Read();
        }
        private void SendServerAddress()
        {
            byte[] bytes = new byte[500];
            var mcastSocket = new Socket(AddressFamily.InterNetwork,
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
            IPAddress serverTCPAddress = localAddress;


            Task.Run(() =>
            {   
                while (true)
                {

                    bytes = bytes.Take(mcastSocket.ReceiveFrom(bytes, ref clientEndPoint)).ToArray();
                    var clientAddress = Encoding.UTF8.GetString(bytes);

                    var serverTCPSocketEndPoint = new IPEndPoint(serverTCPAddress, mcastPort);
                    var newClientSendMessageDocument = serverTCPSocketEndPoint.ToString();
                  
                    bytes = Encoding.UTF8.GetBytes(newClientSendMessageDocument.ToString());

                    mcastSocket.SendTo(bytes, clientEndPoint);


                    Console.WriteLine(clientAddress + " подключился к чатику ");
                }
            });
        }

        private void ReceiveBroadcastMessages()
        {
         
            var mcastPort = 4000;
           
            IPAddress serverTCPAddress = localAddress;
            var serverTCPSocketEndPoint = new IPEndPoint(serverTCPAddress, mcastPort);
            tcpSocket.Bind(serverTCPSocketEndPoint);
            tcpSocket.Listen(1);
            Task.Run(() =>
            {
                int userCount = 0;
                while (true)
                {
                    var acceptedClient = tcpSocket.Accept();
                    if (!DictionaryOfClientsAndGuids.Keys.Contains(acceptedClient))
                    {
                        var guid = String.Format("Пользователь " + userCount);
                        DictionaryOfClientsAndGuids.Add(acceptedClient, guid);
                        userCount++;
                        SendMessageAboutConnection(acceptedClient);
                        Thread.Sleep(1000);
                        SendListOfClients();
                    }

                    Task.Run(() =>
                    {
                        while (true)
                        {
                            var dictionary = new Dictionary<string, string>();
                            var bytes = new byte[500];
                                    bytes = bytes.Take(acceptedClient.Receive(bytes)).ToArray();     
                                    var message = Encoding.UTF8.GetString(bytes);
                                    
                                    var xdoc = XDocument.Parse(message);

                                    foreach (var elem in xdoc.Element("msg").Elements("data"))
                                    {
                                        dictionary.Add(elem.Attribute("key").Value, elem.Attribute("value").Value);

                                    }
                                    var clientName = DictionaryOfClientsAndGuids[acceptedClient];
                            if (dictionary["Type"] == "Send")
                            {

                                var sendMessageDocument = new XDocument(
                                    new XElement("msg",
                                        new XElement("data", new XAttribute("key", "Type"),
                                            new XAttribute("value", "Send")),
                                        new XElement("data", new XAttribute("key", "Author"),
                                            new XAttribute("value", clientName)),
                                        new XElement("data", new XAttribute("key", "Text"),
                                            new XAttribute("value", dictionary["Text"]))
                                    ));

                                foreach (var socket in DictionaryOfClientsAndGuids.Keys)
                                    socket.Send(Encoding.UTF8.GetBytes(sendMessageDocument.ToString()));

                                dictionary.Clear();
                                bytes = new byte[500];
                            }
                            else if (dictionary["Type"] == "PrivateSend")
                            {
                                var sendMessageDocument = new XDocument(
                                    new XElement("msg",
                                        new XElement("data", new XAttribute("key", "Type"),
                                            new XAttribute("value", "PrivateSend")),
                                        new XElement("data", new XAttribute("key", "Author"),
                                            new XAttribute("value", clientName)),
                                        new XElement("data", new XAttribute("key", "Text"),
                                            new XAttribute("value", dictionary["Text"]))
                                    ));
                                var charSeparators = new char[] {','};
                                var listOfUsers = dictionary["Persons"]
                                    .Split(charSeparators, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var user in listOfUsers)
                                foreach (var socket in DictionaryOfClientsAndGuids.Keys)
                                    if (DictionaryOfClientsAndGuids[socket] == user)
                                        socket.Send(Encoding.UTF8.GetBytes(sendMessageDocument.ToString()));

                                dictionary.Clear();
                                bytes = new byte[500];
                            }
                        }
                    });
                }
            });
        }


        private void SendMessageAboutConnection(Socket acceptedClient)
        {         
            var newConnectionMessage = new XDocument(
                new XElement("msg",
                    new XElement("data", new XAttribute("key", "Type"),
                        new XAttribute("value", "Name")),
                    new XElement("data", new XAttribute("key", "Name"),
                        new XAttribute("value", DictionaryOfClientsAndGuids[acceptedClient]))));

            foreach (var socket in DictionaryOfClientsAndGuids.Keys)
                socket.Send(Encoding.UTF8.GetBytes(newConnectionMessage.ToString()));
        }

        private void SendListOfClients()
        {
            var list = "";
            foreach (var str in DictionaryOfClientsAndGuids.Values)
                list += str + ",";

            var listOfClientsDocument = new XDocument(
                new XElement("msg",
                    new XElement("data", new XAttribute("key", "Type"),
                        new XAttribute("value", "List")),
                    new XElement("data", new XAttribute("key", "List"),
                        new XAttribute("value", list))));

            foreach (var socket in DictionaryOfClientsAndGuids.Keys)
                socket.Send(Encoding.UTF8.GetBytes(listOfClientsDocument.ToString()));
        }
    }
}
