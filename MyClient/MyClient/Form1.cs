using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
        public delegate void Action<T>(string text);
        string LocalAddress = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString();
         

        public Form1()
        {
            InitializeComponent();
        }

        public void GetServerAddress()
        {
            byte[] bytes = new byte[500];
            var dictionary = new Dictionary<string, string>();
            IPAddress mcastAddress = IPAddress.Parse("224.12.12.12");
             int  mcastPort = 4000;
            Socket mcastSocket = new Socket(AddressFamily.InterNetwork,
                                        SocketType.Dgram,
                                        ProtocolType.Udp);
            EndPoint serverEndPoint = new IPEndPoint(mcastAddress, mcastPort);

            IPEndPoint multicastEndPoint = new IPEndPoint(mcastAddress, mcastPort);
           
             MulticastOption mcastOption = new MulticastOption(mcastAddress, IPAddress.Parse(LocalAddress));

            mcastSocket.SetSocketOption(SocketOptionLevel.IP,
                                        SocketOptionName.AddMembership,
                                        mcastOption);
            mcastSocket.SendTo(Encoding.UTF8.GetBytes(LocalAddress + ":" + mcastPort), multicastEndPoint);
            
            bytes = bytes.Take(mcastSocket.ReceiveFrom(bytes, ref serverEndPoint)).ToArray();
            //richTextBox1.AppendText(define_message(bytes));
            
            tcpSocket.Connect(serverEndPoint);
            
        }

        void SendMessage(XDocument document)
        {
            tcpSocket.Send(GetBytesToSendFromDocument(document));
        }

        public string define_message(byte[] bytes)
        {
            var receivedMessage = GetDocumentFromReceivedBytes(bytes);
            var dictionary = receivedMessage.Element("msg").Elements("data").ToDictionary
            (elem => elem.Attribute("key").Value,
            elem => elem.Attribute("value").Value);

            switch (dictionary["Type"])
            {
                case "Send":
                    return $"{dictionary["Author"]} wrote  {dictionary["Text"]}";
                case "Name":
                    return $"{dictionary["Name"]} joined the chat";
                case "List":
                    
                  
                    Action<string> comboBoxAction = (collection) =>
                    {
                        char[] charSeparators = new char[] { ',' };
                        var listOfUsers = collection.Split(charSeparators, StringSplitOptions.RemoveEmptyEntries);
                        
                        comboBox1.Items.Clear();

                        foreach (var str in listOfUsers)
                        comboBox1.Items.Add(str);
                       
                    
                    };

                    Invoke(comboBoxAction, dictionary["List"]);
                    return $"Список юзеров обновлен!";
            }

            return "something went wrong!";

        }

        private string[] updateListOfUsers(string[] listOfUsers)
        {
            return listOfUsers;

        }

        private  string  ReceiveBroadcastMessages()
        {
            var bytes = new byte[500];
         
            bytes = bytes.Take(tcpSocket.Receive(bytes)).ToArray();

            return define_message(bytes);
        }

     
        private XDocument GetDocumentFromString(string message)
        {
            var sendMessageDocument = new XDocument(
                new XElement("msg",
                    new XElement("data", new XAttribute("key", "Type"), new XAttribute("value", "Send")),
                    new XElement("data", new XAttribute("key", "Text"), new XAttribute("value", message))));
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

        private void button1_Click(object sender, EventArgs e)
        {
            GetServerAddress();
            timer1.Start();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            
            SendMessage(GetDocumentFromString(textBox1.Text));
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            Action<string> myAction =  (str) => richTextBox1.AppendText("\n" + str);
            new Thread(() =>
            {
                this.Invoke(myAction, ReceiveBroadcastMessages());
            }
             ).Start();
            
        }
    }
}
