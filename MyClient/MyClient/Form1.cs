using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace MyClient
{
    public partial class Form1 : Form
    {
         Socket tcpSocket = new Socket(AddressFamily.InterNetwork,
                                        SocketType.Stream,
                                        ProtocolType.Tcp);
        // we need this delegate to change richTextBox and comboBox
        public delegate void Action(string text);

        public string clientName="";
        // get the local address of our machine
        private readonly IPAddress LocalAddress = Dns.GetHostEntry(Dns.GetHostName()).AddressList
            .Last(x => x.AddressFamily == AddressFamily.InterNetwork);
        private readonly int localPort = 4001;
        private List<string> listOfUsersToSend = new List<string>();
        public Form1()
        {
            InitializeComponent();
        }
        // we want to get  server local address 
        public  EndPoint GetServerAddress()
        {
            // buffer for the received message UPD: need to be updated every time
            var receivedBytesFromMulticast = new byte[100];

            // multicast address and port
            IPAddress mcastAddress = IPAddress.Parse("224.12.12.12");

            // we need multicast socket to send both  addresses between host and clients
            Socket mcastSocket = new Socket(AddressFamily.InterNetwork,
                                        SocketType.Dgram,
                                        ProtocolType.Udp);
            mcastSocket.Bind(new IPEndPoint(LocalAddress,localPort));

            int mcastPort = 4000;
            // things that were described at https://msdn.microsoft.com/ru-ru/library/system.net.sockets.multicastoption(v=vs.110).aspx
            EndPoint serverEndPoint = new IPEndPoint(mcastAddress, mcastPort);
            var multicastEndPoint = new IPEndPoint(mcastAddress, mcastPort);   
             var mcastOption = new MulticastOption(mcastAddress, LocalAddress);
            mcastSocket.SetSocketOption(SocketOptionLevel.IP,
                                        SocketOptionName.AddMembership,
                                        mcastOption);
            // now we want to send our localAddress:localPort information to the server
            mcastSocket.SendTo(Encoding.UTF8.GetBytes(LocalAddress+":"+localPort), multicastEndPoint);

            // we need to 'cut' the buffer(problem with zero bytes)
            var receivedCount = mcastSocket.ReceiveFrom(receivedBytesFromMulticast, ref serverEndPoint);
            var takenReceivedBytes = GetTrimmedBytes(receivedBytesFromMulticast, receivedCount);
            string stringEndpoint = Encoding.UTF8.GetString(takenReceivedBytes);
            char separator = ':';
            string[] strings = stringEndpoint.Split(separator);
            IPAddress serverAddress = IPAddress.Parse(strings[0]);
            EndPoint newServerEP = new IPEndPoint(serverAddress, mcastPort);
            button1.Enabled = false;

            // now we want to connect our tcp socket to the server
            return newServerEP;
        }
        // sending message via tcp
        void SendMessage(XDocument document)
        {
            tcpSocket.Send(GetBytesToSendFromDocument(document));
        }
        // here we want to define the type of the received message
        // and take the information we need
        public string DefineMessage(byte[] bytes)
        { 
            var receivedMessage = GetDocumentFromReceivedBytes(bytes);
            // creating the dictionary from the xml document to parse data correctly
            var dictionary = receivedMessage.Element("msg").Elements("f").ToDictionary
            (elem => elem.Attribute("n").Value,
            elem => elem.Attribute("v").Value);


            // simple switch-case to determine the type of the message
            switch (dictionary["Type"])
            {
                case "Send":
                    if (clientName == dictionary["Author"])
                        return "Вы написали:" + dictionary["Text"];
                    else
                        return dictionary["Author"]+" написал: " +dictionary["Text"];
                case "Name":
                    clientName = dictionary["Name"];
                        return "Вы подключились к чату";
                case "List":
                    // here we use this Action to write data into our control   
                    Invoke(new Action(collection =>
                    {
                        var charSeparators = new char[] { ',' };
                        var listOfUsers = collection.Split(charSeparators, StringSplitOptions.RemoveEmptyEntries);
                        listBox1.Items.Clear();
                        foreach (var user in listOfUsers)
                            listBox1.Items.Add(user);
                    }), dictionary["List"]);
                    return "Список юзеров обновлен!";
                case "PrivateSend":
                    if (dictionary["Author"] == clientName)
                    {
                        if (dictionary.Count == 4)
                            return "Вы написали  " + dictionary["Persons"] +":"+  dictionary["Text"];
                        else return dictionary["Author"]+ " написал вам:"+  dictionary["Text"];
                    }
                    else return dictionary["Author"]+" написал вам: " +dictionary["Text"];
            }
            return "something went wrong!";
        }
        // this method is in eternal loop 
        // so it just waits until it gets data
        // and start defining it
        private  string  ReceiveBroadcastMessages()
        { 
            var receivedBytes = new byte[2048];
            try
            {
                var receivedCount = tcpSocket.Receive(receivedBytes);
                var takenBytes = GetTrimmedBytes(receivedBytes, receivedCount);
                return DefineMessage(takenBytes);
            }
            catch (Exception)
            {
                timer1.Stop();
              return "Сервер разорвал соединение";
            }
          
        }
        // connection to the server 
        private void button1_OnClicked(object sender, EventArgs e)
        {
            tcpSocket.Connect(GetServerAddress());
            timer1.Start();
        }
        // method for private messages 
        // everything is the same to usual messages
        // but it also parses the elemets from listview
        public void SendPrivateMessage(string message)
        {
            var userString = "";
           userString =  string.Join(",", listBox1.SelectedItems.Cast<string>().Where(x => x != Text));
            var sendMessageDocument = new XDocument(
                new XElement("msg",
                    new XElement("f", new XAttribute("n", "Type"), new XAttribute("v", "PrivateSend")),
                    new XElement("f", new XAttribute("n", "Persons"), new XAttribute("v", userString)),
                    new XElement("f", new XAttribute("n", "Text"), new XAttribute("v", message))
                ));
            tcpSocket.Send(GetBytesToSendFromDocument(sendMessageDocument));
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            // simple action to put data in our richtextbox
            new Thread(() =>
                {
                    try
                    {
                        this.Invoke(new Action(str => richTextBox1.AppendText(str + "\n")), ReceiveBroadcastMessages());
                    }
                    catch(Exception)
                    {
                        tcpSocket.Close();
                        tcpSocket.Dispose();
                        Application.Exit();
                    }
                }
            ).Start();
        }

        private void button2_OnClicked(object sender, EventArgs e)
        {
            if(listBox1.SelectedItems.Count==0)
            SendMessage(CreateSendDocumentFrom(textBox1.Text));
            else SendPrivateMessage(textBox1.Text);
        }

        // simple parsers
        private XDocument CreateSendDocumentFrom(string message)
        {
            var sendMessageDocument = new XDocument(
                new XElement("msg",
                    new XElement("f", new XAttribute("n", "Type"), new XAttribute("v", "Send")),
                    new XElement("f", new XAttribute("n", "Text"), new XAttribute("v", message))));
            return sendMessageDocument;
        }

        private byte[] GetBytesToSendFromDocument(XDocument document)
        {
            return Encoding.UTF8.GetBytes(document.ToString());
        }

        private XDocument GetDocumentFromReceivedBytes(byte[] bytes)
        {
            return XDocument.Parse(Encoding.UTF8.GetString(bytes));
        }

        // simple trimming to make code easier to read
        private byte[] GetTrimmedBytes(byte[] receivedBytes, int receivedCount)
        {
            return receivedBytes.Take(receivedCount).ToArray();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            tcpSocket.Close();
            tcpSocket.Dispose();
            Application.Exit();
        }
    }
}
