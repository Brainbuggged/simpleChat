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
        static string Host = Dns.GetHostName();
        string LocalAddress = Dns.GetHostEntry(Host).AddressList[0].ToString();
        Socket tcpSocket = new Socket(AddressFamily.InterNetwork,
                                        SocketType.Stream,
                                        ProtocolType.Tcp);

        public delegate void Action<T>(string text);

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
            richTextBox1.AppendText(defineMessage(bytes));
            
            tcpSocket.Connect(serverEndPoint);
            
        }

         void SendMessage(XDocument document)
        {
              tcpSocket.Send(GetBytesToSendFromDocument(document));
         }

        public string defineMessage(byte[] bytes)
        {
            var dictionary = new Dictionary<string, string>();
            var receivedMessage = GetDocumentFromReceivedBytes(bytes);
            foreach (XElement elem in receivedMessage.Element("msg").Elements("data"))
            {
                dictionary.Add(elem.Attribute("key").Value, elem.Attribute("value").Value);
            }

            if (dictionary["Type"] == "Send")
            {

                return $"{dictionary["Author"]} wrote  {dictionary["Text"]}";



            }
            if (dictionary["Type"] == "Name")
            {

                return $"{dictionary["Name"]} joined the chat";


            }

            return null;

        }

        private  string  ReceiveBroadcastMessages()
        {
            var bytes = new byte[500];
         
            bytes = bytes.Take(tcpSocket.Receive(bytes)).ToArray();

            return defineMessage(bytes);
        }

        private byte[] GetBytesToSendFromDocument(XDocument document)
        {
            return Encoding.UTF8.GetBytes(document.ToString());


        }
        private XDocument GetDocumentFromString(string message)
        {
            var sendMessageDocument = new XDocument(
                new XElement("msg",
                    new XElement("data", new XAttribute("key", "Type"), new XAttribute("value", "Send")),
                    new XElement("data", new XAttribute("key", "Text"), new XAttribute("value", message))));
            return sendMessageDocument;

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

        private void textBox2_KeyUp(object sender, KeyEventArgs e)
        {

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
