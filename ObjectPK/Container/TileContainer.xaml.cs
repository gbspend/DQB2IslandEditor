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

        //See-through view: the block shown under an air tile (null when not showing one).
        public ObservableProperty<BlockInfo> ghostInfo { get; set; } = new ObservableProperty<BlockInfo> { };
        private uint? _ghostBlockID;
        private byte _ghostDepth;

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
        public void SetGhost(uint blockID, byte depth)
        {
            if (_ghostBlockID == blockID && _ghostDepth == depth) return; //Nothing changed.
            if (!DataBaseReading.BLOCK_INFO_DICTIONARY.TryGetValue(blockID, out var info))
            {
                ClearGhost(); //Unknown block ID, don't crash the view over it.
                return;
            }
            _ghostBlockID = blockID;
            _ghostDepth = depth;
            ghostInfo.Value = info;
            GhostShade.Opacity = ShadeForDepth(depth);
            Ghost.Visibility = Visibility.Visible;
            Image.Visibility = Visibility.Hidden;
        }

        public void ClearGhost()
        {
            if (_ghostBlockID == null) return;
            _ghostBlockID = null;
            ghostInfo.Value = null;
            Ghost.Visibility = Visibility.Collapsed;
            Image.Visibility = Visibility.Visible;
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
            Image.Opacity = 1;
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
