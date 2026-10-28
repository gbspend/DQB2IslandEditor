using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DQB2IslandEditor.InterfacePK.ChunkEditor.Tool.BlockPanel
{
    /// <summary>
    /// Interaction logic for BlockValues.xaml
    /// </summary>
    public partial class OverflowEdit : UserControl
    {
        private ChunkEditorViewModel viewModel;
        //Holds its own view model (like ChiselEdit): the inherited DataContext goes null when the panel is removed from the tool menu.
        public OverflowEdit(ChunkEditorViewModel viewModel)
        {
            this.viewModel = viewModel;
            InitializeComponent();
            DataContext = viewModel;

            if (viewModel.SelectedBlock == null)
            {
                this.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.Visibility = Visibility.Visible;
            }
            viewModel.PropertyChanged += PropertyChanged;
        }

        private void PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(viewModel.SelectedBlock))  // BLOCKINFO CHANGE
            {
                if (viewModel.SelectedBlock == null)
                {
                    this.Visibility = Visibility.Collapsed;
                }
                else
                {
                    this.Visibility = Visibility.Visible;
                }
            }
        }
    }
}
