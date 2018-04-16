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
        public Form1()
        {
            InitializeComponent();
        }

        public void GetServerAddress()
        {
            byte[] bytes = new byte[500];
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
            mcastSocket.ReceiveFrom(bytes, ref serverEndPoint);
            Thread.Sleep(500);
            tcpSocket.Connect(serverEndPoint);
            MessageBox.Show(serverEndPoint.ToString());
        }

         void SendMessage(string message)
        {
            var sendMessageDocument = new XDocument(
                  new XElement("msg",
                  new XElement("data", new XAttribute("key", "Type"), new XAttribute("value", "Send")),
                  new XElement("data", new XAttribute("key", "Text"), new XAttribute("value", message))));

              tcpSocket.Send(GetBytesToSendFromDocument(sendMessageDocument));
         }

        delegate void Receive(string text);

        public void DoSmth(string text)
        {
            richTextBox1.AppendText("\n"+text);

        }

        private  string  ReceiveBroadcastMessages()
        {
            byte[] bytes = new byte[500];
        
            bytes = bytes.Take(tcpSocket.Receive(bytes)).ToArray();
            string message = Encoding.UTF8.GetString(bytes);
            return message;
        }

        private byte[] GetBytesToSendFromDocument(XDocument document)
        {
            return Encoding.UTF8.GetBytes(document.ToString());

        }
        private void button1_Click(object sender, EventArgs e)
        {
            GetServerAddress();
            timer1.Start();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            SendMessage(textBox1.Text);
        }

        private void textBox2_KeyUp(object sender, KeyEventArgs e)
        {

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            Receive my = DoSmth;
            new Thread(() =>
            {
                this.Invoke(my, ReceiveBroadcastMessages());
            }
             ).Start();
        }
    }
}
