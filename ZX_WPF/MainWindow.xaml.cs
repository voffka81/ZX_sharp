using Microsoft.Win32;
using Speccy;
using System.Threading.Tasks;
using System.Windows;

namespace ZX_sharp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Computer _spectrum;

        public MainWindow()
        {
            InitializeComponent();

            _spectrum = new Computer();

            Task.Run(() =>
            {
                using (var game = new MonoSpectrum(_spectrum))
                {
                    game.Run();
                }
            });
        }

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog() { Filter = "snapshots (*.z80)|*.z80" };
            if (openFileDialog.ShowDialog() == true)
                _spectrum.TapeInput(openFileDialog.FileName);
        }
    }
}


