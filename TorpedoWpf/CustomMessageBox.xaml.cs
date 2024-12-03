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

namespace TorpedoWpf
{
    /// <summary>
    /// Interaction logic for CustomMessageBox.xaml
    /// </summary>
    public partial class CustomMessageBox : Window
    {
        public bool Result { get; private set; } // True for Accept, False for Reject

        public CustomMessageBox(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            Result = true; // Accept
            this.DialogResult = true; // Close the dialog
        }

        private void RejectButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false; // Reject
            this.DialogResult = false; // Close the dialog
        }
    }
}
