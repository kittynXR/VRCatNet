
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

namespace VRCatNet
{
    public sealed partial class MainPage : Page
    {
        private async Task InitializeTwitchClient()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            var storedOAuthKey = localSettings.Values["OAuthKey"] as string;
            var broadcasterName = localSettings.Values["BroadcasterName"] as string;

            try
            {
                if (!string.IsNullOrEmpty(storedOAuthKey) && !string.IsNullOrEmpty(broadcasterName))
                {
                    // Configure the Twitch client
                    var credentials = new ConnectionCredentials(broadcasterName, storedOAuthKey);
                    twitchClient = new TwitchClient();
                    twitchClient.Initialize(credentials, _broadcasterName);

                    // Subscribe to relevant events
                    twitchClient.OnMessageSent += TwitchClient_OnMessageSent;
                    twitchClient.OnMessageReceived += TwitchClient_OnMessageReceived;
                    twitchClient.OnConnected += TwitchClient_OnConnected;
                    twitchClient.OnDisconnected += TwitchClient_OnDisconnected;
                    twitchClient.OnJoinedChannel += TwitchClient_OnJoinedChannel;
                    twitchClient.OnLeftChannel += TwitchClient_OnLeftChannel;

                    twitchClient.OnConnectionError += TwitchClient_OnConnectionError;
                    twitchClient.OnReconnected += TwitchClient_OnReconnected;

                    // Connect to Twitch
                    if (twitchClient != null) await ConnectTwitchClientAsync(twitchClient);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitializeTwitchClient exception: {ex.Message}");
            }
        }

        private async Task DisconnectTwitchClientAsync(TwitchClient twitchClient)
        {

        }

        private async Task ConnectTwitchClientAsync(TwitchClient twitchClient)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            async void OnConnected(object sender, OnConnectedArgs e)
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
                });
        }

        private async void TwitchClient_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    UpdateTextHistory($"Joined channel: {e.Channel}\n");
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

        private void TwitchClient_OnDisconnected(object sender, OnDisconnectedEventArgs e)
        {
            //sendButton.IsEnabled = false;
            // Handle disconnection, e.g., update UI or attempt to reconnect
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

        private async void TwitchClient_OnMessageSent(object sender, OnMessageSentArgs e)
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
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    UpdateTextHistory(e.ChatMessage.Message, e.ChatMessage.Username, e.ChatMessage.EmoteSet.Emotes);
                    ScrollToBottom();
                });
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

        private async void TwitchClient_OnReconnected(object sender, OnReconnectedEventArgs e)
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
