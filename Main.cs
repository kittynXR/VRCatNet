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
using System.Collections.Generic;
using System.Threading;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using System.Linq;
using Windows.ApplicationModel;
using Windows.UI;

namespace VRCatNet
{

  public sealed partial class MainPage : Page
  {
    private const int OscPort = 9000;
    private const string OscIP = "127.0.0.1";

    public static readonly DependencyProperty MaxCharactersProperty =
        DependencyProperty.Register("MaxCharacters", typeof(int), typeof(MainPage), new PropertyMetadata(500));

    private readonly DispatcherTimer typingTimer;

    private readonly SemaphoreSlim uiSemaphore = new SemaphoreSlim(1, 1);
    private bool audioEnabled;
    private bool OBSIsConnected;
    private bool isSendingMessage;
    private bool messageSentByApp;

    private UDPSender oscSender;
    private bool pauseScroll;


    private Dictionary<string, BitmapImage> _emoteCache = new Dictionary<string, BitmapImage>();

    public MainPage()
    {
      InitializeComponent();
      Loaded += MainPage_Loaded;

      var localSettings = ApplicationData.Current.LocalSettings;

      toggleTyping.UpdateButtonColor();

      twitchIsConnected = false;
      typingTimer = new DispatcherTimer();
      typingTimer.Interval = TimeSpan.FromSeconds(1); // Set the interval to 1 second, or change it to the desired delay
      typingTimer.Tick += TypingTimer_Tick;

      // Add event handlers for the send button and return key
      sendButton.Click += SendButton_Click;

      oscTriggers.Click             += OscTriggers_Click;
      gButton.Click                 += gButton_Click;
      textInput.KeyDown             += TextInput_KeyUp;
      clearInputButton.Click        += ClearInputButton_Click;
      clearOscEndpointButton.Click  += ClearOscEndpointButton_Click;

      Application.Current.Suspending += new SuspendingEventHandler(OnSuspending);
      Window.Current.Activated += OnActivated;

      UpdateCharacterCounter();
    }

    private void OscTriggers_Click(object sender, RoutedEventArgs e)
    {
      //throw new NotImplementedException();
    }

    public int MaxCharacters
    {
      get => (int)GetValue(MaxCharactersProperty);
      set => SetValue(MaxCharactersProperty, value);
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
      var localSettings = ApplicationData.Current.LocalSettings;
      InitializeOsc();
      InitializeObs();

      if (localSettings.Values.TryGetValue("AutoConnectTwitch", out object connectOption))
        twitchAutoConnect = (bool)connectOption;
      if (localSettings.Values.TryGetValue("AutoConnectOBS", out object obsConnectOption))
        obsAutoConnect = (bool)obsConnectOption;
      if(obsAutoConnect)
      {
        string OBSAddress = "127.0.0.1";
        string OBSPort = "4455";
        string OBSPassword = "";
        bool? SSLOption = false;

        if (localSettings.Values.TryGetValue("OBSAddress", out object obsAddress) && !string.IsNullOrEmpty(obsAddress as string))
          OBSAddress = obsAddress as string;

        if (localSettings.Values.TryGetValue("OBSPort", out object obsPort) && !string.IsNullOrEmpty(obsPort as string))
          OBSPort = obsPort as string;

        if (localSettings.Values.TryGetValue("OBSPassword", out object obsPassword) && !string.IsNullOrEmpty(obsPassword as string))
          OBSPassword = obsPassword as string;

        if (localSettings.Values.TryGetValue("SSLOption", out object useSSLOption) && useSSLOption != null)
          SSLOption = (bool)useSSLOption;

        OBSAddress = $"{(SSLOption ?? false ? "wss" : "ws")}://{OBSAddress}:{OBSPort}/";
        await OBSConnect(OBSAddress);
      }
      if(twitchAutoConnect)
      {
        try
        {
          await InitializeTwitchClient();
          initTwitchButton.Content = "Disconnect TTV";
        }
        catch (Exception ex)
        {
          Debug.WriteLine($"initTwitchButton_Click exception: {ex.Message}");
        }
      }
    }

    private void TextHistory_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
      if (args.InRecycleQueue) return;

      if (args.Phase == 0)
      {
        var container = args.ItemContainer as ListViewItem;
        var chatItem = args.Item as ChatItem;
        if (container != null && chatItem != null)
        {
          args.RegisterUpdateCallback(1, (s, e) => LoadChatItem(container, chatItem, e));
        }
        args.Handled = true;
      }
    }

    private void LoadChatItem(ListViewItem container, ChatItem chatItem, ContainerContentChangingEventArgs args)
    {
      if (args.Phase == 1)
      {
        var rtb = container.ContentTemplateRoot as RichTextBlock;
        if (rtb != null)
        {
          var paragraph = rtb.Blocks.FirstOrDefault() as Paragraph;
          if (paragraph != null)
          {
            paragraph.Inlines.Clear();
            foreach (var chatElement in chatItem.ChatElements)
            {
              if (chatElement.IsEmote)
              {
                Image image = new Image
                {
                  Source = chatElement.EmoteImage,
                  Width = 28,
                  Height = 28,
                  VerticalAlignment = VerticalAlignment.Center
                };

                InlineUIContainer iContainer = new InlineUIContainer
                {
                  Child = image
                };
                paragraph.Inlines.Add(iContainer);
              }
              else
              {
                Run run = new Run
                {
                  Text = chatElement.Text
                };
                paragraph.Inlines.Add(run);
              }
            }
          }
        }
      }

      args.Handled = true;
    }

    private async void UpdateTextHistory(string message, string username = "", IList<Emote> emotes = null)
    {
      var chatItem = new ChatItem { ChatElements = new ObservableCollection<ChatElement>() };
      chatItem.ChatElements.Add(new ChatElement { Text = $"{username}: " });

      if (emotes != null)
      {
        var currentIndex = 0;
        foreach (var emote in emotes)
        {
          if (emote.StartIndex > currentIndex)
            chatItem.ChatElements.Add(new ChatElement
            { Text = message.Substring(currentIndex, emote.StartIndex - currentIndex) });

          string emoteUrl = $"https://static-cdn.jtvnw.net/emoticons/v1/{emote.Id}/1.0";
          var emoteImage = await GetEmoteImageAsync(emoteUrl);
          chatItem.ChatElements.Add(new ChatElement { EmoteImage = emoteImage, IsEmote = true });
          currentIndex = emote.EndIndex + 1;
        }

        if (currentIndex < message.Length)
          chatItem.ChatElements.Add(new ChatElement { Text = message.Substring(currentIndex) });
      }
      else
      {
        chatItem.ChatElements.Add(new ChatElement { Text = message });
      }

      try
      {
        textHistory.Items.Add(chatItem);
        textHistory.UpdateLayout();
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"Error adding chat message: {ex.Message}");
      }
    }

    private void UpdateCharacterCounter()
    {
      var charactersRemaining = MaxCharacters - textInput.Text.Length;
      characterCounter.Text = $"{charactersRemaining}/{MaxCharacters}";

      if (charactersRemaining <= MaxCharacters * 0.15)
        characterCounter.Foreground = new SolidColorBrush(Colors.Red);
      else
        characterCounter.Foreground = new SolidColorBrush(Colors.White);
    }

    private void ScrollToBottom()
    {
      if (!pauseScroll)
      {
        // Scroll the textHistoryScrollViewer to the bottom
        var verticalOffset = textHistoryScrollViewer.ExtentHeight - textHistoryScrollViewer.ViewportHeight;
        textHistoryScrollViewer.ChangeView(null, verticalOffset, null, true);
        textInput.Focus(FocusState.Programmatic);
      }
    }

    private void SendMessage()
    {
      if (textInput.Text == "") return;
      // Send message to Twitch chat if the toggle is on
      try
      {
        if (toggleTwitch.IsChecked.Value && twitchIsConnected)
        {
          SendStreamCaption(textInput.Text);
          twitchClient.SendMessage("#" + currentChannel, textInput.Text);
        }
      }
      catch (TwitchLib.Client.Exceptions.BadStateException ex)
      {
        Debug.WriteLine($"Error sending message: {ex.Message}");
      }

      // Send message as an OSC endpoint if the toggle is on
      if (toggleOsc.IsChecked.Value)
        try
        {
          var txt = textInput.Text;

          object[] args = { textInput.Text, true, audioEnabled };
          var message = new OscMessage("/chatbox/input", args);
          oscSender.Send(message);
        }
        catch (Exception ex)
        {
          // Log the error or display a message to the user
          Debug.WriteLine($"Error sending OSC message: {ex.Message}");
        }

      // Update the text history with the sent message
      UpdateTextHistory(textInput.Text, _broadcasterName);
      ScrollToBottom();

      // Clear the text input
      textInput.Text = "";

      textInput.Focus(FocusState.Programmatic);
    }

    private void OnSuspending(object sender, SuspendingEventArgs e)
    {
      var localSettings = ApplicationData.Current.LocalSettings;
      var storedOAuthOption = (bool)localSettings.Values["RememberOAuth"];

      if (storedOAuthOption == false)
        localSettings.Values["OAuthKey"] = "";
    }

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
      if (e.WindowActivationState == CoreWindowActivationState.CodeActivated || e.WindowActivationState == CoreWindowActivationState.PointerActivated)
      {
        ScrollToBottom();
        textInput.Focus(FocusState.Programmatic);
      }
    }

  }
}