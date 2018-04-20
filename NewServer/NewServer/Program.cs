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
            byte[] receivedBytes = new byte[100];
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
            var separator = ':';
            Task.Run(() =>
            {
                while (true)
                {
                    var receivedCount = (mcastSocket.ReceiveFrom(receivedBytes, ref clientEndPoint));
                    var takenBytes  = GetTrimmedBytes(receivedBytes,receivedCount);
                    var ClientEndPoint = Encoding.UTF8.GetString(takenBytes);
                    var serverTCPSocketEndPoint = new IPEndPoint(serverTCPAddress, mcastPort);
                    var serverAddressMessage = serverTCPSocketEndPoint.ToString();

                   var  bytesToSend = Encoding.UTF8.GetBytes(serverAddressMessage.ToString());
                    //separation
                    var strings = ClientEndPoint.Split(separator);
                    var remoteAddress = IPAddress.Parse(strings[0]);
                    var remotePort = int.Parse(strings[1]);

                    mcastSocket.SendTo(bytesToSend, new IPEndPoint(remoteAddress, remotePort));
                    Console.WriteLine(remoteAddress +":"+ remotePort+ " подключился к чатику ");
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
                            var receivedBytes = new byte[2048];
                            var receivedCount = acceptedClient.Receive(receivedBytes);
                            var takenBytes = GetTrimmedBytes(receivedBytes, receivedCount);
                            var xdoc = GetDocumentFromReceivedBytes(takenBytes);

                            foreach (var elem in xdoc.Element("msg").Elements("data"))
                            {
                                dictionary.Add(elem.Attribute("key").Value, elem.Attribute("value").Value);
                            }

                            var clientName = DictionaryOfClientsAndGuids[acceptedClient];
                            if (dictionary["Type"] == "Send")
                            {
                                var sendMessageDocument = GetDocumentFromStringAndClient(dictionary["Text"], clientName,"Send");
                                foreach (var socket in DictionaryOfClientsAndGuids.Keys)
                                    socket.Send(GetBytesToSendFromDocument(sendMessageDocument));
                                dictionary.Clear();
                            }
                            else if (dictionary["Type"] == "PrivateSend")
                            {
                                var sendMessageDocument =
                                    GetDocumentFromStringAndClient(dictionary["Text"], clientName,"PrivateSend");
                                var charSeparators = new char[] {','};
                                var listOfUsers = dictionary["Persons"]
                                    .Split(charSeparators, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var user in listOfUsers)
                                foreach (var socket in DictionaryOfClientsAndGuids.Keys)
                                    if (DictionaryOfClientsAndGuids[socket] == user)
                                        socket.Send(GetBytesToSendFromDocument(sendMessageDocument));
                                dictionary.Clear();
                            }
                        }
                    });
                }
            });
        }
        private XDocument GetDocumentFromStringAndClient(string message,string clientName,string type)
        {
            var sendMessageDocument = new XDocument(
                new XElement("msg",
                    new XElement("data", new XAttribute("key", "Type"),
                        new XAttribute("value", type)),
                    new XElement("data", new XAttribute("key", "Author"),
                        new XAttribute("value", clientName)),
                    new XElement("data", new XAttribute("key", "Text"),
                        new XAttribute("value", message))
                ));
            return sendMessageDocument;
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
                socket.Send(GetBytesToSendFromDocument(newConnectionMessage));
        }

        private void SendListOfClients()
        {
            var clientGuids = new StringBuilder();
            foreach (var str in DictionaryOfClientsAndGuids.Values)
                clientGuids.Append(str + ",");

            var listOfClientsDocument = new XDocument(
                new XElement("msg",
                    new XElement("data", new XAttribute("key", "Type"),
                        new XAttribute("value", "List")),
                    new XElement("data", new XAttribute("key", "List"),
                        new XAttribute("value", clientGuids))));

            foreach (var socket in DictionaryOfClientsAndGuids.Keys)
                socket.Send(GetBytesToSendFromDocument(listOfClientsDocument));
        }

        private byte[] GetBytesToSendFromDocument(XDocument document)
        {
            return Encoding.UTF8.GetBytes(document.ToString());
        }
        private XDocument GetDocumentFromReceivedBytes(byte[] bytes)
        {
            return XDocument.Parse(Encoding.UTF8.GetString(bytes));
        }
        private byte[] GetTrimmedBytes(byte[] receivedBytes, int receivedCount)
        {

            return receivedBytes.Take(receivedCount).ToArray();

        }
    }
}
