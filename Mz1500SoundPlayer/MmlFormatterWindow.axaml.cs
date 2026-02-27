using Avalonia.Controls;
using System;
using Mz1500SoundPlayer.Sound.Mml;

namespace Mz1500SoundPlayer
{
    public partial class MmlFormatterWindow : Window
    {
        public string? FormattedMml { get; private set; } = null;

        public MmlFormatterWindow()
        {
            InitializeComponent();

            TxtInput.TextChanged += (s, e) => UpdateFormat();
            NumTimeSig.ValueChanged += (s, e) => UpdateFormat();
            DenTimeSig.ValueChanged += (s, e) => UpdateFormat();
            NumUpbeat.ValueChanged += (s, e) => UpdateFormat();
            NumBars.ValueChanged += (s, e) => UpdateFormat();
            ChkSpace.PropertyChanged += (s, e) => 
            {
                if (e.Property.Name == "IsChecked") UpdateFormat();
            };
            ChkMeasureSpace.PropertyChanged += (s, e) => 
            {
                if (e.Property.Name == "IsChecked") UpdateFormat();
            };

            BtnApply.Click += (s, e) => 
            {
                FormattedMml = TxtOutput.Text;
                Close(FormattedMml);
            };
            BtnCancel.Click += (s, e) => Close(null);
        }

        public MmlFormatterWindow(string initialMml) : this()
        {
            TxtInput.Text = initialMml;
            UpdateFormat();
        }

        private void UpdateFormat()
        {
            if (string.IsNullOrEmpty(TxtInput.Text))
            {
                TxtOutput.Text = "";
                return;
            }

            try
            {
                int num = (int)(NumTimeSig.Value ?? 4);
                int den = (int)(DenTimeSig.Value ?? 4);
                
                double upbeatBeats = decimal.ToDouble(NumUpbeat.Value ?? 0);
                double upbeatDurationInWholeNotes = upbeatBeats * (1.0 / den);

                int bars = (int)(NumBars.Value ?? 4);
                bool insertSpace = ChkSpace.IsChecked ?? true;
                bool insertMeasureSpace = ChkMeasureSpace.IsChecked ?? true;

                TxtOutput.Text = MmlFormatter.Format(TxtInput.Text, num, den, upbeatDurationInWholeNotes, bars, insertSpace, insertMeasureSpace);
            }
            catch (Exception ex)
            {
                TxtOutput.Text = "エラーが発生しました: " + ex.Message;
            }
        }
    }
}
