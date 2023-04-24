using CoreOSC;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace VRCatNet
{
    public sealed partial class MainPage : Page
    {

        private void InitializeOsc()
        {
            var ipAddress = "127.0.0.1";
            var port = 9000;
            // Replace the IP and port with your OSC server's IP and port
            oscSender = new UDPSender(ipAddress, port);
        }

        private void InitializeOsc()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            string storedOscAddress = localSettings.Values["OSCAddress"] as string;
            string storedOscPort = localSettings.Values["OSCPort"] as string;
            int port = OscPort;

            if (string.IsNullOrWhiteSpace(storedOscAddress))
            {
                storedOscAddress = OscIP;
            }

            if (storedOscPort != null)
            {
                port = int.Parse(storedOscPort);
            }

            // Replace the IP and port with your OSC server's IP and port
            oscSender = new UDPSender(storedOscAddress, port);
        }
    }
}


