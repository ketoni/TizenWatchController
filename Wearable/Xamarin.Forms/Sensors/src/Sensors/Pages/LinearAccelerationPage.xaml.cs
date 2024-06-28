/*
 * Copyright (c) 2017 Samsung Electronics Co., Ltd All Rights Reserved
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Sensors.Model;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Tizen.Sensor;
using Tizen.Wearable.CircularUI.Forms;
using Xamarin.Forms.Xaml;

namespace Sensors.Pages
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class LinearAccelerationPage : CirclePage
    {
        private readonly string[] spinnerChars = new string[] { "|", "/", "-", "\\"};
        private int spinnerIdx = 0;
        public string Spinner
        {
            get 
            {
                return spinnerChars[spinnerIdx++ % spinnerChars.Length];
            }
        }
        private IPEndPoint clientEP = new IPEndPoint(IPAddress.Parse("192.168.0.159"), 5555);
        private LatencyCommunicator latencyChecker = null; 
        private UdpClient client = new UdpClient();





        // Changes the frequency of which the accelometer operates at
        private void SetSensorFrequency(uint freq)
        {
            Model.Frequency = freq;
            LinearAcceleration.Interval = 1000 / freq;
        } 

        // Callback for increasing sensor frequency, used in the page xaml definition
        public void IncreaseFrequency(object sender, EventArgs e)
        {
            var newFreq = Model.Frequency + 10;
            SetSensorFrequency(newFreq > 50 ? 50 : newFreq);
        }

        // Callback for decreasing sensor frequency, used in the page xaml definition
        public void DecreaseFrequency(object sender, EventArgs e)
        {
            var newFreq = Model.Frequency - 10;
            SetSensorFrequency(newFreq < 10 ? 10 : newFreq);
        }

        public LinearAccelerationPage()
        {
            Model = new LinearAccelerationModel
            {
                IsSupported = LinearAccelerationSensor.IsSupported,
                SensorCount = LinearAccelerationSensor.Count
            };
            InitializeComponent();

            latencyChecker = new LatencyCommunicator(clientEP);
            
            if (Model.IsSupported)
            {
                LinearAcceleration = new LinearAccelerationSensor();
                LinearAcceleration.DataUpdated += LinearAcceleration_DataUpdated;
                LinearAcceleration.AccuracyChanged += LinearAcceleration_AccuracyChanged;
                SetSensorFrequency(10);

                latencyChecker.active = true;
            }

        }

        public LinearAccelerationSensor LinearAcceleration { get; private set; }

        public LinearAccelerationModel Model { get; private set; }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            LinearAcceleration?.Start();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            LinearAcceleration?.Stop();
        }


        private void LinearAcceleration_AccuracyChanged(object sender, SensorAccuracyChangedEventArgs e)
        {
            Model.Accuracy = Enum.GetName(e.Accuracy.GetType(), e.Accuracy);
        }

        private void LinearAcceleration_DataUpdated(object sender, LinearAccelerationSensorDataUpdatedEventArgs e)
        {
            var message = $"D{e.X};{e.Y};{e.Z};{latencyChecker.Latency}";
            var data = Encoding.UTF8.GetBytes(message);
            client.Send(data, data.Length, clientEP);
            //Log.Debug("APP", "Sent" + message);

            Model.LinkIndicator = Spinner;
            Model.Latency = latencyChecker.Latency;
        }
    }

    public class LatencyCommunicator
    {
        public double Latency { get; private set; }
        public bool active;

        private UdpClient client = new UdpClient(5555);
        private IPEndPoint remoteEP = null;
        private Thread senderThread;


        public LatencyCommunicator(IPEndPoint endpoint)
        {
            // Start the thread to send messages and handle responses
            client.Client.ReceiveTimeout = 100;
            remoteEP = endpoint; 
            senderThread = new Thread(SendAndReceive)
            {
                IsBackground = true
            };
            senderThread.Start();
        }

        private void SendAndReceive()
        {
            try
            {
                while (true)
                {
                    Thread.Sleep(250);
                    if (!active)
                    {
                        continue;
                    }

                    // Send current timestamp 
                    var message = $"L{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                    var data = Encoding.UTF8.GetBytes(message);
                    client.Send(data, data.Length, remoteEP);

                    // Wait to receive the message back to calculate latency
                    try
                    {
                        var receiveBuffer = client.Receive(ref remoteEP);
                        var timestamp = long.Parse(Encoding.UTF8.GetString(receiveBuffer));
                        var rtt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp;
                        Latency = rtt / 2.0;
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode != SocketError.TimedOut)
                        {
                            throw;
                        }
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LatencyCommunicator thread: {ex.Message}");
            }
        }
    }
}