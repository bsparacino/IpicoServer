using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Windows.Threading;

namespace IpicoServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int numChipReadsRaw = 0;
        private int numChipReadsUnique = 0;
        private int startButtonState = 0;

        private IPAddress readerIpAddress = null;        
        private int readerPort = 0;

        private IPAddress[] addresses;
        private IPAddress ipAddress = null;
        private int port;
        private WaitHandle addressesSet;
        private TcpClient tcpClient;
        private int failedConnectionCount;
        private String response = String.Empty;

        private HashSet<String> chips = new HashSet<String>();
        private List<String> reads = new List<String>();

        public MainWindow()
        {
            InitializeComponent();
            ipAddressTxt.Text = "192.168.0.51";
            ipAddressTxt.Text = "127.0.0.1";
            portTxt.Text = "10000";
        }

        private void startButton_Click_1(object sender, RoutedEventArgs e)
        {
            if (startButtonState == 1)
            {
                Console.WriteLine("STOP");
                startButtonState = 0;
                startButton.Content = "Connect";
                connectionStatus.Text = "";
                //receiveDone.Set();  
                tcpClient.Close();
            }
            else
            {
                Console.WriteLine("START");
                startButtonState = 1;
                startButton.Content = "Disconnect";
                connectionStatus.Text = "Connecting...";

                Console.WriteLine(ipAddressTxt.Text);

                try
                {
                    readerIpAddress = IPAddress.Parse(ipAddressTxt.Text);
                    readerPort = int.Parse(portTxt.Text);

                    ipAddress = IPAddress.Parse(ipAddressTxt.Text);
                    port = int.Parse(portTxt.Text);

                    addresses = new IPAddress[1];
                    addresses[0] = IPAddress.Parse(ipAddressTxt.Text);
                    


                    Thread clientThread = new Thread(() => clientStart());
                    clientThread.Start();

                    Thread parserThread = new Thread(() => parseBuffer());
                    parserThread.Start();




                    //clnt = new AsyncTcpClient(readerIpAddress, readerPort);
                    //clnt.Connect();

                    //new TcpEchoClient("192.168.0.51", 4096, 10000);

                    //tcpListenerThread = new Thread(() => StartListening(readerIpAddress, readerPort));
                    //tcpListenerThread = new Thread(() => StartClient(readerIpAddress, readerPort));
                    //tcpListenerThread.Start();

                    
                }
                catch (Exception e2)
                {
                    Console.WriteLine(e2.ToString()); 
                    connectionStatus.Text = e2.ToString();
                }
            }
        }

        private void exportButton_Click_1(object sender, RoutedEventArgs e)
        {
            // Configure save file dialog box
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = "Document"; // Default file name
            dlg.DefaultExt = ".text"; // Default file extension
            dlg.Filter = "Text Files (*.txt)|*.txt|All (*.*)|*"; // Filter files by extension 

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results 
            if (result == true)
            {
                String fileText = "";
                foreach (String read in reads)
                {
                    fileText += read;
                }

                // Save document 
                File.WriteAllText(dlg.FileName, fileText);                
            }
        }

        private void parseBuffer()
        {
            while (true)
            {
                Thread.Sleep(1);
                //obtain lock, parse one message from buffer
                lock (response)
                {
                    if (response.Length > 2)
                    {                        
                        String header = response.Substring(0, 2);
                        if (header == "aa") // Tag Read
                        {                            
                            if (response.Length >= 38)
                            {
                                String data = response.Substring(0, 38);
                                Console.WriteLine(data);
                                response = response.Remove(0, 38);

                                reads.Add(data);
                                numChipReadsRaw++;
                                Console.WriteLine(numChipReadsRaw);

                                Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
                                {
                                    numChipReadsRawTxt.Text = numChipReadsRaw.ToString();
                                }));

                                //String readerID = data.Substring(2, 2);
                                //String channelCounter = data.Substring(16, 4);
                                //String dateTime = data.Substring(20, 14);
                                //String checksum = data.Substring(34, 2);
                                //String end = data.Substring(36, 2);                

                                String chip = data.Substring(4, 12);

                                // Start Mat, get first read only
                                if (!chips.Contains(chip))
                                {
                                    chips.Add(chip);

                                    String year = data.Substring(20, 2);
                                    String month = data.Substring(22, 2);
                                    String day = data.Substring(24, 2);
                                    String hh = data.Substring(26, 2);
                                    String mm = data.Substring(28, 2);
                                    String ss = data.Substring(30, 2);
                                    String ms = Convert.ToInt32(data.Substring(32, 2), 16).ToString();
                                    String time = hh + ":" + mm + ":" + ss + "." + ms;

                                    Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
                                    {
                                        numChipReadsUniqueTxt.Text = chips.Count().ToString();
                                        txtConsole.Text += chips.Count().ToString() + ".";
                                        //txtConsole.Text += data;
                                        txtConsole.Text += "\n chip: " + chip;
                                        txtConsole.Text += "\n time: " + time + "\n\n";
                                    }));

                                }

                            }
                        }
                        else if (header == "ab") // Internal Time, could be the trigger
                        {
                            // 0-7 is ab date/time record designation
                            // 8-23 is date/time value                                                     

                            if (response.Length >= 32)
                            {
                                String data = response.Substring(0, 32);
                                response = response.Remove(0, 32);                                
                            }
                        }
                    }
                }
            }   
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("-------------------------------");
            //Console.WriteLine(clnt.getResponse());
            Console.WriteLine(response);
        }








        private void clientStart()
        {
            this.tcpClient = new TcpClient();
            this.Encoding = Encoding.Default;

            //Set the failed connection count to 0
            Interlocked.Exchange(ref failedConnectionCount, 0);
            //Start the async connect operation
            tcpClient.BeginConnect(addresses, port, ConnectCallback, null);
        }


        /// <summary>
        /// The endoding used to encode/decode string when sending and receiving.
        /// </summary>
        public Encoding Encoding { get; set; }

        /// <summary>
        /// Attempts to connect to one of the specified IP Addresses
        /// </summary>
        public void Connect()
        {
            if (addressesSet != null)
                //Wait for the addresses value to be set
                addressesSet.WaitOne();
            //Set the failed connection count to 0
            Interlocked.Exchange(ref failedConnectionCount, 0);
            //Start the async connect operation
            tcpClient.BeginConnect(ipAddress, port, ConnectCallback, null);
        }

        /// <summary>
        /// Callback for Connect operation
        /// </summary>
        /// <param name="result">The AsyncResult object</param>
        private void ConnectCallback(IAsyncResult result)
        {            
            try
            {
                tcpClient.EndConnect(result);
            }
            catch
            {
                //Increment the failed connection count in a thread safe way
                Interlocked.Increment(ref failedConnectionCount);
                if (failedConnectionCount >= addresses.Length)
                {
                    //We have failed to connect to all the IP Addresses
                    //connection has failed overall.
                    return;
                }
            }

            //We are connected successfully.
            NetworkStream networkStream = tcpClient.GetStream();
            byte[] buffer = new byte[tcpClient.ReceiveBufferSize];
            //Now we are connected start asyn read operation.
            networkStream.BeginRead(buffer, 0, buffer.Length, ReadCallback, buffer);
        }

        /// <summary>
        /// Callback for Read operation
        /// </summary>
        /// <param name="result">The AsyncResult object</param>
        private void ReadCallback(IAsyncResult result)
        {
            int read;
            NetworkStream networkStream;
            try
            {
                networkStream = tcpClient.GetStream();
                read = networkStream.EndRead(result);
            }
            catch
            {
                //An error has occured when reading
                return;
            }

            if (read == 0)
            {
                //The connection has been closed.
                return;
            }

            byte[] buffer = result.AsyncState as byte[];
            string data = this.Encoding.GetString(buffer, 0, read);

            lock (response)
            {
                response += data;
            }

            //Do something with the data object here.
            //Then start reading from the network again.
            networkStream.BeginRead(buffer, 0, buffer.Length, ReadCallback, buffer);
        }

        /// <summary>
        /// Callback for Get Host Addresses operation
        /// </summary>
        /// <param name="result">The AsyncResult object</param>
        private void GetHostAddressesCallback(IAsyncResult result)
        {
            addresses = Dns.EndGetHostAddresses(result);
            //Signal the addresses are now set
            ((AutoResetEvent)addressesSet).Set();
        }







    }


}
