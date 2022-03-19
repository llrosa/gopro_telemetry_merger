using DashwareFileMerger;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DashwareFileMerger
{
    public class Data
    {
        public string FolderPath { get; set; } = "";
        public string Console { get; set; } = "";

    }


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Data data = new Data();

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += new RoutedEventHandler(Page_Loaded);
        }

        public void UpdateUI()
        {
            Data s = new Data();
            {
                s.FolderPath = data.FolderPath;
                s.Console = data.Console;
                };

            this.Grid.DataContext = s;
        }

        void Page_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateUI();
        }

        private void btnExecute_Click(object sender, RoutedEventArgs e)
        {

            if (String.IsNullOrWhiteSpace(txtInputPath.Text))
            {
                txtOutput.Text += "Error! Path must be specified\n";
                return;
            }

            txtOutput.Text = "";
            try
            {
                var m = new Merger();
                m.Run(new List<string>() { txtInputPath.Text });
            }
            catch (Exception ex)
            {
                txtOutput.Text += ex.Message+"\n";
            }
            txtOutput.Text += "Done\n";

        }

        private void txtInputPath_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void txtInputPath_PreviewDrop(object sender, DragEventArgs e)
        {
            SetPath(sender, e);
        }

        private void txtInputPath_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private void Grid_DragLeave(object sender, DragEventArgs e)
        {
            SetPath(sender, e);
        }

        private void SetPath(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                txtInputPath.Text = files[0];
            }
        }
    }
}
