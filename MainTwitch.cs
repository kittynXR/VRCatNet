
using System.Diagnostics;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Net.Http;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using TwitchLib.Api;
using TwitchLib.Api.Auth;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchLib.Api.Core.Enums;
using System.Collections.Generic;
using Windows.ApplicationModel.Resources.Core;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Microsoft.UI.Xaml.Controls;
using System.Linq;
using System.Windows.Input;
using Windows.Security.Credentials;

namespace VRCatNet
{
  public sealed partial class MainPage : Page
  {
    const int NumQuickItems = 10;
    private TwitchClient twitchClient;

    private string currentChannel;
    private bool twitchIsConnected;
    //private bool twitchFullAuth;
    private bool twitchAutoConnect;
    private bool twitchStoreAuth;
    private string twitchOAuthKey;
    private string twitchBroadcasterName;

    private List<StackPanel> gameButtonPanels;

    private async Task InitializeTwitchClient()
    {
      gameButtonPanels = new List<StackPanel>();

      var localSettings = ApplicationData.Current.LocalSettings;
      PasswordVault vault = new PasswordVault();

      if(twitchOAuthKey == null)
        twitchOAuthKey = GetCredential(vault, "OAuthKey");

      if(twitchBroadcasterName == null)
        twitchBroadcasterName = GetCredential(vault, "BroadcasterName");

      currentChannel = twitchBroadcasterName;

      changeChannels.Click          += ChangeChannels_Click;
      dropGame.Click                += QuickChat_Click;
      ttvPoints.Click               += TtvPoints_Click;
      twitchPrediction.Click        += TwitchPrediction_Click;
      twitchPoll.Click              += TwitchPoll_Click;

      try
      {
        if (!string.IsNullOrEmpty(twitchOAuthKey) && !string.IsNullOrEmpty(twitchBroadcasterName))
        {
          // Configure the Twitch client
          var credentials = new ConnectionCredentials(twitchBroadcasterName, twitchOAuthKey);
          twitchClient = new TwitchClient();
          twitchClient.Initialize(credentials, twitchBroadcasterName);

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

      //if(twitchFullAuth)
      //{
      //  twitchPrediction.IsEnabled = btnState;
      //  twitchPoll.IsEnabled = btnState;
      //}
    }

    private async void ChangeChannels_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      var localSettings = ApplicationData.Current.LocalSettings;
      string[] storedAltChannels = new string[NumQuickItems];
      TextBox[] newChannelInputs = new TextBox[NumQuickItems];
      Button[] changeButtons = new Button[NumQuickItems];
      Button resetButton = new Button { Content = "Reset", HorizontalAlignment = HorizontalAlignment.Left };

      var channelChangeStackPanel = new StackPanel();


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

      for (int i = 0; i < NumQuickItems; i++)
      {
        if (localSettings.Values.TryGetValue($"AltChannel{i}", out object altChannel))
          storedAltChannels[i] = altChannel as string;
        else
          storedAltChannels[i] = twitchBroadcasterName;

        newChannelInputs[i] = new TextBox
        {
          PlaceholderText = $"Channel {i + 1}",
          Text = storedAltChannels[i],
          HorizontalAlignment = HorizontalAlignment.Left
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
              changeChannelDialog.Hide();
              textInput.Focus(FocusState.Programmatic);
            });
            currentChannel = storedAltChannels[currentIndex];
          }
        };
      }

      for (int i = 0; i < NumQuickItems; i++)
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

      resetButton.Click += async (s, args) =>
      {
        if (twitchClient != null)
        {
          await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
          {
            twitchClient.LeaveChannel(currentChannel);
            Task.Delay(TimeSpan.FromSeconds(1));
            twitchClient.JoinChannel(twitchBroadcasterName);
            changeChannelDialog.Hide();
             textInput.Focus(FocusState.Programmatic);
          });
          currentChannel = twitchBroadcasterName;
        }
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
        textInput.Focus(FocusState.Programmatic);
      }

    }

    private async void QuickChat_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      var localSettings = ApplicationData.Current.LocalSettings;
      string[] storedQuickChat = new string[NumQuickItems];
      TextBox[] newQuickChatInputs = new TextBox[NumQuickItems];
      Button[] changeButtons = new Button[NumQuickItems];
      bool isEdit = true; // To toggle between Edit and Save
      Button editSaveButton = new Button { Content = "Edit", HorizontalAlignment = HorizontalAlignment.Left };

      var channelChangeStackPanel = new StackPanel();

      var quickChatDialog = new ContentDialog
      {
        Title = "Quick Chats",
        Content = new StackPanel
        {
          Children =
            {
                channelChangeStackPanel,
                editSaveButton
            }
        },
        PrimaryButtonText = "Close"
      };

      for (int i = 0; i < NumQuickItems; i++)
      {
        if (localSettings.Values.TryGetValue($"QuickChat{i}", out object quickChat))
          storedQuickChat[i] = quickChat as string;
        else
          storedQuickChat[i] = $"QC{i}";

        newQuickChatInputs[i] = new TextBox
        {
          PlaceholderText = $"Quick Chat {i + 1}",
          Text = storedQuickChat[i],
          HorizontalAlignment = HorizontalAlignment.Left,
          Visibility = Visibility.Collapsed
        };

        changeButtons[i] = new Button
        {
          Content = $"{storedQuickChat[i]}",
          HorizontalAlignment = HorizontalAlignment.Right
        };

        int currentIndex = i; // To avoid closure problem in async method
        changeButtons[i].Click += async (s, args) =>
        {
          if (twitchClient != null)
          {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
              textInput.Text = newQuickChatInputs[currentIndex].Text;
              SendMessage();
              quickChatDialog.Hide();
              textInput.Focus(FocusState.Programmatic);
            });
          }
        };
      }

      for (int i = 0; i < NumQuickItems; i++)
      {
        var channelGrid = new Grid
        {
          HorizontalAlignment = HorizontalAlignment.Stretch,
          Children =
            {
                newQuickChatInputs[i],
                changeButtons[i]
            }
        };

        channelChangeStackPanel.Children.Add(channelGrid);
      }

      editSaveButton.Click += (s, args) =>
      {
        isEdit = !isEdit;
        editSaveButton.Content = isEdit ? "Edit" : "Save";
        Visibility visibility = isEdit ? Visibility.Collapsed : Visibility.Visible;

        foreach (TextBox textBox in newQuickChatInputs)
        {
          textBox.Visibility = visibility;
        }

        if (!isEdit)
        {
          for (int i = 0; i < newQuickChatInputs.Length; i++)
          {
            if (!string.IsNullOrWhiteSpace(newQuickChatInputs[i].Text))
            {
              changeButtons[i].Content = newQuickChatInputs[i].Text;
              localSettings.Values[$"QuickChat{i}"] = newQuickChatInputs[i].Text;
            }
          }
        }
      };

      var result = await quickChatDialog.ShowAsync();

      if (result == ContentDialogResult.Primary)
      {
        for (int i = 0; i < newQuickChatInputs.Length; i++)
        {
          if (!string.IsNullOrWhiteSpace(newQuickChatInputs[i].Text))
          {
            localSettings.Values[$"QuickChat{i}"] = newQuickChatInputs[i].Text;
          }
        }
        textInput.Focus(FocusState.Programmatic);
      }
    }

    /*
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
        }*/

    private void TtvPoints_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView("Resources");

      string twitchClientId = resourceLoader.GetString("Twitch_Client_ID");
      string twitchClientSecret = resourceLoader.GetString("Twitch_Client_Secret");

      List<AuthScopes> scopes = new List<AuthScopes>()
      {
        AuthScopes.Channel_Read,
        AuthScopes.Channel_Subscriptions,
        AuthScopes.User_Read,
        AuthScopes.User_Subscriptions
      };

      TwitchAPI api = new TwitchLib.Api.TwitchAPI();
      api.Settings.ClientId = twitchClientId;
      api.Settings.Secret = twitchClientSecret;

      api.ThirdParty.AuthorizationFlow.CreateFlow("VRCatNet", scopes);
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
        twitchClient.JoinChannel(twitchBroadcasterName);
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
          UpdateTextHistory($"Disconnected from Twitch chat.\n");
          ToggleTwitchButtonState(false);
          toggleTwitch.IsChecked = false;
        });

      if(twitchClient != null)
      {
        ShutdownTwitchClient();
      }
      twitchIsConnected = false;
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
          e.ChatMessage.Username.Equals(twitchBroadcasterName, StringComparison.OrdinalIgnoreCase))
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
