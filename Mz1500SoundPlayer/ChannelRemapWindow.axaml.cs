using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System.Linq;

namespace Mz1500SoundPlayer;

public partial class ChannelRemapWindow : Window
{
    // A dictionary to store the user's remapping choices.
    // Key: Original Channel (A-H, P)
    // Value: Target Channel (A-H, P)
    public Dictionary<string, string> ChannelMap { get; private set; }
    private HashSet<string> _usedChannels;

    public ChannelRemapWindow() : this(new HashSet<string>())
    {
    }

    public ChannelRemapWindow(HashSet<string> usedChannels)
    {
        InitializeComponent();
        ChannelMap = new Dictionary<string, string>();
        _usedChannels = usedChannels ?? new HashSet<string>();
        
        string[] channels = { "A", "B", "C", "D", "E", "F", "G", "H", "P" };
        ComboBox[] combos = { CmbA, CmbB, CmbC, CmbD, CmbE, CmbF, CmbG, CmbH, CmbP };
        
        for (int i = 0; i < combos.Length; i++)
        {
            var combo = combos[i];
            string chName = channels[i];

            foreach (var ch in channels)
            {
                combo.Items.Add(ch);
            }
            combo.SelectedIndex = i; // Default to self

            if (!_usedChannels.Contains(chName))
            {
                combo.IsEnabled = false;
                combo.Opacity = 0.5;
            }
        }
    }

    private void ApplyButton_Click(object? sender, RoutedEventArgs e)
    {
        // 1. Gather all selections
        string[] channels = { "A", "B", "C", "D", "E", "F", "G", "H", "P" };
        ComboBox[] combos = { CmbA, CmbB, CmbC, CmbD, CmbE, CmbF, CmbG, CmbH, CmbP };
        
        ChannelMap.Clear();
        var targetSet = new HashSet<string>();
        var duplicates = new HashSet<string>();

        for (int i = 0; i < combos.Length; i++)
        {
            string original = channels[i];
            string target = combos[i].SelectedItem as string ?? original;
            ChannelMap[original] = target;

            // Only consider duplicates for channels that actually have data
            if (_usedChannels.Contains(original))
            {
                if (!targetSet.Add(target))
                {
                    duplicates.Add(target);
                }
            }
        }

        // 2. Validation
        if (duplicates.Any())
        {
            ErrorMessage.Text = $"エラー: 変換先に重複があります ({string.Join(", ", duplicates)})。合流すると元に戻せなくなるため、重複しないように指定してください。";
            return;
        }

        // 3. Clear error and close with Result = map
        ErrorMessage.Text = "";
        Close(ChannelMap);
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(null); // Return null to indicate cancellation
    }
}
