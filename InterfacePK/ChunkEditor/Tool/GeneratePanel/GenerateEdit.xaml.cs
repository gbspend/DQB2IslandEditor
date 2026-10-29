using DQB2IslandEditor.DataPK;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;

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
            NewSeed();
        }

        private void PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(viewModel.GenerateSelection)) return;
            //Measured once per selection, so generating again with new settings starts from the
            //original ground rather than the previous mountain.
            (groundLayer, highestGround) = viewModel.GenerateGroundRange();
            UpdateHint();
            SuggestMaxLayer();
            UpdateMaxLayerWarning();
        }

        private byte groundLayer = ChunkEditorViewModel.GENERATE_MIN_LAYER;   //Lowest ground: the mountain's base.
        private byte highestGround = ChunkEditorViewModel.GENERATE_MIN_LAYER;

        private void MaxLayerChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e) => UpdateMaxLayerWarning();

        //Existing ground taller than the mountain will stick out of it.
        private void UpdateMaxLayerWarning()
        {
            if (MaxLayerWarning == null) return; //Slider fires before the rest of the panel is built.
            bool below = viewModel.HasGenerateSelection && MaxLayerSlider.Value < highestGround;
            MaxLayerWarning.Visibility = below ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            MaxLayerWarning.Text = "Below highest ground (layer " + highestGround + ")";
        }

        //Starting max layer for a new selection: about 10 layers above the ground per chunk across its
        //short side, so the slopes stay gentle on narrow selections. At least a little above sea level so
        //there's some land, and at most the usual 60. Can still be changed after.
        private const int LAYERS_PER_CHUNK = 10;
        private const int SUGGESTED_MAX_LAYER = 60;
        private const int SEA_LEVEL = 30;
        private const int MIN_ABOVE_SEA = 5;
        private void SuggestMaxLayer()
        {
            var selection = viewModel.GenerateSelection;
            if (selection.Count == 0) return;
            int width = selection.Max(c => c % Island.GRID_DIMENSION) - selection.Min(c => c % Island.GRID_DIMENSION) + 1;
            int depth = selection.Max(c => c / Island.GRID_DIMENSION) - selection.Min(c => c / Island.GRID_DIMENSION) + 1;
            int shortSide = Math.Min(width, depth);
            int suggested = groundLayer + LAYERS_PER_CHUNK * shortSide;
            suggested = Math.Max(suggested, SEA_LEVEL + MIN_ABOVE_SEA);
            suggested = Math.Min(suggested, Math.Max(SUGGESTED_MAX_LAYER, groundLayer + LAYERS_PER_CHUNK));
            MaxLayerSlider.Value = Math.Clamp(suggested, MaxLayerSlider.Minimum, MaxLayerSlider.Maximum);
        }

        private void GenerateClick(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!int.TryParse(SeedBox.Text, out int seed))
            {
                NewSeed(); //Empty or too big, so pick one.
                seed = int.Parse(SeedBox.Text);
            }
            viewModel.Generate(new MountainParams(
                BaseLayer: groundLayer,
                MaxLayer: (byte)MaxLayerSlider.Value,
                CoveragePercent: (int)CoverageSlider.Value,
                PlateauPercent: (int)PlateauSlider.Value,
                SecondPlateau: SecondPlateauCheck.IsChecked == true,
                SecondHeightPercent: (int)SecondHeightSlider.Value,
                RoughnessPercent: (int)RoughnessSlider.Value,
                Seed: seed));
        }

        private void RandomSeedClick(object sender, System.Windows.RoutedEventArgs e) => NewSeed();

        private void NewSeed() => SeedBox.Text = Random.Shared.Next(100000).ToString();

        //Digits only.
        private void SeedTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void UpdateHint()
        {
            int count = viewModel.GenerateSelection.Count;
            HintText.Text = count == 0
                ? "Click and drag on the minimap to paint the chunks to generate in. Right-click a chunk to view it."
                : count + (count == 1 ? " chunk" : " chunks") + " selected, ground from layer " + groundLayer + " to " + highestGround
                  + ". Drag again to replace the selection.";
        }
    }
}
