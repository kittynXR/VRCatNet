using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using CoreOSC;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Events;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml.Media.Imaging;
using System.IO;
using System.Collections.ObjectModel;
using Windows.UI.Xaml.Documents;


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace VRCatNet
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        //public static readonly DependencyProperty MaxCharactersProperty =
        //    DependencyProperty.Register("MaxCharacters", typeof(int), typeof(MainPage), new PropertyMetadata(500));

        //public int MaxCharacters
        //{
        //    get => (int)GetValue(MaxCharactersProperty);
        //    set => SetValue(MaxCharactersProperty, value);
        //}

        public MainPage()
        {
            this.InitializeComponent();
        }

        private void toggleAudio_Checked(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void toggleAudio_Unchecked(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void oauthButton_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void initTwitchButton_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void textInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void textInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void TextInput_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void TextHistory_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            throw new NotImplementedException();
        }
    }
}
