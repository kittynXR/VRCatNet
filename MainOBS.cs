using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace VRCatNet
{
  public sealed partial class MainPage : Page
  {
    private Windows.Networking.Sockets.MessageWebSocket messageWebSocket;

    private void InitializeObs()
    {
      sceneSelector.Click           += SceneSelector_Click;
      sourceSelector.Click          += SourceSelector_Click;
      obsConfig.Click               += ObsConfig_Click;
      vrCat.Click                   += VrCat_Click;
      //obsRecordToggle.Click         += ObsRecordToggle_Click;
    }

    private async void ObsConfig_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      string OBSAddress = "127.0.0.1";
      string OBSPort = "4455";
      string OBSPassword = "";

      var localSettings = ApplicationData.Current.LocalSettings;

      string storedOBSAddress, storedObsPort, storedObsPassword;
      bool storedSSLOption, storedObsConnectOption, storedObsPasswordOption;

      if (localSettings.Values.TryGetValue("OBSAddress", out object obsAddress))
        storedOBSAddress = obsAddress as string;
      else
        storedOBSAddress = null;

      if (localSettings.Values.TryGetValue("OBSPort", out object obsPort))
        storedObsPort = obsPort as string;
      else
        storedObsPort = null;

      if (localSettings.Values.TryGetValue("OBSPassword", out object obsPassword))
        storedObsPassword = obsPassword as string;
      else
        storedObsPassword = null;

      if(localSettings.Values.TryGetValue("SSLOption", out object useSSLOption))
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

      var obsAddressInput  = new TextBox
      { PlaceholderText    = "OBS WS address: default ⇾ 127.0.0.1", Text = storedOBSAddress ?? "" };
      var obsPortInput     = new TextBox
      { PlaceholderText    = "OBS WS port: default ⇾ 4455", Text = storedObsPort ?? "" };
      var obsPasswordInput = new PasswordBox
      { PlaceholderText    = "OBS password: default ⇾ [none]", Password = storedObsPassword ?? "" };

      var useSSL = new CheckBox
      { Content = "Use SSL",
        IsChecked = storedSSLOption };
      var autoConnectObsCheckBox = new CheckBox
      { Content   = "Auto-connect",
        IsChecked = storedObsConnectOption };
      var rememberObsPasswordCheckBox = new CheckBox
      { Content = "Remember password", 
        IsChecked = storedObsPasswordOption };

      var obsConnect = new Button
      { Content = "Connect", HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Center };

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

      var obsDialog = new ContentDialog
      {
        Title = "OBS Settings",
        Content = new StackPanel
        {
          Children =
                    {
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
        if (!string.IsNullOrWhiteSpace(obsPasswordInput.Password))
        {
          localSettings.Values["OBSPassword"] = obsPasswordInput.Password;
        }

        localSettings.Values["OBSAddress"]  = obsAddressInput.Text;
        localSettings.Values["OBSPort"]     = obsPortInput.Text;

        localSettings.Values["SSLOption"] = useSSL.IsChecked;
        localSettings.Values["AutoConnectOBS"] = autoConnectObsCheckBox.IsChecked;
        localSettings.Values["RememberOBSPassword"] = rememberObsPasswordCheckBox.IsChecked;
      }
    }

    private async void SceneSelector_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      RequestScenes();
      //await OBSConnect(OBSAddress);
    }

    private async void SourceSelector_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      //await OBSConnect(OBSAddress);
    }

    private void obsRecordToggle_Checked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      SetCurrentScene("Record");
    }

    private void obsRecordToggle_Unchecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      SetCurrentScene("Record");
    }

    private void ObsRecordToggle_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      SetCurrentScene("Record");
    }

    private async void VrCat_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      SetCurrentScene("VRChatLive");
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

        UpdateTextHistory($"Connected to OBS.\n");
      }
      catch (Exception connectEx)
      {
        // Handle exceptions from ConnectAsync
        Debug.WriteLine(connectEx.ToString());
      }
    }

    private void WebSocket_MessageReceived(Windows.Networking.Sockets.MessageWebSocket sender, Windows.Networking.Sockets.MessageWebSocketMessageReceivedEventArgs args)
    {
      int eventSubscriptions = 0; // Start with all event subscriptions disabled

      // Enable the General category
      eventSubscriptions |= (1 << 0); // This is equivalent to eventSubscriptions = eventSubscriptions | 1;
      
      // Enable the Scenes category
      eventSubscriptions |= (1 << 2); // This is equivalent to eventSubscriptions = eventSubscriptions |

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
            // Try to access the rpcVersion property
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

              // Convert Identify message to JSON
              var identifyMessageJson = JsonConvert.SerializeObject(identifyMessage);

              // Send the Identify message
              SendMessageUsingMessageWebSocketAsync(identifyMessageJson).Wait();
            }
            catch (RuntimeBinderException ex)
            {
              Console.WriteLine("Failed to access rpcVersion property: " + ex.Message);
            }
          }

          // CHeck if the message is connection validated response
          if (message.op == 2)
          {

            Debug.WriteLine("Got message using MessageWebSocket: " + messageString);
          }

          if(message.op == 7)
          {
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
      Debug.WriteLine("WebSocket_Closed; Code: " + args.Code + ", Reason: \"" + args.Reason + "\"");
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