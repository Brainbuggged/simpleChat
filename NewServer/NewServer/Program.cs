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
         IPAddress localAddress = Dns.GetHostByName(Dns.GetHostName()).AddressList[1];

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
        //sending address of the local machine for all the clients
        private void SendServerAddress()
        {
            byte[] receivedBytes = new byte[100];

            //things that were described at https://msdn.microsoft.com/ru-ru/library/system.net.sockets.multicastoption(v=vs.110).aspx
            var mcastSocket = new Socket(AddressFamily.InterNetwork,
                SocketType.Dgram,
                ProtocolType.Udp);
            IPAddress mcastAddress = IPAddress.Parse("224.12.12.12");
            var mcastPort = 4000;
   
            //binding the socket to the local address
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

                    //an address we want to send
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

            // binding and then listening to server end point
            tcpSocket.Bind(serverTCPSocketEndPoint);
            tcpSocket.Listen(1);
            Task.Run(() =>
            {
                int userCount = 0;
                while (true)
                {
                   
                    var acceptedClient = tcpSocket.Accept();
                    // after we accepted the connection 
                    // the new thread starts
                    // where we receive data from every client

                    // here we are checking if there are any equal sockets
                    if (!DictionaryOfClientsAndGuids.Keys.Contains(acceptedClient))
                    {
                        var guid = String.Format("Пользователь " + userCount);
                        DictionaryOfClientsAndGuids.Add(acceptedClient, guid);
                        userCount++;
                        SendMessageAboutConnection(acceptedClient);

                        // without sleeping there would be an exception 
                        // dunno how to fix it yet
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

                            // filling the dictionary with data from takenbytes
                            foreach (var elem in xdoc.Element("msg").Elements("data"))
                            {
                                dictionary.Add(elem.Attribute("key").Value, elem.Attribute("value").Value);
                            }
                            // here we get the 'value' of our socket
                            var clientName = DictionaryOfClientsAndGuids[acceptedClient];

                            // determing the type of the message in if-else-if
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
                               
                                // stupid foreaches for sending message to exact clients
                                // need to be fixed actually
                                var specialMessageForSender = CreateSpecialDocument(dictionary["Text"], clientName,
                                    dictionary["Persons"]);
                                acceptedClient.Send(GetBytesToSendFromDocument(specialMessageForSender));
                                Console.WriteLine("sadsad");

                           
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
        // simple parser here
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


        private XDocument CreateSpecialDocument(string message, string clientName, string listOfPersons)
        {
            var sendMessageDocument = new XDocument(
                new XElement("msg",
                    new XElement("data", new XAttribute("key", "Type"),
                        new XAttribute("value", "PrivateSend")),
                    new XElement("data", new XAttribute("key", "Author"),
                        new XAttribute("value", clientName)),
                    new XElement("data", new XAttribute("key", "Persons"),
                        new XAttribute("value", listOfPersons)),
                    new XElement("data", new XAttribute("key", "Text"),
                        new XAttribute("value", message))
                ));
            return sendMessageDocument;
        }

        // send it to every client in dictionary
        private void SendMessageAboutConnection(Socket acceptedClient)
        {         
            var newConnectionMessage = new XDocument(
                new XElement("msg",
                    new XElement("data", new XAttribute("key", "Type"),
                        new XAttribute("value", "Name")),
                    new XElement("data", new XAttribute("key", "Name"),
                        new XAttribute("value", DictionaryOfClientsAndGuids[acceptedClient]))));

            
                acceptedClient.Send(GetBytesToSendFromDocument(newConnectionMessage));
        }
        // the same for this method
        // maybe should've done one method for them?
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
        // parsers  for messsages here
        private byte[] GetBytesToSendFromDocument(XDocument document)
        {
            return Encoding.UTF8.GetBytes(document.ToString());
        }
        private XDocument GetDocumentFromReceivedBytes(byte[] bytes)
        {
            return XDocument.Parse(Encoding.UTF8.GetString(bytes));
        }
        // simple trimming to make code more readable
        private byte[] GetTrimmedBytes(byte[] receivedBytes, int receivedCount)
        {
            return receivedBytes.Take(receivedCount).ToArray();
        }
    }
}
