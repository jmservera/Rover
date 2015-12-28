using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Gpio;

namespace Rover
{
    public class UltrasonicDistanceSensor
    {
        private readonly GpioPin gpioPinTrig;
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private readonly GpioPin gpioPinEcho;
        bool init;

        public UltrasonicDistanceSensor(int trigGpioPin, int echoGpioPin)
        {
            var gpio = GpioController.GetDefault();

            gpioPinTrig = gpio.OpenPin(trigGpioPin);
            gpioPinEcho = gpio.OpenPin(echoGpioPin);
            gpioPinTrig.SetDriveMode(GpioPinDriveMode.Output);
            gpioPinEcho.SetDriveMode(GpioPinDriveMode.Input);
            gpioPinTrig.Write(GpioPinValue.Low);
        }

        public async Task<double> GetDistanceInCmAsync(int timeoutInMilliseconds)
        {
            return await Task.Run(() =>
            {
                double distance = 0;
                // turn on the pulse
                gpioPinTrig.Write(GpioPinValue.High);
                var sw = Stopwatch.StartNew();
                while (distance < 9)
                {
                    distance = sw.Elapsed.TotalSeconds * 1000000;
                }
                Debug.WriteLine($"pulse: {sw.Elapsed.TotalSeconds * 1000000} µs");
                sw.Restart();
                bool high = false;
                gpioPinTrig.Write(GpioPinValue.Low);
                while (sw.Elapsed.TotalMilliseconds < timeoutInMilliseconds)
                {
                    if (gpioPinEcho.Read() != GpioPinValue.Low)
                    {
                        high = true;
                        break;
                    }
                }
                if (high)
                {
                    sw.Restart();
                    while (gpioPinEcho.Read() == GpioPinValue.High)
                    {
                        distance = sw.Elapsed.TotalSeconds * 17150;
                        if(sw.ElapsedMilliseconds > timeoutInMilliseconds)
                        {
                            throw new TimeoutException("Could not read from sensor");
                        }
                    }
                    Debug.WriteLine($"{distance} cm");
                    sw.Stop();
                    return distance;
                }
                else
                {
                    throw new TimeoutException("Could not read from sensor");
                }
            });
        }

        public async Task InitAsync()
        {
            if (!init)
            {
                //first time ensure the pin is low and wait two seconds
                gpioPinTrig.Write(GpioPinValue.Low);
                await Task.Delay(2000);
                init = true;
            }
        }
    }
}