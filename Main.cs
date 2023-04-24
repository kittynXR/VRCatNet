﻿using Windows.UI.Core;
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
using Windows.UI;

namespace VRCatNet
{

    public sealed partial class MainPage : Page
    {
        private int _oscPort = 9000;
        private string _oscIP = "127.0.0.1";

        public static readonly DependencyProperty MaxCharactersProperty =
            DependencyProperty.Register("MaxCharacters", typeof(int), typeof(MainPage), new PropertyMetadata(500));

        private readonly DispatcherTimer typingTimer;

        private readonly SemaphoreSlim uiSemaphore = new SemaphoreSlim(1, 1);
        private bool audioEnabled;
        private bool twitchIsConnected;
        private bool isSendingMessage;
        private bool messageSentByApp;

        private UDPSender oscSender;
        private bool pauseScroll;

        private string storedBroadcasterName;
        private TwitchClient twitchClient;

        private Dictionary<string, BitmapImage> _emoteCache = new Dictionary<string, BitmapImage>();

        public MainPage()
        {
            InitializeComponent();
            InitializeOsc();
            toggleTyping.UpdateButtonColor();

            twitchIsConnected = false;
            typingTimer = new DispatcherTimer();
            typingTimer.Interval =
                TimeSpan.FromSeconds(1); // Set the interval to 1 second, or change it to the desired delay
            typingTimer.Tick += TypingTimer_Tick;

            // Add event handlers for the send button and return key
            sendButton.Click += SendButton_Click;
            textInput.KeyDown += TextInput_KeyUp;
            clearInputButton.Click += ClearInputButton_Click;
            clearOscEndpointButton.Click += ClearOscEndpointButton_Click;

            UpdateCharacterCounter();
        }

        public int MaxCharacters
        {
            get => (int)GetValue(MaxCharactersProperty);
            set => SetValue(MaxCharactersProperty, value);
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeTwitchClient();
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
            }
        }

        private void SendMessage()
        {
            if (textInput.Text == "") return;
            // Send message to Twitch chat if the toggle is on
            try
            {
                if (toggleTwitch.IsChecked.Value && twitchIsConnected)
                    twitchClient.SendMessage("#" + storedBroadcasterName, textInput.Text);
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
            UpdateTextHistory(textInput.Text, storedBroadcasterName);
            ScrollToBottom();

            // Clear the text input
            textInput.Text = "";

            textInput.Focus(FocusState.Programmatic);
        }

    }
}