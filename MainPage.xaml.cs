using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using CoreOSC;
using System;
using System.Diagnostics;
using Windows.Security.Credentials;
using Windows.UI.Core;

//////////
/////
///release for every platform///
////
////
////
//
////////////////////////// TODO:  make it so when you disconnect twitch it actually cleans up the chat
///   turn tabs into buttons
///   

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace VRCatNet
{
  /// <summary>
  ///     An empty page that can be used on its own or navigated to within a Frame.
  /// </summary>
  public sealed partial class MainPage : Page
  {
    private async void makeClip_Click(object sender, RoutedEventArgs e)
    {
      if(OBSIsConnected && OBSReplayEnabled)
      {
        await ObsRequest("SaveReplayBuffer");
      }
      //if(twitchIsConnected && twitchFullAuth)
      if(twitchIsConnected)
      {
        // TODO:  make this a function
      }
      textInput.Focus(FocusState.Programmatic);
    }
    
    private async void gButton_Click(object sender, RoutedEventArgs e)
    {
      if (!isSendingMessage)
      {
        isSendingMessage = true;
        messageSentByApp = true;
        textInput.Text = NutButtonText;
        
        await SendMessage();
        isSendingMessage = false;
      }

      textInput.Focus(FocusState.Programmatic);
    }
    
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

      textInput.Focus(FocusState.Programmatic);
    }

    private void toggleAudio_Unchecked(object sender, RoutedEventArgs e)
    {
      audioEnabled = false;
      textInput.Focus(FocusState.Programmatic);
    }

    private void toggleOsc_Checked(object sender, RoutedEventArgs e)
    {
      textInput.Focus(FocusState.Programmatic);
    }

    private void toggleOsc_Unchecked(object sender, RoutedEventArgs e)
    {
      textInput.Focus(FocusState.Programmatic);
    }

    private void toggleTwitch_Checked(object sender, RoutedEventArgs e)
    {
      textInput.Focus(FocusState.Programmatic);
    }

    private void toggleTwitch_Unchecked(object sender, RoutedEventArgs e)
    {
      textInput.Focus(FocusState.Programmatic);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
      try
      {
        base.OnNavigatedTo(e);
        var localSettings = ApplicationData.Current.LocalSettings;
        bool storedAutoConnect;
        if (localSettings.Values.TryGetValue("AutoConnectTwitch", out object storedValue))
        {
          storedAutoConnect = (bool)storedValue;
        }
        else
        {
          storedAutoConnect = false;
        }
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
        }
        catch (Exception ex)
        {
          Debug.WriteLine($"initTwitchButton_Click exception: {ex.Message}");
        }
      else
      {
        ShutdownTwitchClient();
        twitchClient.Disconnect();
        twitchIsConnected = false;
        await UpdateTextHistory("TTV Disconnected. . .");
      }

      textInput.Focus(FocusState.Programmatic);
    }

    private async void textInput_TextChanged(object sender, TextChangedEventArgs e)
    {
      await UpdateCharacterCounter();
    }

    private async void TextInput_KeyUp(object sender, KeyRoutedEventArgs e)
    {
      if (e.Key == VirtualKey.Enter && !isSendingMessage)
      {
        isSendingMessage = true;
        e.Handled = true;
        await SendMessage();
        await ScrollToBottom();
        isSendingMessage = false;
      }

      // Send a True signal to the /chatbox/typing OSC endpoint
      if (toggleOsc.IsChecked.Value && toggleTyping.IsChecked.Value)
        oscSender.Send(new OscMessage("/chatbox/typing", true));
    }

    private async void TypingTimer_Tick(object sender, object e)
    {
      // Set the /chatbox/typing OSC endpoint to false when the timer ticks
      if (toggleOsc.IsChecked.Value) oscSender.Send(new OscMessage("/chatbox/typing", false));

      if(Dispatcher.HasThreadAccess)
      {
        toggleTyping.SetTypingColor(false);
      }
      await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
      {
        toggleTyping.SetTypingColor(false);
      });

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
      textInput.Focus(FocusState.Programmatic);
    }

    private void toggleTyping_Unchecked(object sender, RoutedEventArgs e)
    {
      toggleTyping.Background = new SolidColorBrush(Colors.LightGray);
      oscSender.Send(new OscMessage("/chatbox/typing", false));
      textInput.Focus(FocusState.Programmatic);
    }

    private void togglePauseScroll_Checked(object sender, RoutedEventArgs e)
    {
      pauseScroll = true;
    }

    private async void togglePauseScroll_Unchecked(object sender, RoutedEventArgs e)
    {
      pauseScroll = false;
      await ScrollToBottom(); // Scroll to the bottom when the pause is released
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

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
      if (!isSendingMessage)
      {
        isSendingMessage = true;
        messageSentByApp = true;
        await SendMessage();
        isSendingMessage = false;
      }

      textInput.Focus(FocusState.Programmatic);
    }

    private async void oauthButton_Click(object sender, RoutedEventArgs e)
    {
      if (_currentDialog != null)
        return; // There is already a dialog open.

      var localSettings = ApplicationData.Current.LocalSettings;
      bool storedConnectOption, storedOAuthOption;
      string storedOscAddress, storedOscPort, storedObsAddress, storedObsPort, storedObsPassword;
      string storedNutButton;

      PasswordVault vault = new PasswordVault();

      string storedOAuthKey = GetCredential(vault, "OAuthKey");
      if (storedOAuthKey != null)
        twitchOAuthKey = storedOAuthKey;

      string storedBroadcasterName = GetCredential(vault, "BroadcasterName");
      if (storedBroadcasterName != null)
        twitchBroadcasterName = storedBroadcasterName;
      
      if (localSettings.Values.TryGetValue("NutButton", out object nutButton))
        storedNutButton = nutButton as string;
      else
        storedNutButton = null;

      if (localSettings.Values.TryGetValue("OSCAddress", out object oscAddress))
        storedOscAddress = oscAddress as string;
      else
        storedOscAddress = null;

      if (localSettings.Values.TryGetValue("OSCPort", out object oscPort))
        storedOscPort = oscPort as string;
      else
        storedOscPort = null;

      if (localSettings.Values.TryGetValue("OBSAddress", out object obsAddress))
        storedObsAddress = obsAddress as string;
      else
        storedObsAddress = null;

      if (localSettings.Values.TryGetValue("OBSPort", out object obsPort))
        storedObsPort = obsPort as string;
      else
        storedObsPort = null;

      if (localSettings.Values.TryGetValue("OBSPassword", out object obsPassword))
        storedObsPassword = obsPassword as string;
      else
        storedObsPassword = null;

      if (localSettings.Values.TryGetValue("AutoConnectTwitch", out object connectOption))
        storedConnectOption = (bool)connectOption;
      else
        storedConnectOption = false;

      if (localSettings.Values.TryGetValue("RememberOAuth", out object oauthOption))
        storedOAuthOption = (bool)oauthOption;
      else
        storedOAuthOption = false;
      // Create input fields for entering the broadcaster OAuth key, name, OSC address, and OSC port
      var oauthInput = new PasswordBox
      {
        PlaceholderText = "OAuth key",
        IsEnabled = twitchOAuthKey == null
      };

      if (twitchOAuthKey != null) oauthInput.Password = twitchOAuthKey; // Replace with masked OAuth key

      var broadcasterNameInput = new TextBox
      {
        PlaceholderText = "Broadcaster name",
        IsEnabled = twitchBroadcasterName == null,
        Text = twitchBroadcasterName ?? ""
      };

      var nutButtonInput = new TextBox
      { PlaceholderText = "Nut button: default ⇾ Nut", Text = storedNutButton ?? "" };

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
        Visibility = twitchOAuthKey != null ? Visibility.Visible : Visibility.Collapsed
      };
      var editButton = new Button
      {
        Content = "Edit OAuth token",
        Visibility = twitchOAuthKey != null ? Visibility.Visible : Visibility.Collapsed
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
                        oauthTokenGeneratorLink, oscAddressInput,
                        oscPortInput, rememberOAuthCheckBox, autoConnectTwitchCheckBox,
                        nutButtonInput
                    }
        },
        PrimaryButtonText = "OK",
        SecondaryButtonText = "Cancel"
      };

      _currentDialog = oauthDialog; // Set the _currentDialog before opening it.

      // Show the dialog and update the Twitch client's OAuth key, broadcaster name, OSC address, and OSC port if provided
      var result = await oauthDialog.ShowAsync();

      _currentDialog = null; // Clear the _currentDialog after closing it.


      // This is what happens when you click OK
      //
      if (result == ContentDialogResult.Primary)
      {
        if ((!string.IsNullOrWhiteSpace(oauthInput.Password) &&
            !string.IsNullOrWhiteSpace(broadcasterNameInput.Text)) 
                 && rememberOAuthCheckBox.IsChecked == true)  // weird how the bool && bool gets cast to a ?bool
        {
          SetCredential(vault, "OAuthKey", oauthInput.Password);
          SetCredential(vault, "BroadcasterName", broadcasterNameInput.Text);
        }
        else
        {
          ClearCredential(vault, "OAuthKey");
          ClearCredential(vault, "BroadcasterName");
        }

        localSettings.Values["OSCAddress"] = oscAddressInput.Text;
        localSettings.Values["OSCPort"] = oscPortInput.Text;

        if (oscAddressInput.Text != storedOscAddress || oscPortInput.Text != storedOscPort)
          InitializeOsc();

        localSettings.Values["AutoConnectTwitch"] = autoConnectTwitchCheckBox.IsChecked;
        localSettings.Values["RememberOAuth"] = rememberOAuthCheckBox.IsChecked;
        localSettings.Values["NutButton"] = nutButtonInput.Text;

        if (!string.IsNullOrWhiteSpace(nutButtonInput.Text))
          NutButtonText = nutButtonInput.Text;
        else
          NutButtonText = "!gamba all";

        textInput.Focus(FocusState.Programmatic);
      }
      if (result == ContentDialogResult.Secondary)
      {
        textInput.Focus(FocusState.Programmatic);
      }
    }

    //private void storeAuthData(string authString)
    //{
    //    string[] keyValuePairs = authString.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
    //    Dictionary<string, string> values = new Dictionary<string, string>();

    //    foreach (string keyValuePair in keyValuePairs)
    //    {
    //        // Split each key-value pair into key and value
    //        string[] keyValue = keyValuePair.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

    //        if (keyValue.Length == 2)
    //        {
    //            values[keyValue[0]] = keyValue[1];
    //        }
    //    }

    //    string username = values.ContainsKey("username") ? values["username"] : null;
    //    string user_id = values.ContainsKey("user_id") ? values["user_id"] : null;
    //    string client_id = values.ContainsKey("client_id") ? values["client_id"] : null;
    //    string oauth_token = values.ContainsKey("oauth_token") ? values["oauth_token"] : null;

    //    var localSettings = ApplicationData.Current.LocalSettings;

    //    localSettings.Values["username"] = username;
    //    localSettings.Values["user_id"] = user_id;
    //    localSettings.Values["client_id"] = client_id;
    //    localSettings.Values["client"] = oauth_token;
    //}
  }
}