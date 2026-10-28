using System.ComponentModel;
using System.Windows.Controls;

namespace DQB2IslandEditor.InterfacePK.ChunkEditor.Tool.GeneratePanel
{
    /// <summary>
    /// Generation parameters and the Generate button, for the chunks painted green on the minimap.
    /// </summary>
    public partial class GenerateEdit : UserControl
    {
        private ChunkEditorViewModel viewModel;

        public GenerateEdit(ChunkEditorViewModel viewModel)
        {
            this.viewModel = viewModel;
            InitializeComponent();
            DataContext = viewModel;
            viewModel.PropertyChanged += PropertyChanged;
            UpdateHint();
        }

        private void PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(viewModel.GenerateSelection)) UpdateHint();
        }

        private void UpdateHint()
        {
            int count = viewModel.GenerateSelection.Count;
            HintText.Text = count == 0
                ? "Click and drag on the minimap to paint the chunks to generate in. Right-click a chunk to view it."
                : count + (count == 1 ? " chunk" : " chunks") + " selected. Drag again to replace the selection.";
        }
    }
}
