using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using CoreOSC;
using TwitchLib.Client.Models;
using Windows.UI.Xaml.Media.Imaging;
using System.Collections.ObjectModel;
using Windows.UI.Xaml.Documents;
using System.Collections.Generic;
using System.Threading;
using System;
using System.Diagnostics;
using Windows.Storage;
using System.Linq;
using Windows.ApplicationModel;
using Windows.UI;
using Windows.Security.Credentials;
using System.Threading.Tasks;

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
    private bool isSendingMessage;
    private bool messageSentByApp;

    private UDPSender oscSender;
    private bool pauseScroll;
    private string NutButtonText = "";

    private Dictionary<string, BitmapImage> _emoteCache = new Dictionary<string, BitmapImage>();

    public MainPage()
    {
      InitializeComponent();
      var localSettings = ApplicationData.Current.LocalSettings;

      bool firstTime = true;
      if (localSettings.Values.TryGetValue("FirstTime", out object firstTimeOption))
        firstTime = (bool)firstTimeOption;

      if (localSettings.Values.TryGetValue("NutButton", out object nutButtonOption))
        NutButtonText = (string)nutButtonOption;
      if (NutButtonText == "")
        NutButtonText = "Nut";

      if (firstTime) InitPasswordVault();

      Loaded += MainPage_Loaded;

      toggleTyping.UpdateButtonColor();

      twitchIsConnected = false;
      typingTimer = new DispatcherTimer();
      typingTimer.Interval = TimeSpan.FromSeconds(1); // Set the interval to 1 second, or change it to the desired delay
      typingTimer.Tick += TypingTimer_Tick;

      // Add event handlers for the send button and return key
      sendButton.Click += SendButton_Click;

      makeClip.Click                += makeClip_Click;
      oscTriggers.Click             += OscTriggers_Click;
      gButton.Click                 += gButton_Click;
      textInput.KeyDown             += TextInput_KeyUp;
      clearInputButton.Click        += ClearInputButton_Click;
      clearOscEndpointButton.Click  += ClearOscEndpointButton_Click;

      Application.Current.Suspending += new SuspendingEventHandler(OnSuspending);
      Window.Current.Activated += OnActivated;
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
      try
      {
        var localSettings = ApplicationData.Current.LocalSettings;
        InitializeOsc();
        InitializeObs();

        if (localSettings.Values.TryGetValue("AutoConnectTwitch", out object connectOption))
          twitchAutoConnect = (bool)connectOption;
        if (localSettings.Values.TryGetValue("RememberOAuth", out object oauthOption))
          twitchStoreAuth = (bool)oauthOption;
        if (localSettings.Values.TryGetValue("AutoConnectOBS", out object obsConnectOption))
          obsAutoConnect = (bool)obsConnectOption;

        if (obsAutoConnect)
        {
          string OBSAddress = "127.0.0.1";
          string OBSPort = "4455";
          //PasswordVault vault = new PasswordVault();
          //string OBSPassword  = GetOBSPassword(vault);
          bool? SSLOption = false;

          if (localSettings.Values.TryGetValue("OBSAddress", out object obsAddress) && !string.IsNullOrEmpty(obsAddress as string))
            OBSAddress = obsAddress as string;

          if (localSettings.Values.TryGetValue("OBSPort", out object obsPort) && !string.IsNullOrEmpty(obsPort as string))
            OBSPort = obsPort as string;

          if (localSettings.Values.TryGetValue("SSLOption", out object useSSLOption) && useSSLOption != null)
            SSLOption = (bool)useSSLOption;

          OBSAddress = $"{(SSLOption ?? false ? "wss" : "ws")}://{OBSAddress}:{OBSPort}/";
          await OBSConnect(OBSAddress);
        }
        if (twitchAutoConnect && twitchStoreAuth)  // auto connect enabled
        {
          await InitializeTwitchClient();
          initTwitchButton.Content = "Disconnect TTV";
        }
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"MainPage_Loaded exception: {ex.Message}");
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
          args.RegisterUpdateCallback(1, async (s, e) => await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => LoadChatItem(container, chatItem, e)));
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

    private async Task UpdateTextHistory(string message, string username = "", IList<Emote> emotes = null)
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
        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        {
          textHistory.Items.Add(chatItem);
          textHistory.UpdateLayout();
        });
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"Error adding chat message: {ex.Message}");
      }
    }

    private async Task UpdateCharacterCounter()
    {
      var charactersRemaining = MaxCharacters - textInput.Text.Length;

      await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
      {
        characterCounter.Text = $"{charactersRemaining}/{MaxCharacters}";

        if (charactersRemaining <= MaxCharacters * 0.15)
          characterCounter.Foreground = new SolidColorBrush(Colors.Red);
        else
          characterCounter.Foreground = new SolidColorBrush(Colors.White);
      });
    }

    private async Task ScrollToBottom()
    {
      if (!pauseScroll)
      {
        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        {
          textHistory.ScrollIntoView(textHistory.Items.LastOrDefault());
          textInput.Focus(FocusState.Programmatic);
        });
      }
    }

    private async Task SendMessage()
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
      await UpdateTextHistory(textInput.Text, twitchBroadcasterName);
      await ScrollToBottom();

      // Clear the text input
      await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        {
          textInput.Text = "";
          textInput.Focus(FocusState.Programmatic);
        });
    }

    private void OnSuspending(object sender, SuspendingEventArgs e)
    {
      var localSettings = ApplicationData.Current.LocalSettings;
      var storedOAuthOption = false;

      if (localSettings.Values.TryGetValue("RememberOAuth", out object OAuthOption))
         storedOAuthOption = (bool)OAuthOption;

      if (storedOAuthOption == false)
        localSettings.Values["OAuthKey"] = "";
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs e)
    {
      if (e.WindowActivationState == CoreWindowActivationState.CodeActivated || e.WindowActivationState == CoreWindowActivationState.PointerActivated)
      {
        await ScrollToBottom();
        textInput.Focus(FocusState.Programmatic);
      }
    }

    private string GetCredential(PasswordVault vault, string CredentialName)
    {
      try
      {
        // Retrieve the BroadcasterName
        PasswordCredential cred = vault.Retrieve("CatResource", CredentialName);
        cred.RetrievePassword();

        if(cred.Password == "none")
          return null;
        return cred.Password;
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"{ex.Message}");
        return null; // or assign a default value, or prompt user input
      }
    }

    private void ClearCredential(PasswordVault vault, string CredentialName)
    {
      PasswordCredential cred = new PasswordCredential(
              "CatResource", // Resource for which OAuth is saved
              CredentialName, // UserName, acting as key to retrieve the password
              "none"); // Password or the actual OAuth Key
      vault.Add(cred);
    }
    
    private void SetCredential(PasswordVault vault, string CredentialName, string CredentialValue)
    {
      PasswordCredential cred = new PasswordCredential(
        "CatResource",
        CredentialName,
        CredentialValue);
      vault.Add(cred);
    }
    
    private void InitPasswordVault()
    {
      PasswordVault vault = new PasswordVault();

      PasswordCredential oauthCredential = new PasswordCredential(
              "CatResource", // Resource for which OAuth is saved
              "OAuthKey", // UserName, acting as key to retrieve the password
              "none"); // Password or the actual OAuth Key
          vault.Add(oauthCredential);

      PasswordCredential broadcasterCredential = new PasswordCredential(
              "CatResource", // Resource for which BroadcasterName is saved
              "BroadcasterName", // UserName, acting as key to retrieve the password
              "none"); // Password or the actual Broadcaster Name
          vault.Add(broadcasterCredential);

      PasswordCredential obsCredential = new PasswordCredential(
              "CatResource", // Resource for which OAuth is saved
              "OBSPassword", // UserName, acting as key to retrieve the password
              "none"); // Password or the actual OAuth Key
          vault.Add(obsCredential);

      
      var localSettings = ApplicationData.Current.LocalSettings;
      localSettings.Values["FirstTime"] = false;
    }
  }
}