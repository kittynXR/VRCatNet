using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace VRCatNet
{
    public sealed partial class ChatElementControl : UserControl
    {
        public ChatElementControl()
        {
            this.InitializeComponent();
        }

        public void SetText(string text)
        {
            ChatElementText.Text = text;
            ChatElementImage.Visibility = Visibility.Collapsed;
            ChatElementText.Visibility = Visibility.Visible;
        }

        public void SetImage(BitmapImage image)
        {
            ChatElementImage.Source = image;
            ChatElementText.Visibility = Visibility.Collapsed;
            ChatElementImage.Visibility = Visibility.Visible;
        }
    }
}
