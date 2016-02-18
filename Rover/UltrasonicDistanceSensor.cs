﻿using System;
using System.Diagnostics;
using System.Runtime;
using System.Threading.Tasks;
using Windows.Devices.Gpio;

namespace Rover
{
    public class UltrasonicDistanceSensor : IDisposable
    {
        private GpioPin gpioPinTrig;
        private GpioPin gpioPinEcho;
        bool init;
        int trigGpioPin, echoGpioPin;
        public bool Initialized { get { return init; } }
        public UltrasonicDistanceSensor(int trigGpioPin, int echoGpioPin)
        {
            this.trigGpioPin = trigGpioPin;
            this.echoGpioPin = echoGpioPin;
        }

        public async Task<double> GetDistanceInCmAsync(int timeoutInMilliseconds)
        {
            return await Task.Run(() =>
            {
                GCLatencyMode oldMode = GCSettings.LatencyMode;
                try
                {
                    GCSettings.LatencyMode = GCLatencyMode.LowLatency;
                    double distance = double.MaxValue;
                    // turn on the pulse
                    gpioPinTrig.Write(GpioPinValue.High);
                    Task.Delay(TimeSpan.FromTicks(100)).Wait();
                    gpioPinTrig.Write(GpioPinValue.Low);
                    var sw = Stopwatch.StartNew();
                    bool timeout = false;
                    while (gpioPinEcho.Read() == GpioPinValue.Low)
                    {
                        if (sw.ElapsedMilliseconds > timeoutInMilliseconds)
                        {
                            timeout = true;
                            break;
                        }
                    }
                    if (!timeout)
                    {
                        sw.Restart();
                        while (sw.ElapsedMilliseconds < timeoutInMilliseconds && gpioPinEcho.Read() == GpioPinValue.High)
                        {
                            distance = sw.Elapsed.TotalSeconds * 17150;
                        }
                        Debug.WriteLine($"{sw.Elapsed.TotalSeconds} {distance}");
                        return distance;
                    }
                    throw new TimeoutException("The sensor did not respond in time.");
                }
                finally
                {
                    GCSettings.LatencyMode = oldMode;
                }
            });
        }

        public async Task InitAsync()
        {
            if (!init)
            {
                var gpio = GpioController.GetDefault();

                if (gpio != null)
                {
                    gpioPinTrig = gpio.OpenPin(trigGpioPin);
                    gpioPinEcho = gpio.OpenPin(echoGpioPin);
                    gpioPinTrig.SetDriveMode(GpioPinDriveMode.Output);
                    gpioPinEcho.SetDriveMode(GpioPinDriveMode.Input);
                    gpioPinTrig.Write(GpioPinValue.Low);

                    //first time ensure the pin is low and wait two seconds
                    gpioPinTrig.Write(GpioPinValue.Low);
                    await Task.Delay(2000);
                    init = true;
                }
                else
                {
                    throw new InvalidOperationException("Gpio not present");
                }
            }
        }

        public void Dispose()
        {
            if (gpioPinEcho != null)
            {
                gpioPinEcho.Dispose();
                gpioPinEcho = null;
            }
            if (gpioPinTrig != null)
            {
                gpioPinTrig.Dispose();
                gpioPinTrig = null;
            }
        }
    }
}
