using System;
using System.Windows;

namespace SpeechTranslator
{
    /// <summary>
    /// Interaction logic for MiniWindow.xaml
    /// </summary>
    public partial class MiniWindow : Window
    {
        private int _NoOfLines;

        public MiniWindow()
        {
            InitializeComponent();
            SizeChanged += MiniWindow_SizeChanged;
            Closing += MiniWindow_Closing;

            // Restore the window size and position
            Height = (Properties.Settings.Default.MiniWindow_Height > 5) ? Properties.Settings.Default.MiniWindow_Height : 100;
            Width = (Properties.Settings.Default.MiniWindow_Width > 5) ? Properties.Settings.Default.MiniWindow_Width : 400;
            Left = Properties.Settings.Default.MiniWindow_Left;
            Top = Properties.Settings.Default.MiniWindow_Top;
            SetFontSize(Properties.Settings.Default.MiniWindow_Lines);
        }

        private void MiniWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save the window size and position
            Properties.Settings.Default.MiniWindow_Height = (int)Height;
            Properties.Settings.Default.MiniWindow_Width = (int)Width;
            Properties.Settings.Default.MiniWindow_Left = (int)Left;
            Properties.Settings.Default.MiniWindow_Top = (int)Top;
        }

        private void MiniWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetFontSize(_NoOfLines);
        }

        /// <summary>
        /// Sets the font size so that NoOfLines+1 lines fit into the Mini Window
        /// </summary>
        /// <param name="NoOfLines">Number of lines (index 0)</param>
        public void SetFontSize(int NoOfLines)
        {
            _NoOfLines = NoOfLines;
            DisplayText.FontSize = (DisplayText.ActualHeight > 10) ? (DisplayText.ActualHeight / (NoOfLines + 1) * 0.73) : 8;
        }
    }
}
