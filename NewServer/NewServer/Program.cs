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
        private readonly IPAddress localAddress = Dns.GetHostEntry(Dns.GetHostName()).AddressList
            .Last(x => x.AddressFamily == AddressFamily.InterNetwork);

        private static readonly int localPort = 4000;
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
            const int mcastPort = 4000;

            // things that were described at https://msdn.microsoft.com/ru-ru/library/system.net.sockets.multicastoption(v=vs.110).aspx
            var mcastSocket = new Socket(AddressFamily.InterNetwork,
                SocketType.Dgram,
                ProtocolType.Udp);
            IPAddress mcastAddress = IPAddress.Parse("224.12.12.12");
            
   
            // binding the socket to the local endpoint
            mcastSocket.Bind(new IPEndPoint(IPAddress.Any, mcastPort));

            var mcastOption = new MulticastOption(mcastAddress, IPAddress.Any);
            mcastSocket.SetSocketOption(SocketOptionLevel.IP,
                SocketOptionName.AddMembership,
                mcastOption);
            var clientEndPoint = new IPEndPoint(IPAddress.Any, mcastPort) as EndPoint;
            IPAddress serverTCPAddress = localAddress;
            const char separator = ':';
            Task.Run(() =>
            {
                while (true)
                {
                    var receivedCount = (mcastSocket.ReceiveFrom(receivedBytes, ref clientEndPoint));
                    var takenBytes  = GetTrimmedBytes(receivedBytes,receivedCount);
                    var ClientEndPoint = Encoding.UTF8.GetString(takenBytes);
                    var serverTCPSocketEndPoint = new IPEndPoint(serverTCPAddress, mcastPort);

                    // an address we want to send
                    var serverAddressMessage = serverTCPSocketEndPoint.ToString();

                   var  bytesToSend = Encoding.UTF8.GetBytes(serverAddressMessage.ToString());

                    // separation
                    // simply divide an array 
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
            
           
            var serverTCPSocketEndPoint = new IPEndPoint(localAddress, localPort);

            // binding and then listening to server end point
            tcpSocket.Bind(serverTCPSocketEndPoint);
            tcpSocket.Listen(1);
            Task.Run(() =>
            {
                var userCount = 0;
                while (true)
                {
                   
                    var acceptedClient = tcpSocket.Accept();
                    // after we accepted the connection 
                    // the new thread starts
                    // where we receive data from every client

                    // here we are checking if there are any equal sockets
                    var guid = String.Format("Пользователь " + userCount);
                    if (!DictionaryOfClientsAndGuids.Keys.Contains(acceptedClient))
                    {
                        DictionaryOfClientsAndGuids.Add(acceptedClient, guid);
                        userCount++;
                        SendMessageAboutConnection(acceptedClient);

                        // without sleeping there would be an exception 
                        // just because we do not have much time for each operation
                        Thread.Sleep(1000);
                        SendListOfClients();
                    }
                    Task.Run(() =>
                    {
                        while (true)
                        {
                            var dictionary = new Dictionary<string, string>();
                            var receivedBytes = new byte[2048];
                            try
                            {
                                var receivedCount = acceptedClient.Receive(receivedBytes);
                                var takenBytes = GetTrimmedBytes(receivedBytes, receivedCount);
                                var xdoc = GetDocumentFromReceivedBytes(takenBytes);

                                // filling the dictionary with data from takenbytes
                                foreach (var elem in xdoc.Element("msg").Elements("f"))
                                {
                                    dictionary.Add(elem.Attribute("n").Value, elem.Attribute("v").Value);
                                }

                                // here we get the 'value' of our socket
                                var clientName = DictionaryOfClientsAndGuids[acceptedClient];

                                // determing the type of the message in if-else-if
                                switch (dictionary["Type"])
                                {
                                    case "Send":
                                    {
                                        var sendMessageDocument =
                                            GetDocumentFromStringAndClient(dictionary["Text"], clientName, "Send");
                                        foreach (var socket in DictionaryOfClientsAndGuids.Keys)
                                            socket.Send(GetBytesToSendFromDocument(sendMessageDocument));
                                        dictionary.Clear();
                                        break;
                                    }
                                    case "PrivateSend":
                                    {
                                        var sendMessageDocument =
                                            GetDocumentFromStringAndClient(dictionary["Text"], clientName,
                                                "PrivateSend");
                                        var charSeparators = new char[] {','};
                                        var listOfUsers = dictionary["Persons"]
                                            .Split(charSeparators, StringSplitOptions.RemoveEmptyEntries);

                                        // stupid foreaches for sending message to exact clients
                                        // need to be fixed actually
                                        var specialMessageForSender = CreateSpecialDocument(dictionary["Text"],
                                            clientName,
                                            dictionary["Persons"]);
                                        acceptedClient.Send(GetBytesToSendFromDocument(specialMessageForSender));

                                        // stupid way of finding every client in the list
                                        foreach (var user in listOfUsers)
                                        foreach (var socket in DictionaryOfClientsAndGuids.Keys)
                                            if (DictionaryOfClientsAndGuids[socket] == user)
                                                socket.Send(GetBytesToSendFromDocument(sendMessageDocument));
                                        dictionary.Clear();
                                        break;
                                    }
                                }
                            }
                            catch (SocketException ex)
                            {
                                DictionaryOfClientsAndGuids.Remove(acceptedClient);
                                
                                SendListOfClients();
                                break;
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
                    new XElement("f", new XAttribute("n", "Type"),
                        new XAttribute("v", type)),
                    new XElement("f", new XAttribute("n", "Author"),
                        new XAttribute("v", clientName)),
                    new XElement("f", new XAttribute("n", "Text"),
                        new XAttribute("v", message))
                ));
            return sendMessageDocument;
        }


        private XDocument CreateSpecialDocument(string message, string clientName, string listOfPersons)
        {
            var sendMessageDocument = new XDocument(
                new XElement("msg",
                    new XElement("f", new XAttribute("n", "Type"),
                        new XAttribute("v", "PrivateSend")),
                    new XElement("f", new XAttribute("n", "Author"),
                        new XAttribute("v", clientName)),
                    new XElement("f", new XAttribute("n", "Persons"),
                        new XAttribute("v", listOfPersons)),
                    new XElement("f", new XAttribute("n", "Text"),
                        new XAttribute("v", message))
                ));
            return sendMessageDocument;
        }

        // send it to every client in dictionary
        private void SendMessageAboutConnection(Socket acceptedClient)
        {         
            var newConnectionMessage = new XDocument(
                new XElement("msg",
                    new XElement("f", new XAttribute("n", "Type"),
                        new XAttribute("v", "Name")),
                    new XElement("f", new XAttribute("n", "Name"),
                        new XAttribute("v", DictionaryOfClientsAndGuids[acceptedClient]))));

            
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
                    new XElement("f", new XAttribute("n", "Type"),
                        new XAttribute("v", "List")),
                    new XElement("f", new XAttribute("n", "List"),
                        new XAttribute("v", clientGuids))));

            foreach (var socket in DictionaryOfClientsAndGuids.Keys)
                if (socket.Connected)
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
            if (receivedBytes == null) throw new ArgumentNullException(nameof(receivedBytes));
            return receivedBytes.Take(receivedCount).ToArray();
        }
    }
}
