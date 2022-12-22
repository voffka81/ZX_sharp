using Microsoft.Win32;
using Speccy;
using System.Windows;

namespace ZX_sharp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Computer _speccy;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog() { Filter = "All supported files|*.z80;*.tap;|*.tap snapshots (*.z80)|*.z80|tape file (*.tap)|*.tap" };
            if (openFileDialog.ShowDialog() == true)
                _speccy.TapeInput(openFileDialog.FileName);
            screenImage.Focus();
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            _speccy.Reset();
        }
        private void btnPlayTape_Click(object sender, RoutedEventArgs e)
        {
            _speccy.TapeDevice.Play();
        }
    }
}


