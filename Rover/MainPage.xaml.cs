using Porrey.Uwp.IoT;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Rover
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    // ReSharper disable once RedundantExtendsListEntry
    public sealed partial class MainPage : Page
    {

        private BackgroundWorker _worker;
        private CoreDispatcher _dispatcher;
        EngineController controller = new EngineController("8000");
        private bool _finish;

        public MainPage()
        {
            InitializeComponent();

            Loaded += MainPage_Loaded;

            Unloaded += MainPage_Unloaded;
        }

        bool run = true;

        private void Controller_StateChanged(object sender, string state)
        {
            if (state == "Start")
            {
                run = true;
            }
            else if (state == "Stop")
            {
                run = false;
            }
        }

        GpioPin button;

        GpioPin r, g, b;

        private void MainPage_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            _dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;

            _worker = new BackgroundWorker();
            _worker.DoWork += DoWork;
            _worker.RunWorkerAsync();

            controller.StateChanged += Controller_StateChanged;
            controller.StartServer();

            var gpio=GpioController.GetDefault();
            button=gpio.OpenPin(4);
            if (button != null)
            {
                if (button.IsDriveModeSupported(GpioPinDriveMode.InputPullUp))
                    button.SetDriveMode(GpioPinDriveMode.InputPullUp);
                else
                    button.SetDriveMode(GpioPinDriveMode.Input);
                button.DebounceTimeout = TimeSpan.FromMilliseconds(50);
                button.ValueChanged += Button_ValueChanged;
            }
            //r = gpio.OpenPin(RPIN);
            //g = gpio.OpenPin(GPIN);
            //b = gpio.OpenPin(BPIN);
            //var pmr = r.AssignSoftPwm().WithPulseFrequency(800).WithValue(50).Start();
            //var pmg = g.AssignSoftPwm().WithPulseFrequency(800).WithValue(50).Start();
            //var pmb = b.AssignSoftPwm().WithPulseFrequency(800).WithValue(50).Start();
        }

        private void Button_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            if(args.Edge== GpioPinEdge.FallingEdge)
                run = !run;
        }

        private void MainPage_Unloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            _finish = true;
            if (button != null)
            {
                button.Dispose();
                button = null;
            }
            if (controller != null)
            {
                controller.Dispose();
                controller = null;
            }
        }

        bool turnRight = true;
        int turnCount;
        int flipCount;
        int exCount;

        private async void DoWork(object sender, DoWorkEventArgs e)
        {
            WriteLog("Initializing...");
            var driver = new TwoMotorsDriver(new Motor(27, 22), new Motor(5, 6));
            var ultrasonicDistanceSensor = new UltrasonicDistanceSensor(23, 24);
            await ultrasonicDistanceSensor.InitAsync();
            WriteLog("Sensor initialized");

            Stopwatch w = new Stopwatch();
            while (!_finish)
            {
                if (run)
                {
                    try
                    {
                        driver.MoveForward();

                        await Task.Delay(200);

                        var distance = await ultrasonicDistanceSensor.GetDistanceInCmAsync(100);
                        WriteData("Forward", distance);
                        if (distance > 35.0)
                            continue;

                        if (w.ElapsedMilliseconds < 1000)
                        {
                            if (turnCount++ > 3)
                            {
                                turnRight = !turnRight;
                                flipCount++;
                                turnCount = 0;
                            }
                            if (flipCount > 3)
                            {
                                WriteData("Back", distance);
                                driver.MoveBackward();
                                await Task.Delay(500);
                            }
                        }
                        else
                        {
                            turnCount = 0;
                            flipCount = 0;
                        }

                        if (turnRight)
                        {
                            //WriteLog($"Obstacle found at {distance:F2} cm or less. Turning right");
                            WriteData("Turn Right", distance);
                            await driver.TurnRightAsync();
                        }
                        else
                        {
                            WriteData("Turn Left", distance);
                            await driver.TurnLeftAsync();
                        }
                        w.Restart();
                        WriteLog("Moving forward");
                    }
                    catch (Exception ex)
                    {
                        WriteLog(ex.Message);
                        if (exCount++ > 4)
                        {
                            driver.Stop();
                            WriteData("Stop", -1);
                            exCount = 0;
                        }
                        else
                        {
                            driver.MoveBackward();
                            await Task.Delay(300);
                            WriteData("Back", -1);
                        }
                    }
                }
                else
                {
                    driver.Stop();
                    await Task.Delay(200);
                }
            }
        }

        private async void WriteLog(string text)
        {
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Log.Text += $"{text} | ";
                if (Log.Text.Length > 500)
                    Log.Text = Log.Text.Substring(100);
            });
        }

        private async void WriteData(string move, double distance)
        {
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                System.Diagnostics.Debug.WriteLine($"{move} {distance} cm");
                ForwardImage.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                TurnRightImage.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                switch (move)
                {
                    case "Forward":
                        ForwardImage.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        break;
                    case "Turn Right":
                        TurnRightImage.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        break;
                }
                Distance.Text = $"{distance:F2} cm";
            });
        }
    }
}