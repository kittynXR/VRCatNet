using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace VRCatNet
{
    public sealed partial class AutoCompleteTextBox : UserControl
    {
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

            // Clear the existing suggestions
            Suggestions.Clear();

            // Add new suggestions
            //foreach (var suggestion in newSuggestions)
            //{
            //    Suggestions.Add(new SuggestionItem
            //    {
            //        Username = suggestion.Username,
            //        EmoteImage = LoadImageFromUrl(suggestion.EmoteImageUrl)
            //    });
            //}
        }

        private BitmapImage LoadImageFromUrl(string url)
        {
            var image = new BitmapImage();
            image.UriSource = new Uri(url);
            return image;
        }


        private void TextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Tab)
            {
                // Perform auto-completion with the selected suggestion
                e.Handled = true;
            }
        }
    }

    public class SuggestionItem
    {
        public string Username { get; set; }
        public BitmapImage EmoteImage { get; set; }
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
