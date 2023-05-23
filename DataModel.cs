using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace VRCatNet
{
    public class ChatItem : INotifyPropertyChanged
    {
        private ObservableCollection<ChatElement> _chatElements;

        public ObservableCollection<ChatElement> ChatElements
        {
            get => _chatElements;
            set
            {
                _chatElements = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ChatElement : INotifyPropertyChanged
    {
        private BitmapImage _emoteImage;

        private bool _isEmote;

        private string _text;

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                OnPropertyChanged();
            }
        }

        public BitmapImage EmoteImage
        {
            get => _emoteImage;
            set
            {
                _emoteImage = value;
                OnPropertyChanged();
            }
        }

        public bool IsEmote
        {
            get => _isEmote;
            set
            {
                _isEmote = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool booleanValue) return booleanValue ? Visibility.Visible : Visibility.Collapsed;

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Visibility visibilityValue) return visibilityValue == Visibility.Visible;

            return false;
        }
    }

    public class CustomToggleButton : ToggleButton
    {
        public CustomToggleButton()
        {
            DefaultStyleKey = typeof(CustomToggleButton);
            UpdateButtonColor();
            Checked += CustomToggleButton_Checked;
            Unchecked += CustomToggleButton_Unchecked;
        }

        private void CustomToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            UpdateButtonColor();
        }

        private void CustomToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateButtonColor();
        }

        public void UpdateButtonColor()
        {
            if (IsChecked == true)
                Background = new SolidColorBrush(Colors.Blue);
            else
                Background = new SolidColorBrush(Colors.DarkMagenta);
        }

        public void SetTypingColor(bool isTyping)
        {
            if (IsChecked == true)
                Background = isTyping ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Blue);
            else
                Background = new SolidColorBrush(Colors.DarkMagenta);
        }
    }
}