using CoreOSC;
using Windows.UI.Xaml.Controls;

namespace VRCatNet
{
    public sealed partial class MainPage : Page
    {
        private void InitializeOsc()
        {
            var ipAddress = _oscIP;
            var port = _oscPort;
            // Replace the IP and port with your OSC server's IP and port
            oscSender = new UDPSender(ipAddress, port);
        }
    }
}