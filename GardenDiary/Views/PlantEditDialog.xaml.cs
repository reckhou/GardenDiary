using System.Windows;
using System.Windows.Controls;
using GardenDiary.Models;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace GardenDiary.Views;

public partial class PlantEditDialog : Window
{
    private static readonly string[] CommonEmojis =
    [
        // Flowers
        "🌹", "🌻", "🌷", "🌺", "🌸", "💐", "🌼", "💮", "🏵",
        // Plants & leaves
        "🌱", "🌿", "☘", "🍀", "🪴", "🎋", "🎍", "🌾",
        // Trees
        "🌳", "🌲", "🌴", "🎄", "🪵",
        // Fruits
        "🍎", "🍐", "🍊", "🍋", "🍇", "🍓", "🫐", "🍒", "🍑", "🥭", "🍍", "🥝", "🍈", "🍉", "🥥",
        // Vegetables
        "🍅", "🥕", "🌽", "🌶", "🫑", "🥒", "🥬", "🥦", "🧅", "🧄", "🥔", "🍆", "🫛",
        // Herbs & misc
        "🌰", "🥜", "🍄", "🌵", "🎃"
    ];

    public Plant Plant { get; private set; }

    public PlantEditDialog(Plant plant)
    {
        InitializeComponent();
        Plant = plant;
        TxtCommonName.Text = plant.CommonName;
        TxtLatinName.Text = plant.LatinName;
        TxtVariety.Text = plant.Variety;
        TxtSelectedEmoji.Text = plant.Emoji;

        BuildEmojiPicker();
        Loaded += (_, _) => TxtCommonName.Focus();
    }

    private void BuildEmojiPicker()
    {
        foreach (var emoji in CommonEmojis)
        {
            var btn = new Button
            {
                Content    = new Emoji.Wpf.TextBlock { Text = emoji, FontSize = 16 },
                Width      = 32,
                Height     = 32,
                Margin     = new Thickness(1),
                Padding    = new Thickness(0),
                Cursor     = System.Windows.Input.Cursors.Hand,
                ToolTip    = emoji,
                Tag        = emoji
            };
            btn.Click += EmojiButton_Click;
            EmojiPanel.Children.Add(btn);
        }
    }

    private void EmojiButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string emoji)
            TxtSelectedEmoji.Text = emoji;
    }

    private void ClearEmoji_Click(object sender, RoutedEventArgs e)
    {
        TxtSelectedEmoji.Text = "";
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtCommonName.Text))
        {
            MessageBox.Show("Common Name is required.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCommonName.Focus();
            return;
        }
        Plant.CommonName = TxtCommonName.Text.Trim();
        Plant.LatinName = TxtLatinName.Text.Trim();
        Plant.Variety = TxtVariety.Text.Trim();
        Plant.Emoji = TxtSelectedEmoji.Text.Trim();
        DialogResult = true;
    }
}
