using System;
using System.Diagnostics;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using CoreOSC;


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace VRCatNet
{
    /// <summary>
    ///     An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private void ChatElementControl_Loaded(object sender, RoutedEventArgs e)
        {
            var chatElementControl = sender as ChatElementControl;
            var chatElement = chatElementControl.DataContext as ChatElement;

            if (chatElement.IsEmote)
                chatElementControl.SetImage(chatElement.EmoteImage);
            else
                chatElementControl.SetText(chatElement.Text);
        }

        private void toggleAudio_Checked(object sender, RoutedEventArgs e)
        {
            audioEnabled = true;
        }

        private void toggleAudio_Unchecked(object sender, RoutedEventArgs e)
        {
            audioEnabled = false;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            try
            {
                base.OnNavigatedTo(e);
                var localSettings = ApplicationData.Current.LocalSettings;
                var storedAutoConnect = (bool)localSettings.Values["AutoConnectTwitch"];

                if (storedAutoConnect == true)
                    await InitializeTwitchClient();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnNavigatedTo exception: {ex.Message}");
            }
        }

        private async void initTwitchButton_Click(object sender, RoutedEventArgs e)
        {
            if (!twitchIsConnected)
                try
                {
                    await InitializeTwitchClient();
                    initTwitchButton.Content = "Disconnect TTV";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"initTwitchButton_Click exception: {ex.Message}");
                }
            else
            {
                await ShutdownTwitchClient();
                twitchIsConnected = false;
                UpdateTextHistory("TTV Disconnected. . .");
                initTwitchButton.Content = "Connect TTV";
            }

            textInput.Focus(FocusState.Programmatic);
        }

        private async void discTwitchButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void textInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCharacterCounter();
        }

        private void TextInput_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && !isSendingMessage)
            {
                isSendingMessage = true;
                e.Handled = true;
                SendMessage();
                ScrollToBottom();
                isSendingMessage = false;
            }

            // Send a True signal to the /chatbox/typing OSC endpoint
            if (toggleOsc.IsChecked.Value && toggleTyping.IsChecked.Value)
                oscSender.Send(new OscMessage("/chatbox/typing", true));
        }

        private void TypingTimer_Tick(object sender, object e)
        {
            // Set the /chatbox/typing OSC endpoint to false when the timer ticks
            if (toggleOsc.IsChecked.Value) oscSender.Send(new OscMessage("/chatbox/typing", false));

            //toggleTyping.UpdateButtonColor();
            toggleTyping.SetTypingColor(false);

            typingTimer.Stop(); // Stop the timer
        }

        private void textInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // ...

            // Start/reset the timer when a key is pressed
            if (toggleOsc.IsChecked.Value && toggleTyping.IsChecked.Value)
            {
                typingTimer.Stop(); // Stop the timer if it's running
                typingTimer.Start(); // Start/reset the timer
                //toggleTyping.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                toggleTyping.SetTypingColor(true);
            }
            else
            {
                toggleTyping.UpdateButtonColor();
            }
        }

        private void toggleTyping_Checked(object sender, RoutedEventArgs e)
        {
            toggleTyping.Background = new SolidColorBrush(Colors.LightSeaGreen);
        }

        private void toggleTyping_Unchecked(object sender, RoutedEventArgs e)
        {
            toggleTyping.Background = new SolidColorBrush(Colors.LightGray);
            oscSender.Send(new OscMessage("/chatbox/typing", false));
        }

        private void togglePauseScroll_Checked(object sender, RoutedEventArgs e)
        {
            pauseScroll = true;
        }

        private void togglePauseScroll_Unchecked(object sender, RoutedEventArgs e)
        {
            pauseScroll = false;
            ScrollToBottom(); // Scroll to the bottom when the pause is released
        }

        private void ClearInputButton_Click(object sender, RoutedEventArgs e)
        {
            textInput.Text = "";
            textInput.Focus(FocusState.Programmatic);
        }

        private void ClearOscEndpointButton_Click(object sender, RoutedEventArgs e)
        {
            // Send an empty string to the /chatbox/input OSC endpoint
            oscSender.Send(new OscMessage("/chatbox/input", ""));
            textInput.Focus(FocusState.Programmatic);
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isSendingMessage)
            {
                isSendingMessage = true;
                messageSentByApp = true;
                SendMessage();
                isSendingMessage = false;
            }

            textInput.Focus(FocusState.Programmatic);
        }

        private async void oauthButton_Click(object sender, RoutedEventArgs e)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            var storedOAuthKey = localSettings.Values["OAuthKey"] as string;
            var storedBroadcasterName = localSettings.Values["BroadcasterName"] as string;
            var storedOscAddress = localSettings.Values["OSCAddress"] as string;
            var storedOscPort = localSettings.Values["OSCPort"] as string;
            var storedConnectOption = (bool)localSettings.Values["AutoConnectTwitch"];
            var storedOAuthOption = (bool)localSettings.Values["RememberOAuth"];

            // Create input fields for entering the broadcaster OAuth key, name, OSC address, and OSC port
            var oauthInput = new PasswordBox
            {
                PlaceholderText = "OAuth key",
                IsEnabled = storedOAuthKey == null
            };

            if (storedOAuthKey != null) oauthInput.Password = storedOAuthKey; // Replace with masked OAuth key

            var broadcasterNameInput = new TextBox
            {
                PlaceholderText = "Broadcaster name",
                IsEnabled = storedBroadcasterName == null,
                Text = storedBroadcasterName ?? ""
            };

            var oscAddressInput = new TextBox
                { PlaceholderText = "OSC address: default ⇾ 127.0.0.1", Text = storedOscAddress ?? "" };
            var oscPortInput = new TextBox
                { PlaceholderText = "OSC port: default ⇾ 9000", Text = storedOscPort ?? "" };

            var showOauthButton = new Button { Content = "Show OAuth key", IsEnabled = false };
            // Create a TextBlock and Button for the locked visual indicator and Edit button
            var oauthLabel = new TextBlock
                { Text = "OAuth Token: " };

            var broadcasterNameLabel = new TextBlock
                { Text = "Broadcaster Name: " };

            var lockedIndicator = new TextBlock
            {
                Text = "OAuth Saved!",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = storedOAuthKey != null ? Visibility.Visible : Visibility.Collapsed
            };
            var editButton = new Button
            {
                Content = "Edit OAuth token",
                Visibility = storedOAuthKey != null ? Visibility.Visible : Visibility.Collapsed
            };

            // Add a click event handler for the Edit button
            editButton.Click += (s, args) =>
            {
                oauthInput.IsEnabled = true;
                broadcasterNameInput.IsEnabled = true;
                lockedIndicator.Visibility = Visibility.Collapsed;
                editButton.Visibility = Visibility.Collapsed;
                showOauthButton.IsEnabled = true;
            };

            showOauthButton.Click += (s, args) =>
            {
                if (oauthInput.PasswordRevealMode == PasswordRevealMode.Hidden)
                {
                    oauthInput.PasswordRevealMode = PasswordRevealMode.Visible;
                    showOauthButton.Content = "Hide OAuth token";
                }
                else
                {
                    oauthInput.PasswordRevealMode = PasswordRevealMode.Hidden;
                    showOauthButton.Content = "Show OAuth token";
                }
            };

            // Create a HyperlinkButton for the OAuth token generator
            var oauthTokenGeneratorLink = new HyperlinkButton
            {
                Content = "Generate OAuth token",
                NavigateUri = new Uri("https://twitchapps.com/tmi/"),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Create checkboxes for remembering OAuth and automatically connecting to Twitch
            var rememberOAuthCheckBox = new CheckBox
                { Content = "Save OAuth between sessions", IsChecked = storedOAuthOption };
            var autoConnectTwitchCheckBox = new CheckBox
                { Content = "Automatically connect to Twitch", IsChecked = storedConnectOption };

            // Create a new input dialog for entering the broadcaster OAuth key, name, OSC address, and OSC port
            var oauthDialog = new ContentDialog
            {
                Title = "Settings",
                Content = new StackPanel
                {
                    Children =
                    {
                        //new StackPanel
                        //{
                        //    Orientation = Orientation.Horizontal,
                        //    Children =
                        //    {
                        //    }
                        //},
                        //new StackPanel
                        //{
                        //    Orientation = Orientation.Horizontal,
                        //    Children =
                        //    {
                        //    }
                        //},
                        oauthLabel, oauthInput,
                        broadcasterNameLabel, broadcasterNameInput,
                        new StackPanel // Nested StackPanel with Horizontal orientation
                        {
                            Orientation = Orientation.Horizontal,
                            Children =
                            {
                                showOauthButton, editButton
                            }
                        },
                        lockedIndicator,
                        //showOauthButton, lockedIndicator, editButton,
                        oauthTokenGeneratorLink, oscAddressInput,
                        oscPortInput, rememberOAuthCheckBox, autoConnectTwitchCheckBox
                    }
                },
                PrimaryButtonText = "OK",
                SecondaryButtonText = "Cancel"
            };

            // Show the dialog and update the Twitch client's OAuth key, broadcaster name, OSC address, and OSC port if provided
            var result = await oauthDialog.ShowAsync();

            // This is what happens when you click OK
            //
            if (result == ContentDialogResult.Primary)
            {
                if (!string.IsNullOrWhiteSpace(oauthInput.Password) &&
                    !string.IsNullOrWhiteSpace(broadcasterNameInput.Text))
                {
                    localSettings.Values["OAuthKey"] = oauthInput.Password;
                    localSettings.Values["BroadcasterName"] = broadcasterNameInput.Text;
                }

                localSettings.Values["OSCAddress"] = oscAddressInput.Text;
                localSettings.Values["OSCPort"] = oscPortInput.Text;

                if (oscAddressInput.Text != storedOscAddress || oscPortInput.Text != storedOscPort)
                    InitializeOsc();

                localSettings.Values["RememberOAuth"] = rememberOAuthCheckBox.IsChecked;
                localSettings.Values["AutoConnectTwitch"] = autoConnectTwitchCheckBox.IsChecked;

                textInput.Focus(FocusState.Programmatic);
            }
        }
    }
}