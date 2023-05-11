using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace VRCatNet
{
  public sealed partial class MainPage : Page
  {
    private const int ObsPort = 4455;
    //private const string ObsIP = "ws://127.0.0.1:4455/";
    private const string ObsIP = "ws://10.0.0.1:4455/";
    private const string ObsPassword = "";
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
      string storedObsIP, storedObsPort, storedObsPassword;
      var localSettings = ApplicationData.Current.LocalSettings;

      if (localSettings.Values.TryGetValue("ObsIP", out object obsIP))
        storedObsIP = obsIP as string;
      else
        storedObsIP = null;

      if (localSettings.Values.TryGetValue("ObsPort", out object obsPort))
        storedObsPort = obsPort as string;
      else
        storedObsPort = null;

      if (localSettings.Values.TryGetValue("ObsPassword", out object obsPassword))
        storedObsPassword = obsPassword as string;
      else
        storedObsPassword = null;

      var obsAddressInput = new TextBox
      { PlaceholderText = "OBS WS address: default ⇾ 127.0.0.1", Text = storedObsIP ?? "" };
      var obsPortInput = new TextBox
      { PlaceholderText = "OBS WS port: default ⇾ 4455", Text = storedObsPort ?? "" };
      var obsPasswordInput = new TextBox
      { PlaceholderText = "OBS password: default ⇾ [none]", Text = storedObsPassword ?? "" };

      var obsConnect = new Button
      { Content = "Connect", HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Center };

      obsConnect.Click += async (s, ee) =>
      {
        localSettings.Values["ObsIP"] = obsAddressInput.Text;
        localSettings.Values["ObsPort"] = obsPortInput.Text;
        localSettings.Values["ObsPassword"] = obsPasswordInput.Text;
        await OBSConnect();
      };

      var oauthDialog = new ContentDialog
      {
        Title = "Settings",
        Content = new StackPanel
        {
          Children =
                    {
                        obsAddressInput,
                        obsPortInput,
                        obsPasswordInput,
                        obsConnect
                    }
        },
        PrimaryButtonText = "OK",
        SecondaryButtonText = "Cancel"
      };

      // Show the dialog and update the Twitch client's OAuth key, broadcaster name, OSC address, and OSC port if provided
      var result = await oauthDialog.ShowAsync();

    }

    private async void SceneSelector_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      await OBSConnect();
    }

    private async void SourceSelector_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      await OBSConnect();
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

    private async Task OBSConnect()
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
        var connectTask = messageWebSocket.ConnectAsync(new Uri(ObsIP)).AsTask();
        await connectTask;

        // If we reach this line, the connection was successful
        // Now we wait for the Hello message from the server
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

            Debug.WriteLine("Got message using MessageWebSocket: " + message);
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