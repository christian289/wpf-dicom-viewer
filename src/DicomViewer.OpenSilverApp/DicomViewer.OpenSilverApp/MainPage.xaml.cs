using System.Windows.Controls;

namespace DicomViewer.OpenSilverApp
{
    public partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            // DataContext를 MainViewModel로 설정
            // Set DataContext to MainViewModel
            this.DataContext = App.MainViewModel;
        }
    }
}
