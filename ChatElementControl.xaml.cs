using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using VRCatNet;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

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
