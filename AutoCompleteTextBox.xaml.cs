using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Store.Preview.InstallControl;
using TwitchLib.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Client;

namespace VRCatNet
{
    public sealed partial class AutoCompleteTextBox : UserControl
    {
        private bool tabPressedOnce = false;


        public ObservableCollection<SuggestionItem> Suggestions { get; set; }

        public static readonly DependencyProperty MaxCharactersProperty =
            DependencyProperty.Register("MaxCharacters", typeof(int), typeof(MainPage), new PropertyMetadata(500));

        public AutoCompleteTextBox()
        {
            this.InitializeComponent();
            Suggestions = new ObservableCollection<SuggestionItem>();
            textBox.TextChanged += TextBox_TextChanged;
            textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
            suggestionsListView.Tapped += SuggestionsListView_Tapped;
        }

        public int MaxCharacters
        {
            get => (int)GetValue(MaxCharactersProperty);
            set => SetValue(MaxCharactersProperty, value);
        }

        private void SuggestionsListView_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Perform auto-completion with the clicked suggestion
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Implement your custom search algorithm here
            // Update the Suggestions collection with new suggestions

            // Sort the suggestions based on usage count
            SortSuggestions();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Tab)
            {
                e.Handled = true;

                if (tabPressedOnce)
                {
                    // Perform auto-completion with the top suggestion item
                    var topSuggestion = Suggestions.FirstOrDefault();
                    if (topSuggestion != null)
                    {
                        // Increase the usage count and update the TextBox
                        topSuggestion.UsageCount++;
                        textBox.Text = topSuggestion.Text;
                        textBox.SelectionStart = textBox.Text.Length;
                    }
                    tabPressedOnce = false;
                }
                else
                {
                    tabPressedOnce = true;
                }
            }
            else
            {
                tabPressedOnce = false;
            }
        }

        private void SortSuggestions()
        {
            var sortedSuggestions = Suggestions.OrderByDescending(s => s.UsageCount).ToList();
            Suggestions.Clear();

            foreach (var suggestion in sortedSuggestions)
            {
                Suggestions.Add(suggestion);
            }
        }

        private BitmapImage LoadImageFromUrl(string url)
        {
            var image = new BitmapImage();
            image.UriSource = new Uri(url);
            return image;
        }

    }
    public class EmoteCache
    {
        public DateTime LastUpdated { get; private set; }
        //public List<Emote> Emotes { get; private set; }

        public EmoteCache()
        {
            //Emotes = new List<Emote>();
        }

        public async Task UpdateEmotesAsync(TwitchAPI api, string userId, string channelId)
        {
            // Fetch the user's emotes
            var userResponse= await api.Helix.Users.GetUsersAsync(ids: new List<string> { userId }); //api.V5.Users.GetUserEmotesAsync(userId);

            // Fetch the channel's emotes
            //var channelEmotes = await api.V5.Chat.GetChannelEmotesAsync(channelId);

            // Merge and store the fetched emotes
            //Emotes.Clear();
            //Emotes.AddRange(userEmotes.Emoticons);
            //Emotes.AddRange(channelEmotes);

            // Update the last updated timestamp
            LastUpdated = DateTime.UtcNow;
        }

        public bool NeedsUpdate()
        {
            // Check if the cache needs to be updated (e.g., after 1 hour)
            return (DateTime.UtcNow - LastUpdated).TotalHours >= 1;
        }
    }

    public class SuggestionItem
    {
        public string Text { get; set; }
        public ImageSource Emoji { get; set; }
        public SuggestionItemType ItemType { get; set; }
        public int UsageCount { get; set; }
    }

    public enum SuggestionItemType
    {
        Username,
        Emoji
    }

    public class ItemTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var itemType = (SuggestionItemType)value;
            var desiredItemType = (SuggestionItemType)Enum.Parse(typeof(SuggestionItemType), (string)parameter);

            return itemType == desiredItemType ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class TrieNode
    {
        public Dictionary<char, TrieNode> Children { get; set; }
        public bool IsEndOfWord { get; set; }

        public TrieNode()
        {
            Children = new Dictionary<char, TrieNode>();
            IsEndOfWord = false;
        }
    }

    public class Trie
    {
        private TrieNode _root;

        public Trie()
        {
            _root = new TrieNode();
        }

        public void Insert(string word)
        {
            TrieNode node = _root;
            foreach (char c in word)
            {
                if (!node.Children.ContainsKey(c))
                {
                    node.Children[c] = new TrieNode();
                }
                node = node.Children[c];
            }
            node.IsEndOfWord = true;
        }

        public List<string> AutoComplete(string prefix, bool reverse = false)
        {
            List<string> results = new List<string>();
            TrieNode node = _root;

            foreach (char c in prefix)
            {
                if (!node.Children.ContainsKey(c))
                {
                    return results;
                }
                node = node.Children[c];
            }

            if (reverse)
            {
                prefix = new string(prefix.Reverse().ToArray());
            }

            DFS(node, prefix, results, reverse);
            return results;
        }

        private void DFS(TrieNode node, string prefix, List<string> results, bool reverse)
        {
            if (node.IsEndOfWord)
            {
                if (reverse)
                {
                    results.Add(new string(prefix.Reverse().ToArray()));
                }
                else
                {
                    results.Add(prefix);
                }
            }

            foreach (var child in node.Children)
            {
                DFS(child.Value, prefix + child.Key, results, reverse);
            }
        }
    }

}
