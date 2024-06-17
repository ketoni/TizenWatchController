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

using System;

namespace Sensors.Model
{
    /// <summary>
    /// LinearAccelerationModel class.
    /// </summary>
    public class LinearAccelerationModel : BaseSensorModel
    {
        private string accuracy;
        private uint frequency;
        private string linkIndicator;
        private double latency;

        public string Accuracy
        {
            get { return accuracy; }
            set
            {
                accuracy = value;
                OnPropertyChanged();
            }
        }

        public uint Frequency 
        {
            get { return frequency; }
            set 
            {
                frequency = value;
                OnPropertyChanged();
            }
        }

        public string LinkIndicator
        {
            get { return linkIndicator; }
            set 
            {
                linkIndicator = value;
                OnPropertyChanged();
            }
        }

        public double Latency
        {
            get { return latency; }
            set 
            {
                latency = value;
                OnPropertyChanged();
            }
        }


    }
}