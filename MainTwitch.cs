
using System.Diagnostics;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Net.Http;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Client;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using TwitchLib.Communication.Events;
using Windows.UI.Xaml.Media.Imaging;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace VRCatNet
{
  public sealed partial class MainPage : Page
  {
    private TwitchClient twitchClient;

    private string currentChannel;
    private string _broadcasterName;
    private bool twitchIsConnected;
    private bool twitchFullAuth;
    private bool twitchAutoConnect;

    private async Task InitializeTwitchClient()
    {
      var localSettings = ApplicationData.Current.LocalSettings;
      var storedOAuthKey = localSettings.Values["OAuthKey"] as string;
      var broadcasterName = localSettings.Values["BroadcasterName"] as string;
      _broadcasterName = localSettings.Values["BroadcasterName"] as string;
      currentChannel = _broadcasterName;

      changeChannels.Click          += ChangeChannels_Click;
      dropGame.Click                += DropGame_Click;
      ttvPoints.Click               += TtvPoints_Click;
      twitchPrediction.Click        += TwitchPrediction_Click;
      twitchPoll.Click              += TwitchPoll_Click;

      try
      {
        if (!string.IsNullOrEmpty(storedOAuthKey) && !string.IsNullOrEmpty(broadcasterName))
        {
          // Configure the Twitch client
          var credentials = new ConnectionCredentials(broadcasterName, storedOAuthKey);
          twitchClient = new TwitchClient();
          twitchClient.Initialize(credentials, _broadcasterName);

          // Subscribe to relevant events
          twitchClient.OnMessageSent      += TwitchClient_OnMessageSent;
          twitchClient.OnMessageReceived  += TwitchClient_OnMessageReceived;
          twitchClient.OnConnected        += TwitchClient_OnConnected;
          twitchClient.OnDisconnected     += TwitchClient_OnDisconnected;
          twitchClient.OnJoinedChannel    += TwitchClient_OnJoinedChannel;
          twitchClient.OnLeftChannel      += TwitchClient_OnLeftChannel;

          twitchClient.OnConnectionError  += TwitchClient_OnConnectionError;
          twitchClient.OnReconnected      += TwitchClient_OnReconnected;

          // Connect to Twitch
          if (twitchClient != null) await ConnectTwitchClientAsync(twitchClient);
        }
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"InitializeTwitchClient exception: {ex.Message}");
      }
    }

    private void ToggleTwitchButtonState(bool btnState)
    {
      ttvPoints.IsEnabled = btnState;
      toggleTwitch.IsEnabled = btnState;
      makeClip.IsEnabled = btnState;
      changeChannels.IsEnabled = btnState;
      dropGame.IsEnabled = btnState;
      gButton.IsEnabled = btnState;

      if(twitchFullAuth)
      {
        twitchPrediction.IsEnabled = btnState;
        twitchPoll.IsEnabled = btnState;
      }
    }

    private async void ChangeChannels_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      var localSettings = ApplicationData.Current.LocalSettings;
      string[] storedAltChannels = new string[5];
      TextBox[] newChannelInputs = new TextBox[5];
      Button[] changeButtons = new Button[5];
      Button resetButton = new Button { Content = "Reset", HorizontalAlignment = HorizontalAlignment.Left };

      for (int i = 0; i < 5; i++)
      {
        if (localSettings.Values.TryGetValue($"AltChannel{i}", out object altChannel))
          storedAltChannels[i] = altChannel as string;
        else
          storedAltChannels[i] = _broadcasterName;

        newChannelInputs[i] = new TextBox
        {
          PlaceholderText = $"Channel {i + 1}",
          Text = storedAltChannels[i],
          HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Left
        };

        changeButtons[i] = new Button
        {
          Content = $"Go Now {i + 1}",
          HorizontalAlignment = HorizontalAlignment.Right
        };

        int currentIndex = i; // To avoid closure problem in async method
        changeButtons[i].Click += async (s, args) =>
        {
          if (twitchClient != null)
          {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
              twitchClient.LeaveChannel(currentChannel);
              Task.Delay(TimeSpan.FromSeconds(1));
              twitchClient.JoinChannel(newChannelInputs[currentIndex].Text);
            });
            currentChannel = storedAltChannels[currentIndex];
          }
        };
      }

      resetButton.Click += async (s, args) =>
      {
        if (twitchClient != null)
        {
          await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
          {
            twitchClient.LeaveChannel(currentChannel);
            Task.Delay(TimeSpan.FromSeconds(1));
            twitchClient.JoinChannel(_broadcasterName);
          });
          currentChannel = _broadcasterName;
        }
      };

      var channelChangeStackPanel = new StackPanel();
      for (int i = 0; i < 5; i++)
      {
        var channelGrid = new Grid
        {
          HorizontalAlignment = HorizontalAlignment.Stretch,
          Children =
            {
                newChannelInputs[i],
                changeButtons[i]
            }
        };

        channelChangeStackPanel.Children.Add(channelGrid);
      }

      var changeChannelDialog = new ContentDialog
      {
        Title = "Change Chats",
        Content = new StackPanel
        {
          Children =
            {
                channelChangeStackPanel,
                resetButton
            }
        },
        PrimaryButtonText = "Close"
      };

      // Show the dialog and update the Twitch client's OAuth key, broadcaster name, OSC address, and OSC port if provided
      var result = await changeChannelDialog.ShowAsync();

      if (result == ContentDialogResult.Primary)
      {
        for (int i = 0; i < newChannelInputs.Length; i++)
        {
          if (!string.IsNullOrWhiteSpace(newChannelInputs[i].Text))
          {
            localSettings.Values[$"AltChannel{i}"] = newChannelInputs[i].Text;
          }
        }
      }

    }

    private async void DropGame_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      var dropDialog = new ContentDialog();

      var pizzaBtn = new Button
      { Content = "pineapple", HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Center };

      pizzaBtn.Click += (s, args) =>
      {
        textInput.Text = "!drop luunavrPizza";
        SendMessage();
        dropDialog.Hide();
        textInput.Focus(FocusState.Programmatic);
      };

      var cuteBtn = new Button
      { Content = "ucute", HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Center };

      cuteBtn.Click += (s, args) =>
      {
        textInput.Text = "!drop kittyn9Ucute";
        SendMessage();
        dropDialog.Hide();
        textInput.Focus(FocusState.Programmatic);
      };

      var derpBtn = new Button
      { Content = "derp", HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Center };

      derpBtn.Click += (s, args) =>
      {
        textInput.Text = "!drop totsDerp";
        SendMessage();
        dropDialog.Hide();
        textInput.Focus(FocusState.Programmatic);
      };

      dropDialog.Title = "Chat Games";
      dropDialog.Content = new StackPanel
      {
          Children =
                    {
                      pizzaBtn,
                      cuteBtn,
                      derpBtn
                    },
      };
      dropDialog.PrimaryButtonText = "Close";

      var result = await dropDialog.ShowAsync();
    }

    private void TtvPoints_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
    }

    private void TwitchPrediction_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
    }

    private void TwitchPoll_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
    }

    private void ShutdownTwitchClient()
    {
      twitchClient.OnMessageSent -= TwitchClient_OnMessageSent;
      twitchClient.OnMessageReceived -= TwitchClient_OnMessageReceived;
      twitchClient.OnConnected -= TwitchClient_OnConnected;
      twitchClient.OnDisconnected -= TwitchClient_OnDisconnected;
      twitchClient.OnJoinedChannel -= TwitchClient_OnJoinedChannel;
      twitchClient.OnLeftChannel -= TwitchClient_OnLeftChannel;

      twitchClient.OnConnectionError -= TwitchClient_OnConnectionError;
      twitchClient.OnReconnected -= TwitchClient_OnReconnected;

      twitchClient.Disconnect();
      twitchClient = null;
    }

    private async Task ConnectTwitchClientAsync(TwitchClient twitchClient)
    {
      TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

      void OnConnected(object sender, OnConnectedArgs e)
      {
        twitchClient.OnConnected -= OnConnected;
        tcs.SetResult(true);
      }

      async void OnJoinedChannel(object sender, OnJoinedChannelArgs e)
      {
        twitchClient.OnJoinedChannel -= OnJoinedChannel;
        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
              //UpdateTextHistory($"Joined channel: {e.Channel}\n");
              //sendButton.IsEnabled = true;
              ScrollToBottom();
            });
      }

      twitchClient.OnConnected += OnConnected;
      twitchClient.OnJoinedChannel += OnJoinedChannel;

      if (twitchClient.Connect())
      {
        await Task.Delay(TimeSpan.FromSeconds(1));
        twitchClient.JoinChannel(_broadcasterName);
        twitchIsConnected = true;
      }
      else
      {
        UpdateTextHistory("Unable to connect to TTV. . .");
      }

      await tcs.Task;
    }

    private async void TwitchClient_OnConnected(object sender, OnConnectedArgs e)
    {
      await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
          () =>
          {
            UpdateTextHistory($"Connected to Twitch chat.\n");
            ToggleTwitchButtonState(true);
            toggleTwitch.IsChecked = true;
          });
    }

    private async void TwitchClient_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
    {
      await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
          () =>
          {
            UpdateTextHistory($"Joined channel: {e.Channel}\n");
            currentChannel = e.Channel;
            //sendButton.IsEnabled = true;
            ScrollToBottom();
          });
    }

    private async void TwitchClient_OnLeftChannel(object sender, OnLeftChannelArgs e)
    {
      await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
          () =>
          {
            UpdateTextHistory($"Parted channel: {e.Channel}\n");
            //sendButton.IsEnabled = false;
            ScrollToBottom();
          });
    }

    private async void TwitchClient_OnDisconnected(object sender, OnDisconnectedEventArgs e)
    {
      await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
        () =>
        {
          UpdateTextHistory($"Connected to Twitch chat.\n");
          ToggleTwitchButtonState(false);
          toggleTwitch.IsChecked = false;
        });
    }

    private async Task<BitmapImage> GetEmoteImageAsync(string emoteUrl)
    {
      if (_emoteCache.ContainsKey(emoteUrl))
      {
        return _emoteCache[emoteUrl];
      }
      else
      {
        BitmapImage image = new BitmapImage();
        image.UriSource = new Uri(emoteUrl);
        using (var httpClient = new HttpClient())
        {
          var response = await httpClient.GetAsync(emoteUrl);
          if (response.IsSuccessStatusCode)
          {
            using (var stream = await response.Content.ReadAsStreamAsync())
            {
              await image.SetSourceAsync(stream.AsRandomAccessStream());
            }
          }
        }

        _emoteCache.Add(emoteUrl, image);
        return image;
      }
    }

    private void TwitchClient_OnMessageSent(object sender, OnMessageSentArgs e)
    {
      return;
    }

    private async void TwitchClient_OnMessageReceived(object sender, OnMessageReceivedArgs e)
    {
      await uiSemaphore.WaitAsync(); // Wait for the semaphore

      if (messageSentByApp &&
          e.ChatMessage.Username.Equals(_broadcasterName, StringComparison.OrdinalIgnoreCase))
      {
        messageSentByApp = false;
        return;
      }

      try
      {
        if (Dispatcher.HasThreadAccess)
        {
          UpdateTextHistory(e.ChatMessage.Message, e.ChatMessage.Username, e.ChatMessage.EmoteSet.Emotes);
          ScrollToBottom();
        }
        else
        {
          await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
          {
            UpdateTextHistory(e.ChatMessage.Message, e.ChatMessage.Username, e.ChatMessage.EmoteSet.Emotes);
            ScrollToBottom();
          });
        }
      }
      finally
      {
        uiSemaphore.Release(); // Release the semaphore
      }
    }

    private async void TwitchClient_OnConnectionError(object sender, OnConnectionErrorArgs e)
    {
      Debug.WriteLine($"Connection error: {e.Error.Message}");
      await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
          () =>
          {
            UpdateTextHistory($"Connection error: {e.Error.Message}\n");
          });
    }

    private async void TwitchClient_OnReconnected(object sender, EventArgs e)
    {
      Debug.WriteLine("Reconnected to Twitch chat.");
      await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
          () =>
          {
            UpdateTextHistory("Reconnected to Twitch chat.\n");
          });
    }

  }
}
