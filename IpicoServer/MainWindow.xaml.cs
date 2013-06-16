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
        // Incoming data from the client.
        public static string data = null;

        private Thread tcpListenerThread;
        private int numChipReadsRaw = 0;
        private int numChipReadsUnique = 0;
        private int startButtonState = 0;

        private IPAddress readerIpAddress = null;
        private int readerPort = 0;

        private AsyncTcpClient clnt = null;

        private HashSet<String> chips = new HashSet<String>();
        private List<String> reads = new List<String>();

        public MainWindow()
        {
            InitializeComponent();
            ipAddress.Text = "192.168.0.51";
            ipAddress.Text = "127.0.0.1";
            port.Text = "10000";
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
                clnt.Close();
            }
            else
            {
                Console.WriteLine("START");
                startButtonState = 1;
                startButton.Content = "Disconnect";
                connectionStatus.Text = "Connecting...";

                Console.WriteLine(ipAddress.Text);

                try
                {
                    readerIpAddress = IPAddress.Parse(ipAddress.Text);
                    readerPort = int.Parse(port.Text);

                    clnt = new AsyncTcpClient(readerIpAddress, readerPort);
                    clnt.Connect();

                    //new TcpEchoClient("192.168.0.51", 4096, 10000);

                    //tcpListenerThread = new Thread(() => StartListening(readerIpAddress, readerPort));
                    //tcpListenerThread = new Thread(() => StartClient(readerIpAddress, readerPort));
                    //tcpListenerThread.Start();

                    Thread parserThread = new Thread(() => parseBuffer());
                    parserThread.Start();
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
                //obtain lock, parse one message from buffer
                lock (this)
                {
                    String data = clnt.getResponse();
                    if (data.Length > 2)
                    {
                        String header = data.Substring(0, 2);
                        if (header == "aa") // Tag Read
                        {
                            if (data.Length >= 38)
                            {
                                clnt.removeData(38);

                                reads.Add(data);
                                numChipReadsRaw++;

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
                        }
                    }
                }
            }   
        }

        private void parseData(String data)
        {
            reads.Add(data);

            Console.WriteLine(data);

            return;

            String header = data.Substring(0, 2);

            if (header == "aa") // Tag Read
            {
                numChipReadsRaw++;

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
            else if (header == "ab") // Internal Time, could be the trigger
            {
                // 0-7 is ab date/time record designation
                // 8-23 is date/time value 
            }
                

            
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("-------------------------------");
            Console.WriteLine(clnt.getResponse());
        }

    }


}
