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
    /// Interaction logic for DebugWindow.xaml
    /// </summary>
    public partial class DebugWindow : Window
    {
        public static DebugWindow Instance { get; private set; }

        public DebugWindow()
        {
            InitializeComponent();
            Instance = this;
        }

        public void AppendMessage(string message)
        {
            DebugTextBox.AppendText($"{message}\n");
            DebugTextBox.ScrollToEnd();
        }
    }
}
