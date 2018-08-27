using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SpeechTranslator
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            ClientID.Text = Properties.Settings.Default.ClientID;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ClientID = ClientID.Text;
            Properties.Settings.Default.Save();
            this.Close();
        }

        private void Label_MouseDown_Subscription(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("http://aka.ms/translatorazure");
        }

        private void Label_MouseDown_ClientID(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("http://aka.ms/translatorazure");
        }

        private void ObtainSubscription_MouseEnter(object sender, MouseEventArgs e)
        {
            ObtainSubscription.Foreground = Brushes.DarkBlue;
        }

        private void ObtainSubscription_MouseLeave(object sender, MouseEventArgs e)
        {
            ObtainSubscription.Foreground = Brushes.Blue;
        }

        private void ObtainClientID_MouseEnter(object sender, MouseEventArgs e)
        {
            ObtainClientID.Foreground = Brushes.DarkBlue;
        }

        private void ObtainClientID_MouseLeave(object sender, MouseEventArgs e)
        {
            ObtainClientID.Foreground = Brushes.Blue;
        }

    }
}
