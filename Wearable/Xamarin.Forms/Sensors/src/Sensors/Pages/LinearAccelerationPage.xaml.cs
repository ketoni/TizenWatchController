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

using Sensors.Extensions;
using Sensors.Model;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Tizen;
using Tizen.Sensor;
using Tizen.Wearable.CircularUI.Forms;
using Xamarin.Forms.Xaml;

namespace Sensors.Pages
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class LinearAccelerationPage : CirclePage
    {
        public LinearAccelerationPage()
        {
            Model = new LinearAccelerationModel
            {
                IsSupported = LinearAccelerationSensor.IsSupported,
                SensorCount = LinearAccelerationSensor.Count
            };
            InitializeComponent();
            
            if (Model.IsSupported)
            {
                LinearAcceleration = new LinearAccelerationSensor();
                LinearAcceleration.DataUpdated += LinearAcceleration_DataUpdated;
                LinearAcceleration.AccuracyChanged += LinearAcceleration_AccuracyChanged;

                LinearAcceleration.Interval = 50;

                canvas.Series = new List<Series>()
                {
                    new Series()
                    {
                        Color = SKColors.Red,
                        Name = "X",
                        FormattedText = "X={0:f2}m/s^2",
                    },
                    new Series()
                    {
                        Color = SKColors.Green,
                        Name = "Y",
                        FormattedText = "Y={0:f2}m/s^2",
                    },
                    new Series()
                    {
                        Color = SKColors.Blue,
                        Name = "Z",
                        FormattedText = "Z={0:f2}m/s^2",
                    },
                };
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

        private UdpClient client = new UdpClient();

        private void LinearAcceleration_AccuracyChanged(object sender, SensorAccuracyChangedEventArgs e)
        {
            Model.Accuracy = Enum.GetName(e.Accuracy.GetType(), e.Accuracy);
        }

        private void LinearAcceleration_DataUpdated(object sender, LinearAccelerationSensorDataUpdatedEventArgs e)
        {
            Model.X = e.X;
            Model.Y = e.Y;
            Model.Z = e.Z;
            canvas.InvalidateSurface();

            // Send values also to local server
            var message = $"{e.X};{e.Y};{e.Z};{DateTime.UtcNow.ToString("HH:mm:ss.fff")}";
            //Log.Debug("APP", "Sending " + message);
            var data = Encoding.UTF8.GetBytes(message);
            client.Send(data, data.Length, "192.168.0.100", 5555);
        }
    }
}