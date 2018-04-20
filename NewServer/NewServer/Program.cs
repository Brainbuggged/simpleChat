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
         string localAddress = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString();
         
         Socket tcpSocket = new Socket(AddressFamily.InterNetwork,
                                  SocketType.Stream,
                                  ProtocolType.Tcp);

         Dictionary<Socket, string> dictionaryOfClientsAndGuids = new Dictionary<Socket, string>();

        private  void SendServerAddress()
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
        

            Task.Run(() =>
            {//КОСТЫЛЬ ИСПРАВИТЬ
                int countOfusers = 0;
                    while (true)
                    {
                       
                       bytes = bytes.Take(mcastSocket.ReceiveFrom(bytes, ref clientEndPoint)).ToArray();
                        var clientAddress = Encoding.UTF8.GetString(bytes);
                        
                        var serverTCPSocketEndPoint = new IPEndPoint(serverTCPAddress, mcastPort);
                        var newClientSendMessageDocument = serverTCPSocketEndPoint.ToString();
                    countOfusers++;
                        bytes = Encoding.UTF8.GetBytes(newClientSendMessageDocument.ToString());
                        
                       mcastSocket.SendTo(bytes,clientEndPoint);
                     

                        Console.WriteLine(clientAddress +" подключился к чатику ");
                    }
                });
        }

        private  void ReceiveBroadcastMessages()
        {
            byte[] bytes = new byte[500];
            var mcastPort = 4000;
            var dictionary = new Dictionary<string, string>();
            IPAddress serverTCPAddress = IPAddress.Parse(localAddress);
            var serverTCPSocketEndPoint = new IPEndPoint(serverTCPAddress, mcastPort);
            tcpSocket.Bind(serverTCPSocketEndPoint);
            tcpSocket.Listen(1);
            Task.Run(() =>
            {
                int countOfUsers = 0;
                while (true)
                    {
                        var mySocket = tcpSocket.Accept();


                        if (!dictionaryOfClientsAndGuids.Keys.Contains(mySocket))
                        {

                            var guid = String.Format("Пользователь " + countOfUsers);
                            dictionaryOfClientsAndGuids.Add(mySocket, guid);
                            countOfUsers++;
                            sendMessageAboutConnection(mySocket);
                            Thread.Sleep(1000);
                            sendListOfClients();
                        }


                        Task.Run(() =>
                        {
                            Socket currentUser=null;
                                while (true)
                                {
                                //TODO Need to think about async way of receiving messages from every client
                                    foreach (var client in dictionaryOfClientsAndGuids.Keys)
                                    {
                                       
                                            bytes = bytes.Take(client.Receive(bytes)).ToArray();
                                            currentUser = client;
                                        


                                    }


                                    if (bytes.Length == 0)
                                    continue;

                                var message = Encoding.UTF8.GetString(bytes);
                                var xdoc = new XDocument();
                                xdoc = XDocument.Parse(message);

                                    foreach (var elem in xdoc.Element("msg").Elements("data"))
                                    {
                                        dictionary.Add(elem.Attribute("key").Value, elem.Attribute("value").Value);

                                    }

                                    var clientName = dictionaryOfClientsAndGuids[currentUser];
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

                                        foreach (var socket in dictionaryOfClientsAndGuids.Keys)
                                            socket.Send(Encoding.UTF8.GetBytes(sendMessageDocument.ToString()));

                                        dictionary.Clear();
                                        bytes = new byte[500];
                                    }
                                }
                            });
                    }
                });
        }

        private void sendMessageAboutConnection(Socket mySocket)
        {
            

            var newConnectionMessage = new XDocument(
                new XElement("msg",
                    new XElement("data", new XAttribute("key", "Type"),
                        new XAttribute("value", "Name")),
                    new XElement("data", new XAttribute("key", "Name"),
                        new XAttribute("value", dictionaryOfClientsAndGuids[mySocket]))));

            foreach (var socket in dictionaryOfClientsAndGuids.Keys)
                socket.Send(Encoding.UTF8.GetBytes(newConnectionMessage.ToString()));

        }

        private void sendListOfClients()
        {
            var list = "";
            foreach (var str in dictionaryOfClientsAndGuids.Values)
                list += str +",";

            var listOfClientsDocument = new XDocument(
                new XElement("msg",
                    new XElement("data", new XAttribute("key", "Type"),
                        new XAttribute("value", "List")),
                    new XElement("data", new XAttribute("key", "List"),
                        new XAttribute("value", list))));
                    

                    foreach (var socket in dictionaryOfClientsAndGuids.Keys)
                socket.Send(Encoding.UTF8.GetBytes(listOfClientsDocument.ToString()));



        }

        static void Main(string[] args)
        {
            var program = new Program();
            program.SendServerAddress();
            program.ReceiveBroadcastMessages();
            Console.Read();
        }
    }
}
