using CoreOSC;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace VRCatNet
{
    public sealed partial class MainPage : Page
    {
        private void InitializeOsc()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            var storedOscAddress = localSettings.Values["OSCAddress"] as string;
            var storedOscPort = localSettings.Values["OSCPort"] as string;
            var port = OscPort;

            if (string.IsNullOrWhiteSpace(storedOscAddress))
            {
                storedOscAddress = OscIP;
            }

            if (!string.IsNullOrWhiteSpace(storedOscPort))
            {
                port = int.Parse(storedOscPort);
            }

            oscSender = new UDPSender(storedOscAddress, port);
        }
    }
}
