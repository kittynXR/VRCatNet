using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

// TODO:  reverse scene & sources order

namespace VRCatNet
{
  public class ScenesData
  {
    public string currentPreviewSceneName { get; set; }
    public string currentProgramSceneName { get; set; }
    public List<Scene> scenes { get; set; }
  }

  public class Scene
  {
    public int sceneIndex { get; set; }
    public string sceneName { get; set; }
    public bool isSelected { get; set; }
  }

  public class SourcesData
  {
    public List<Source> sceneItems { get; set; }
  }

  public class Source
  {
    public bool sceneItemEnabled { get; set; }
    public int sceneItemId { get; set; }
    public int sceneitemIndex { get; set; }
    public bool sceneItemLocked { get; set; }
    public string sourceName { get; set; }
  }

  public sealed partial class MainPage : Page
  {
    private Windows.Networking.Sockets.MessageWebSocket messageWebSocket;
    public Grid SceneGrid { get; set; }
    public Border SourceGrid { get; set; }

    public StackPanel stackPanel { get; set; }

    private bool obsAutoConnect = false;
    private bool obsStoreAuth = false;
    private string obsPassword;
    private bool OBSIsConnected = false;
    private bool OBSReplayEnabled = false;
    private string selectedSceneName;
    public delegate void SourcesDataReceivedHandler(string sourcesString);

    public event SourcesDataReceivedHandler OnSourcesDataReceived;

    private void InitializeObs()
    {
      sceneSelector.Click += SceneSelector_Click;
      //sourceSelector.Click += SourceSelector_Click;
      obsConfig.Click += ObsConfig_Click;
      obsRecordToggle.Click += ObsRecordToggle_Click;
      obsRecordToggle.Checked += ObsRecordToggle_Checked;
      obsRecordToggle.Unchecked += ObsRecordToggle_Unchecked;
      obsPauseToggle.Click += ObsPauseToggle_Click;
      obsPauseToggle.Checked += ObsPauseToggle_Checked;
      obsPauseToggle.Unchecked += ObsPauseToggle_Unchecked;
    }

    private async void ToggleObsButtonState(bool btnEnabled)
    {
      await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
      {
        sceneSelector.IsEnabled = btnEnabled;
        obsRecordToggle.IsEnabled = btnEnabled;
        if (btnEnabled == false)
          obsPauseToggle.IsEnabled = btnEnabled;
      });
    } 

    private async void ObsConfig_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      string OBSAddress   = "127.0.0.1";
      string OBSPort      = "4455";
      string OBSPassword  = "";

      var localSettings = ApplicationData.Current.LocalSettings;

      string storedOBSAddress, storedObsPort;
      bool storedSSLOption, storedObsConnectOption, storedObsPasswordOption;

      PasswordVault vault = new PasswordVault();
      string storedObsPassword = GetCredential(vault, "OBSPassword");

      if (storedObsPassword == "none")
        storedObsPassword = null;

      if (localSettings.Values.TryGetValue("OBSAddress", out object obsAddress))
        storedOBSAddress = obsAddress as string;
      else
        storedOBSAddress = null;

      if (localSettings.Values.TryGetValue("OBSPort", out object obsPort))
        storedObsPort = obsPort as string;
      else
        storedObsPort = null;

      if (localSettings.Values.TryGetValue("SSLOption", out object useSSLOption))
        storedSSLOption = (bool)useSSLOption;
      else
        storedSSLOption = false;

      if (localSettings.Values.TryGetValue("AutoConnectOBS", out object obsConnectOption))
        storedObsConnectOption = (bool)obsConnectOption;
      else
        storedObsConnectOption = false;

      if (localSettings.Values.TryGetValue("RememberOBSPassword", out object obsPasswordOption))
        storedObsPasswordOption = (bool)obsPasswordOption;
      else
        storedObsPasswordOption = false;

      var obsAddressInput = new TextBox
      { PlaceholderText = "OBS WS address: default ⇾ 127.0.0.1", Text = storedOBSAddress ?? "" };
      var obsPortInput = new TextBox
      { PlaceholderText = "OBS WS port: default ⇾ 4455", Text = storedObsPort ?? "" };
      var obsPasswordInput = new PasswordBox
      { PlaceholderText = "OBS password: default ⇾ [none]", Password = storedObsPassword ?? "" };

      var useSSL = new CheckBox
      {
        Content = "Use SSL",
        IsChecked = storedSSLOption
      };
      var autoConnectObsCheckBox = new CheckBox
      {
        Content = "Auto-connect",
        IsChecked = storedObsConnectOption
      };
      var rememberObsPasswordCheckBox = new CheckBox
      {
        Content = "Remember password",
        IsChecked = storedObsPasswordOption
      };

      var obsConnect = new Button
      { Content = "Connect", HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Center };

      string obsReplayContent;
      if(OBSReplayEnabled)
      {
        obsReplayContent = "Stop Replay Buffer";
      }
      else
      {
        obsReplayContent = "Start Replay Buffer";
      }

      var obsReplay = new Button
      { Content = obsReplayContent, HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Center };

      obsConnect.Click += async (s, args) =>
      {
        if (!string.IsNullOrWhiteSpace(obsPasswordInput.Password))
        {
          OBSPassword = obsPasswordInput.Password;
        }
        if (!string.IsNullOrWhiteSpace(obsAddressInput.Text))
        {
          OBSAddress = obsAddressInput.Text;
        }
        if (!string.IsNullOrWhiteSpace(obsPortInput.Text))
        {
          OBSPort = obsPortInput.Text;
        }

        OBSAddress = $"{(useSSL.IsChecked ?? false ? "wss" : "ws")}://{OBSAddress}:{OBSPort}/";

        await OBSConnect(OBSAddress);
      };

      obsReplay.Click += (s, args) =>
      {
        if (!OBSReplayEnabled)
        {
          obsReplay.Content = "Stop Replay Buffer";
          ObsRequest("StartReplayBuffer");
          makeClip.IsEnabled = true;
          OBSReplayEnabled = true;
        }
        else
        {
          obsReplay.Content = "Start Replay Buffer";
          ObsRequest("StopReplayBuffer");
          OBSReplayEnabled= false;
          if(!twitchFullAuth || !twitchIsConnected)
            makeClip.IsEnabled = false;
        }
      };

      var obsDialog = new ContentDialog
      {
        Title = "OBS Settings",
        Content = new StackPanel
        {
          Children =
                    {
                        obsReplay,
                        obsAddressInput,
                        obsPortInput,
                        obsPasswordInput,
                        useSSL,
                        obsConnect,
                        autoConnectObsCheckBox,
                        rememberObsPasswordCheckBox
                    }
        },
        PrimaryButtonText = "OK",
        SecondaryButtonText = "Cancel"
      };

      // Show the dialog and update the Twitch client's OAuth key, broadcaster name, OSC address, and OSC port if provided
      var result = await obsDialog.ShowAsync();

      if (result == ContentDialogResult.Primary)
      {
        if((bool)rememberObsPasswordCheckBox.IsChecked && !string.IsNullOrWhiteSpace(obsPasswordInput.Password))
        {
          SetCredential(vault, "OBSPassword", obsPasswordInput.Password);
        }
        else
        {
          ClearCredential(vault, "OBSPassword");
        }
        localSettings.Values["OBSAddress"] = obsAddressInput.Text;
        localSettings.Values["OBSPort"] = obsPortInput.Text;

        localSettings.Values["SSLOption"] = useSSL.IsChecked;
        localSettings.Values["AutoConnectOBS"] = autoConnectObsCheckBox.IsChecked;
        localSettings.Values["RememberOBSPassword"] = rememberObsPasswordCheckBox.IsChecked;
        textInput.Focus(FocusState.Programmatic);
      }
      if (result == ContentDialogResult.Secondary)
      {
        textInput.Focus(FocusState.Programmatic);
      }
    }

    private void SceneSelector_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      if (!OBSIsConnected) return;

      RequestScenes();
      //await OBSConnect(OBSAddress);
    }

    private async void SceneSelector(string scenesString)
    {
      var scenesData = JsonConvert.DeserializeObject<ScenesData>(scenesString);

      await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
      {
        stackPanel = new StackPanel();
        var sceneGrid = GenerateGrid(scenesData);
        stackPanel.Children.Add(sceneGrid);

        await RequestSources(scenesData.currentProgramSceneName);

        var border = new Border
        {
          BorderThickness = new Thickness(2),
          BorderBrush = new SolidColorBrush(Windows.UI.Colors.LightSeaGreen),
          Child = stackPanel
        };

        var dialog = new ContentDialog
        {
          Title = "Select Scene and Source",
          Content = new ScrollViewer
          {
            Content = border,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
          },
          PrimaryButtonText = "Close"
        };

        SourcesDataReceivedHandler handler = null;

        handler = async (sourcesString) =>
        {
          // Deserialize the sources data
          var sourcesData = JsonConvert.DeserializeObject<SourcesData>(sourcesString);

          await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
          {
            // Check if the sources grid already exists
            if (SourceGrid == null)
            {
              SourceGrid = await GenerateSourcesGridAsync(sourcesData);
              stackPanel.Children.Add(SourceGrid);
            }
            else
            {
              // Update the sources grid
              await UpdateSourcesGridAsync(sourcesData, (Grid)SourceGrid.Child);
            }
          });
        };

        OnSourcesDataReceived += handler;

        dialog.Closed += (s, e) =>
        {
          OnSourcesDataReceived -= handler;
          SourceGrid = null;
          stackPanel.Children.Clear();
          textInput.Focus(FocusState.Programmatic);
        };

        dialog.ShowAsync().AsTask().GetAwaiter();
      });
    }

    private Grid GenerateGrid(ScenesData scenesData, SourcesData sourcesData = null)
    {
      var grid = new Grid();

      for (var i = 0; i < scenesData.scenes.Count; i++)
      {
        grid.RowDefinitions.Add(new RowDefinition());
      }

      for (var i = 0; i < 3; i++)
      {
        grid.ColumnDefinitions.Add(new ColumnDefinition());
      }

      for (var i = 0; i < scenesData.scenes.Count; i++)
      {
        var button = new ToggleButton
        {
          Content = scenesData.scenes[i].sceneName,
          IsChecked = scenesData.scenes[i].sceneName == scenesData.currentProgramSceneName,
          Margin = new Thickness(2),  // 2 pixel margin around each button
          HorizontalAlignment = HorizontalAlignment.Stretch,
          VerticalAlignment = VerticalAlignment.Stretch
        };

        button.Checked += async (sender, args) =>
        {
          Console.WriteLine("Button checked event triggered");

          foreach (var child in grid.Children.OfType<ToggleButton>())
          {
            if (child != sender)
            {
              child.IsChecked = false;
            }
          }

          selectedSceneName = ((ToggleButton)sender).Content.ToString();
          SetCurrentScene(selectedSceneName);

          Debug.WriteLine("About to call RequestSources");
          // Retrieve sources for the selected scene
          await RequestSources(selectedSceneName);
          Debug.WriteLine("RequestSources method called");
        };

        grid.Children.Add(button);
        Grid.SetRow(button, i / 3);
        Grid.SetColumn(button, i % 3);
      }

      return grid;
    }

    private async Task UpdateSourcesGridAsync(SourcesData newSourcesData, Grid srcGrid)
    {
      await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
      {
        // Assuming SourceGrid is a class member variable
        srcGrid.Children.Clear();
        srcGrid.RowDefinitions.Clear();
        srcGrid.ColumnDefinitions.Clear();

        for (var i = 0; i < newSourcesData.sceneItems.Count; i++)
        {
          srcGrid.RowDefinitions.Add(new RowDefinition());
        }

        for (var i = 0; i < 3; i++)
        {
          srcGrid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        for (var i = 0; i < newSourcesData.sceneItems.Count; i++)
        {
          var toggleButton = new ToggleButton
          {
            Content = newSourcesData.sceneItems[i].sourceName,
            IsChecked = newSourcesData.sceneItems[i].sceneItemEnabled == true,
            Margin = new Thickness(2),  // 2 pixel margin around each button
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
          };

          var index = i;

          toggleButton.Checked += (sender, args) =>
          {
            SetSourceState(selectedSceneName, newSourcesData.sceneItems[index].sceneItemId, true);
          };

          toggleButton.Unchecked += (sender, args) =>
          {
            SetSourceState(selectedSceneName, newSourcesData.sceneItems[index].sceneItemId, false);
          };

          srcGrid.Children.Add(toggleButton);
          Grid.SetRow(toggleButton, i / 3);
          Grid.SetColumn(toggleButton, i % 3);
        }
      });
    }

    private async Task<Border> GenerateSourcesGridAsync(SourcesData sourcesData)
    {
      if(sourcesData == null)
      {
        Debug.WriteLine("sourcesData is null");
        return null;
      }
      var tcs = new TaskCompletionSource<Border>();

      await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
      {
        try
    {
        var grid = new Grid();

        for (var i = 0; i < sourcesData.sceneItems.Count; i++)
        {
          grid.RowDefinitions.Add(new RowDefinition());
        }

        for (var i = 0; i < 3; i++)
        {
          grid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        for (var i = 0; i < sourcesData.sceneItems.Count; i++)
        {
          var toggleButton = new ToggleButton
          {
            Content = sourcesData.sceneItems[i].sourceName,
            IsChecked = sourcesData.sceneItems[i].sceneItemEnabled == true,
            Margin = new Thickness(2),  // 2 pixel margin around each button
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
          };

          var index = i; // Create a new variable inside the loop

          toggleButton.Checked += (sender, args) =>
          {
            SetSourceState(selectedSceneName, sourcesData.sceneItems[index].sceneItemId, true);
          };

          toggleButton.Unchecked += (sender, args) =>
          {
            SetSourceState(selectedSceneName, sourcesData.sceneItems[index].sceneItemId, false);
          };

          grid.Children.Add(toggleButton);
          Grid.SetRow(toggleButton, i / 3);
          Grid.SetColumn(toggleButton, i % 3);
        }

        var border = new Border
        {
          BorderBrush = new SolidColorBrush(Colors.LightPink),
          BorderThickness = new Thickness(2),
          Child = grid
        };

        tcs.SetResult(border);
        }
    catch (Exception e)
    {
        Debug.WriteLine(e);
        tcs.SetException(e);
    }
      });

      return await tcs.Task;
    }

    private void ObsRecordToggle_Checked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      if (!OBSIsConnected) return;

      obsPauseToggle.IsEnabled = true;
      obsRecordToggle.Content = "STOP\nREC";
      ObsRequest("StartRecord");
      textInput.Focus(FocusState.Programmatic);
    }

    private void ObsRecordToggle_Unchecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      if (!OBSIsConnected) return;

      obsPauseToggle.IsEnabled = false;
      obsPauseToggle.IsChecked = false;

      obsRecordToggle.Content = "*REC*";
      ObsRequest("StopRecord");

      textInput.Focus(FocusState.Programmatic);
    }

    private void ObsRecordToggle_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      //SetCurrentScene("Record");
    }

    private void ObsPauseToggle_Checked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      if (!OBSIsConnected) return;

      obsPauseToggle.Content = "resume\nrecording";
      ObsRequest("PauseRecord");

      textInput.Focus(FocusState.Programmatic);
    }

    private void ObsPauseToggle_Unchecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      if (!OBSIsConnected) return;
      obsPauseToggle.Content = "pause\nrecording";
      ObsRequest("ResumeRecord");
      textInput.Focus(FocusState.Programmatic);
    }

    private async void ObsRequest(string command)
    {
      string requestId = Guid.NewGuid().ToString();

      // Create the request
      var request = new
      {
        op = 6,
        d = new
        {
          requestType = command,
          requestId = requestId
        }
      };

      // Convert the request to JSON
      var requestJson = JsonConvert.SerializeObject(request);

      // Send the request
      await SendMessageUsingMessageWebSocketAsync(requestJson);
    }

    private void ObsPauseToggle_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      //SetCurrentScene("Pause");
    }

    private async Task OBSConnect(string OBSAddress)
    {
      messageWebSocket = new Windows.Networking.Sockets.MessageWebSocket();

      // Set the MessageType to Utf8.
      messageWebSocket.Control.MessageType = Windows.Networking.Sockets.SocketMessageType.Utf8;

      // Set the protocol to obswebsocket.json
      messageWebSocket.Control.SupportedProtocols.Add("obswebsocket.json");

      messageWebSocket.MessageReceived += WebSocket_MessageReceived;
      messageWebSocket.Closed += WebSocket_Closed;

      try
      {
        System.Diagnostics.Debug.WriteLine("In try block");
        var connectTask = messageWebSocket.ConnectAsync(new Uri(OBSAddress)).AsTask();
        await connectTask;

        OBSIsConnected = true;
        UpdateTextHistory($"Connected to OBS.\n");
        ToggleObsButtonState(true);
      }
      catch (Exception connectEx)
      {
        // Handle exceptions from ConnectAsync
        Debug.WriteLine(connectEx.ToString());
      }
    }

    public string GenerateAuthenticationString(string password, string salt, string challenge)
    {
      // Convert the salt and challenge from Base64 to byte arrays
      byte[] saltBytes = Convert.FromBase64String(salt);
      byte[] challengeBytes = Convert.FromBase64String(challenge);

      // Concatenate the password and salt
      //string passwordAndSalt = password + Encoding.UTF8.GetString(saltBytes);
      string passwordAndSalt = password + saltBytes;

      // Generate a SHA256 hash of the password and salt, and Base64 encode it
      byte[] passwordAndSaltBytes = Encoding.UTF8.GetBytes(passwordAndSalt);
      byte[] passwordAndSaltHash = SHA256.Create().ComputeHash(passwordAndSaltBytes);
      string base64Secret = Convert.ToBase64String(passwordAndSaltHash);

      // Concatenate the base64 secret and challenge
      string secretAndChallenge = base64Secret + Encoding.UTF8.GetString(challengeBytes);

      // Generate a SHA256 hash of the secret and challenge, and Base64 encode it
      byte[] secretAndChallengeBytes = Encoding.UTF8.GetBytes(secretAndChallenge);
      byte[] secretAndChallengeHash = SHA256.Create().ComputeHash(secretAndChallengeBytes);
      string authenticationString = Convert.ToBase64String(secretAndChallengeHash);

      return authenticationString;
    }

    private void WebSocket_MessageReceived(Windows.Networking.Sockets.MessageWebSocket sender, Windows.Networking.Sockets.MessageWebSocketMessageReceivedEventArgs args)
    {
      int eventSubscriptions = 0; // Start with all event subscriptions disabled

      // Enable the General category
      eventSubscriptions |= (1 << 0); // This is equivalent to eventSubscriptions = eventSubscriptions | 1;

      // Enable the Scenes category
      eventSubscriptions |= (1 << 2); // This is equivalent to eventSubscriptions = eventSubscriptions |

      eventSubscriptions |= (1 << 7);

      try
      {
        using (DataReader reader = args.GetDataReader())
        {
          reader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
          string read = reader.ReadString(reader.UnconsumedBufferLength);

          // Parse the received message
          var message = JsonConvert.DeserializeObject<dynamic>(read);
          string messageString = JsonConvert.SerializeObject(message);

          // Print out the message to the debug output
          Debug.WriteLine("In got message: " + messageString);

          // Check if the message is a Hello message
          if (message.op == 0)
          {
            // Extract the rpcVersion from the Hello message
            if (message.d.authentication != null)
            {
              int rpcVersion = message.d.rpcVersion;
              Console.WriteLine(rpcVersion);

              PasswordVault vault = new PasswordVault();
              string password = GetCredential(vault, "OBSPassword");

              var authtoken = GenerateAuthenticationString(password, message.d.authentication.salt, message.d.authentication.challenge);
              // Create Identify message
              var identifyMessage = new
              {
                d = new
                {
                  rpcVersion = rpcVersion,
                  eventSubscriptions = eventSubscriptions,
                  authentication = authtoken
                  // Add appropriate session parameters here
                },
                op = 1
              };
              var identifyMessageJson = JsonConvert.SerializeObject(identifyMessage);

              // Send the Identify message
              SendMessageUsingMessageWebSocketAsync(identifyMessageJson).Wait();
            }
            else
            {
              Debug.WriteLine("No Auth");
              Debug.WriteLine("Got message using MessageWebSocket: " + messageString);
              try
              {
                int rpcVersion = message.d.rpcVersion;
                Console.WriteLine(rpcVersion);

                // Create Identify message
                var identifyMessage = new
                {
                  d = new
                  {
                    rpcVersion = rpcVersion,
                    eventSubscriptions = eventSubscriptions
                    // Add appropriate session parameters here
                  },
                  op = 1
                };
                var identifyMessageJson = JsonConvert.SerializeObject(identifyMessage);

                // Send the Identify message
                SendMessageUsingMessageWebSocketAsync(identifyMessageJson).Wait();
              }
              catch (RuntimeBinderException ex)
              {
                Console.WriteLine("Failed to access rpcVersion property: " + ex.Message);
              }
            }

            // Convert Identify message to JSON
          }

          // CHeck if the message is connection validated response
          if (message.op == 2)
          {

            Debug.WriteLine("Got message using MessageWebSocket: " + messageString);
          }
          
          if (message.op == 7)
          {
            if ((message.d.requestType == "GetSceneList") && (message.d.responseData != null))
            {
              SceneSelector(JsonConvert.SerializeObject(message.d.responseData));
            }
            if ((message.d.requestType == "GetSceneItemList") && (message.d.responseData != null))
            {
              //var jObject = JObject.Parse(JsonConvert.SerializeObject(message.d.responseData));
              //var sceneItems = jObject["sceneItems"] as IEnumerable<JToken>;
              //var sourceNames = sceneItems.Select(item => item["sourceName"].ToString()).ToList();
              //Debug.WriteLine("Got message using MessageWebSocket: " + JsonConvert.SerializeObject(sourceNames));
              //OnSourcesDataReceived?.Invoke(JsonConvert.SerializeObject(sourceNames));
              OnSourcesDataReceived?.Invoke(JsonConvert.SerializeObject(message.d.responseData));
            }


            Debug.WriteLine("Got message using MessageWebSocket: " + messageString);
          }

        }
      }
      catch (RuntimeBinderException ex)
      {
        Debug.WriteLine("Failed to process server response: " + ex.Message);
      }
      catch (Exception ex)
      {
        Debug.WriteLine(ex.ToString());
      }
    }

    private async void SendStreamCaption(string caption)
    {
      if (caption == null) return;
      if (!OBSIsConnected) return;

      string requestId = Guid.NewGuid().ToString();

      var request = new
      {
        op = 6,
        d = new
        {
          requestType = "SendStreamCaption",
          requestId = requestId,
          requestData = new
          {
            captionText = caption
          }
        }
      };
      // Convert the request to JSON
      var requestJson = JsonConvert.SerializeObject(request);
      // Send the request
      await SendMessageUsingMessageWebSocketAsync(requestJson);
    }

    private async void SetCurrentScene(string scene)
    {
      if (scene == null)
      {
        return;
      }

      // Create a new UUID for the request
      string requestId = Guid.NewGuid().ToString();

      // Create the request
      var request = new
      {
        op = 6,
        d = new
        {
          requestType = "SetCurrentProgramScene",
          requestId = requestId,
          requestData = new
          {
            sceneName = scene
          }
        }
      };

      // Convert the request to JSON
      var requestJson = JsonConvert.SerializeObject(request);

      // Send the request
      await SendMessageUsingMessageWebSocketAsync(requestJson);
    }

    private async void SetSourceState(string scene, int source, bool sourceState)
    {
      if (scene == null)
      {
        return;
      }

      // Create a new UUID for the request
      string requestId = Guid.NewGuid().ToString();

      // Determine the requestType based on addSource
      string requestType = "SetSceneItemEnabled";

      // Create the request
      var request = new
      {
        op = 6,
        d = new
        {
          requestType = requestType,
          requestId = requestId,
          requestData = new
          {
            sceneName = scene,
            sceneItemId = source,
            sceneItemEnabled = sourceState
          }
        }
      };

      // Convert the request to JSON
      var requestJson = JsonConvert.SerializeObject(request);

      // Send the request
      await SendMessageUsingMessageWebSocketAsync(requestJson);
    }

    private void GetCurrentScene(string scene)
    {
      if (scene == null)
      {
        return;
      }
    }

    private async void RequestScenes()
    {
      // Create a new UUID for the request
      string requestId = Guid.NewGuid().ToString();

      // Create the request
      var request = new
      {
        op = 6,
        d = new
        {
          requestType = "GetSceneList",
          requestId = requestId
        }
      };

      // Convert the request to JSON
      var requestJson = JsonConvert.SerializeObject(request);

      // Send the request
      await SendMessageUsingMessageWebSocketAsync(requestJson);
    }

    private async Task RequestSources(string sceneName)
    {
      // Create a new UUID for the request
      string requestId = Guid.NewGuid().ToString();

      // Create the request
      var request = new
      {
        op = 6,
        d = new
        {
          requestType = "GetSceneItemList",
          requestId = requestId,
          requestData = new
          {
            sceneName = sceneName
          }
        }
      };

      // Convert the request to JSON
      var requestJson = JsonConvert.SerializeObject(request);

      // Send the request
      await SendMessageUsingMessageWebSocketAsync(requestJson);
    }

    private void WebSocket_Closed(Windows.Networking.Sockets.IWebSocket sender, Windows.Networking.Sockets.WebSocketClosedEventArgs args)
    {
      // You can add code to log or display the code and reason
      // for the closure (stored in args.Code and args.Reason)
      // Use the following codes to determine the action to take.
      // 1000 - normal closure
      // 1011 - server is restarting
      // 1012 - server has encountered an error
      // 1013 - server is overloaded
      // 1014 - server refuses handshake
      // 1015 - TLS or SSL error
      OBSIsConnected = false;
      Debug.WriteLine("WebSocket_Closed; Code: " + args.Code + ", Reason: \"" + args.Reason + "\"");
      ToggleObsButtonState(false);
      // Add additional code here to handle the WebSocket being closed.
    }

    private async Task SendMessageUsingMessageWebSocketAsync(string message)
    {
      using (var dataWriter = new DataWriter(this.messageWebSocket.OutputStream))
      {
        dataWriter.WriteString(message);
        await dataWriter.StoreAsync();
        dataWriter.DetachStream();
      }
      Debug.WriteLine("Sending message using MessageWebSocket: " + message);
    }

  }
}