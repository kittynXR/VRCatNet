using System;
using System.Diagnostics;
using System.Threading.Tasks;
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
    //private const string ObsIP = "wss://127.0.0.1/";
    private const string ObsIP = "wss://10.0.0.2/";
    private const string ObsPassword = "";
    private Windows.Networking.Sockets.MessageWebSocket messageWebSocket;

    private void InitializeObs()
    {

    }
    private async Task OBSConnect()
    {
      messageWebSocket = new Windows.Networking.Sockets.MessageWebSocket();

      // In this example, we send/receive a string, so we need to set the MessageType to Utf8.
      messageWebSocket.Control.MessageType = Windows.Networking.Sockets.SocketMessageType.Utf8;

      messageWebSocket.MessageReceived += WebSocket_MessageReceived;
      messageWebSocket.Closed += WebSocket_Closed;

      try
      {
        System.Diagnostics.Debug.WriteLine("In try block");
        var connectTask = messageWebSocket.ConnectAsync(new Uri("ws://127.0.0.1:4455")).AsTask();
        await connectTask;

        // If we reach this line, the connection was successful
        // Now we can try to send the message
        try
        {
          await this.SendMessageUsingMessageWebSocketAsync("Hello, World!");
        }
        catch (Exception sendEx)
        {
          // Handle exceptions from SendMessageUsingMessageWebSocketAsync
          Debug.WriteLine(sendEx.ToString());
        }
      }
      catch (Exception connectEx)
      {
        // Handle exceptions from ConnectAsync
        Debug.WriteLine(connectEx.ToString());
      }
    }

    private async Task OBSListen()
    {

    }
    private async Task SendMessageUsingMessageWebSocketAsync(string message)
    {
      using (var dataWriter = new DataWriter(this.messageWebSocket.OutputStream))
      {
        dataWriter.WriteString(message);
        //_ = await dataWriter.StoreAsync();
        dataWriter.DetachStream();
      }
      Debug.WriteLine("Sending message using MessageWebSocket: " + message);
    }

    private void WebSocket_MessageReceived(Windows.Networking.Sockets.MessageWebSocket sender, Windows.Networking.Sockets.MessageWebSocketMessageReceivedEventArgs args)
    {
      try
      {
        using (DataReader dataReader = args.GetDataReader())
        {
          dataReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
          string message = dataReader.ReadString(dataReader.UnconsumedBufferLength);
          Debug.WriteLine("Message received from MessageWebSocket: " + message);
          this.messageWebSocket.Dispose();
        }
      }
      catch (Exception ex)
      {
        Windows.Web.WebErrorStatus webErrorStatus = Windows.Networking.Sockets.WebSocketError.GetStatus(ex.GetBaseException().HResult);
        // Add additional code here to handle exceptions.
      }
    }

    private void SetCurrentScene(string scene)
    {

    }

    private void WebSocket_Closed(Windows.Networking.Sockets.IWebSocket sender, Windows.Networking.Sockets.WebSocketClosedEventArgs args)
    {
      Debug.WriteLine("WebSocket_Closed; Code: " + args.Code + ", Reason: \"" + args.Reason + "\"");
      // Add additional code here to handle the WebSocket being closed.
    }
  }
}