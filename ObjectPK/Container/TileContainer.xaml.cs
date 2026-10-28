using DQB2IslandEditor.DataPK;
using DQB2IslandEditor.InterfacePK;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DQB2IslandEditor.ObjectPK.Container
{
    /// <summary>
    /// Interaction logic for TileContainer.xaml
    /// </summary>
    public partial class TileContainer : UserControl
    {
        public BlockInstance blockInstance
        {
            get { return _blockInstance; }
            set
            { if (_blockInstance != null && _blockInstance.publicBlockID == value.publicBlockID) return; //Try to reduce cycles?
              _blockInstance = value;
              blockInfo.Value = DataBaseReading.BLOCK_INFO_DICTIONARY[_blockInstance.publicBlockID];}
        }

        private BlockInstance _blockInstance;

        public ObservableProperty<BlockInfo> blockInfo { get; set; } = new ObservableProperty<BlockInfo> { };

        //See-through view: the block shown under an air/water tile (null when not showing one).
        public ObservableProperty<BlockInfo> ghostInfo { get; set; } = new ObservableProperty<BlockInfo> { };
        //Water seen below an air tile, drawn translucent over ghostInfo (null when none).
        public ObservableProperty<BlockInfo> ghostWaterInfo { get; set; } = new ObservableProperty<BlockInfo> { };
        private bool _ghostShown;
        private uint? _ghostBlockID;
        private byte _ghostDepth;
        private uint? _ghostWaterID;
        private byte _ghostWaterDepth;
        private bool _ghostUnderWater;

        //How opaque water is drawn when there's something visible beneath it.
        private const double WATER_OPACITY = 0.5;
        //Opacity of Image when not hovered/clicked (water on the current layer is see-through).
        private double _restingImageOpacity = 1;

        public ushort offset;
        public TileContainer(ushort offset, Action<ushort>? enter, Action<ushort>? leave, Action<ushort>? click, Action<ushort>? release)
        {
            InitializeComponent();
            DataContext = this;
            this.offset = offset;

            Tile.PreviewMouseDown += (_, _) => { click(offset); };
            Tile.MouseEnter += (_, _) => { enter(offset); };
            Tile.MouseLeave += (_, _) => { leave(offset); };
            Tile.MouseLeftButtonUp += (_, _) => { release(offset); };
        }

        //Shows a block from a lower layer under this tile, darker the deeper it is.
        //depth = how many layers below the current one the block is (1 = directly below).
        //blockID can be null if there's only water below (no floor).
        //waterID/waterDepth = topmost water between this tile and the block, if this tile is air.
        //If this tile is itself water, it's drawn translucent over the ghost instead of hidden.
        public void SetGhost(uint? blockID, byte depth, uint? waterID = null, byte waterDepth = 0)
        {
            bool underWater = blockInfo.Value?.isWater == true;
            if (_ghostShown && _ghostBlockID == blockID && _ghostDepth == depth
                && _ghostWaterID == waterID && _ghostWaterDepth == waterDepth
                && _ghostUnderWater == underWater) return; //Nothing changed.

            //Unknown block IDs: don't crash the view over them.
            BlockInfo info = null, waterInfo = null;
            if (blockID != null && !DataBaseReading.BLOCK_INFO_DICTIONARY.TryGetValue(blockID.Value, out info)) blockID = null;
            if (waterID != null && !DataBaseReading.BLOCK_INFO_DICTIONARY.TryGetValue(waterID.Value, out waterInfo)) waterID = null;
            if (blockID == null && waterID == null)
            {
                ClearGhost();
                return;
            }

            _ghostShown = true;
            _ghostBlockID = blockID;
            _ghostDepth = depth;
            _ghostWaterID = waterID;
            _ghostWaterDepth = waterDepth;
            _ghostUnderWater = underWater;

            ghostInfo.Value = info;
            GhostShade.Visibility = info != null ? Visibility.Visible : Visibility.Collapsed;
            GhostShade.Opacity = ShadeForDepth(depth);

            ghostWaterInfo.Value = waterInfo;
            GhostWater.Visibility = waterInfo != null ? Visibility.Visible : Visibility.Collapsed;
            //Over a floor the water is translucent, with nothing under it there's nothing to see through to.
            GhostWater.Opacity = info != null ? WATER_OPACITY : 1;
            GhostWaterShade.Opacity = ShadeForDepth(waterDepth);

            Ghost.Visibility = Visibility.Visible;
            if (underWater)
            {
                Image.Visibility = Visibility.Visible;
                SetRestingImageOpacity(info != null ? WATER_OPACITY : 1);
            }
            else
            {
                Image.Visibility = Visibility.Hidden;
                SetRestingImageOpacity(1);
            }
        }

        public void ClearGhost()
        {
            if (!_ghostShown) return;
            _ghostShown = false;
            _ghostBlockID = null;
            _ghostWaterID = null;
            ghostInfo.Value = null;
            ghostWaterInfo.Value = null;
            Ghost.Visibility = Visibility.Collapsed;
            Image.Visibility = Visibility.Visible;
            SetRestingImageOpacity(1);
        }

        private void SetRestingImageOpacity(double opacity)
        {
            _restingImageOpacity = opacity;
            if (Tile.Background == Brushes.Transparent) Image.Opacity = opacity; //Don't undo hover/click highlight.
        }

        //1 layer down is a little dark, ~10 layers down is mostly dark, never fully black.
        private static double ShadeForDepth(byte depth)
        {
            return Math.Min(0.85, 0.25 + 0.06 * (depth - 1));
        }

        public void IsHovered() {
            Tile.Background = Brushes.DarkOrange;
            Image.Opacity = 0.5;
            Ghost.Opacity = 0.5;
        }
        public void IsUnHovered()
        {
            Tile.Background = Brushes.Transparent;
            Image.Opacity = _restingImageOpacity;
            Ghost.Opacity = 1;
        }
        public void IsClicked()
        {
            Tile.Background = Brushes.Yellow;
            Image.Opacity = 0.5;
            Ghost.Opacity = 0.5;
        }

        public void IsUnClicked()
        {
            if (this.IsMouseOver) IsHovered();
            else IsUnHovered();
        }
    }
}
