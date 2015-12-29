using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Rover
{
    public sealed class EngineController : IDisposable
    {
        string stopHtmlString = "<html><head><title>Rover</title></head><body><form action=\"rover\" method=\"GET\"><input type=\"radio\" name=\"state\" value=\"start\" onclick=\"this.form.submit()\"> Start<br><input type=\"radio\" name=\"state\" value=\"stop\" checked onclick=\"this.form.submit()\"> Stop</form></body></html>";
        string startHtmlString = "<html><head><title>Rover</title></head><body><form action=\"rover\" method=\"GET\"><input type=\"radio\" name=\"state\" value=\"start\" checked onclick=\"this.form.submit()\"> Start<br><input type=\"radio\" name=\"state\" value=\"stop\" onclick=\"this.form.submit()\"> Stop</form></body></html>";
        private const uint BufferSize = 8192;
        private string port = "8000";
        private readonly StreamSocketListener listener;

        public event EventHandler<string> StateChanged;

        public EngineController(string serverPort)
        {
            listener = new StreamSocketListener();
            listener.ConnectionReceived += (s, e) => ProcessRequestAsync(e.Socket);
            port = serverPort;
        }
        public EngineController(int serverPort):this(serverPort.ToString())
        {
        }

        public void StartServer()
        {
#pragma warning disable CS4014
            listener.BindServiceNameAsync(port.ToString());
#pragma warning restore CS4014
        }

        public void Dispose()
        {
            listener.Dispose();
        }

        private async void ProcessRequestAsync(StreamSocket socket)
        {
            // this works for text only
            StringBuilder request = new StringBuilder();
            using (IInputStream input = socket.InputStream)
            {
                byte[] data = new byte[BufferSize];
                IBuffer buffer = data.AsBuffer();
                uint dataRead = BufferSize;
                while (dataRead == BufferSize)
                {
                    await input.ReadAsync(buffer, BufferSize, InputStreamOptions.Partial);
                    request.Append(Encoding.UTF8.GetString(data, 0, data.Length));
                    dataRead = buffer.Length;
                }
            }

            using (IOutputStream output = socket.OutputStream)
            {
                string requestMethod = request.ToString().Split('\n')[0];
                string[] requestParts = requestMethod.Split(' ');

                if (requestParts[0] == "GET")
                    await WriteResponseAsync(requestParts[1], output);
                else
                    throw new InvalidDataException("HTTP method not supported: "
                                                   + requestParts[0]);
            }
        }

        private async Task WriteResponseAsync(string request, IOutputStream os)
        {
            // See if the request is for blinky.html, if yes get the new state
            string state = "Unspecified";
            bool stateChanged = false;
            if (request.Contains("rover?state=start"))
            {
                state = "Start";
                stateChanged = true;
            }
            else if (request.Contains("rover?state=stop"))
            {
                state = "Stop";
                stateChanged = true;
            }

            if (stateChanged)
            {
                if (StateChanged != null)
                {
                    StateChanged(this, state);
                }
            }

            string html = state == "Start" ? startHtmlString : stopHtmlString;
            // Show the html 
            using (Stream resp = os.AsStreamForWrite())
            {
                // Look in the Data subdirectory of the app package
                byte[] bodyArray = Encoding.UTF8.GetBytes(html);
                MemoryStream stream = new MemoryStream(bodyArray);
                string header = String.Format("HTTP/1.1 200 OK\r\n" +
                                  "Content-Length: {0}\r\n" +
                                  "Connection: close\r\n\r\n",
                                  stream.Length);
                byte[] headerArray = Encoding.UTF8.GetBytes(header);
                await resp.WriteAsync(headerArray, 0, headerArray.Length);
                await stream.CopyToAsync(resp);
                await resp.FlushAsync();
            }

        }
    }

}
