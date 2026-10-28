using DQB2IslandEditor.DataPK;
using DQB2IslandEditor.ObjectPK;
using DQB2IslandEditor.ObjectPK.Container;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography.Pkcs;
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

namespace DQB2IslandEditor.InterfacePK.ChunkEditor.Map.ChunkView
{
    public partial class ChunkView : UserControl
    {
        private ChunkEditorViewModel viewModel;

        private ushort chunkIndex;
        public ushort ChunkIndex
        {
            get
            {
                return this.chunkIndex;
            }
            set
            {
                if (value >= Island.GRID_DIMENSION * Island.GRID_DIMENSION) return;
                this.chunkIndex = value;
                UpdateView(null,null);
            }
        }
        private byte layer => viewModel.CurrentLayer;
        private Chunk displayedChunk => viewModel.CurrentIsland.GetChunk(chunkIndex);

        public Dictionary<ushort, List<ItemContainer>> itemContainerGrid = new Dictionary<ushort, List<ItemContainer>>();
        public TileContainer[] tiles = new TileContainer[Chunk.X_DIMENSION*Chunk.Z_DIMENSION];
        public ChunkView(ChunkEditorViewModel viewModel)
        {
            InitializeComponent();
            viewModel.LayerUpdateNotification += UpdateView;
            this.viewModel = viewModel;
            try
            {
                for (ushort i = 0; i < 1024; i++)
                {
                    tiles[i] = new TileContainer(i, Enter,
                        Leave, LeftClick, Release);

                    TileGrid.Children.Add(tiles[i]);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void Enter(ushort offset) { viewModel.BlockTile_TileEnter(offset, chunkIndex); }
        private void Leave(ushort offset) { viewModel.BlockTile_TileLeave(offset, chunkIndex); }
        private void LeftClick(ushort offset) { viewModel.BlockTile_LeftClick(offset, chunkIndex); }
        private void Release(ushort offset) { viewModel.BlockTile_Release(offset, chunkIndex); }

        //Update the display.
        private void UpdateView(object sender, PropertyChangedEventArgs e)
        {
            var blockInstances = displayedChunk.GetBlocksFromLayer(layer);
            for (int i = 0; i < 1024; i++)
            {
                tiles[i].blockInstance = blockInstances[i];
            }
            UpdateSeeThrough(blockInstances);
            var itemInstances = displayedChunk.GetItemsFromLayer(layer);
            //should have delegated the block one to the chunk block grid as well but ah.
            UpdateItemsOnGrid(itemInstances);
        }


        //------------------------------------------------------------------
        //SEE-THROUGH
        //For every air or water tile on the current layer, find the first solid block below it
        //and show it darkened by how many layers down it is. Water is see-through too:
        //water tiles are drawn translucent over the floor, and water below an air tile
        //is drawn translucent over the floor under it.
        private const uint AIR_ID = 0;

        private static bool IsWater(uint id)
        {
            return DataBaseReading.BLOCK_INFO_DICTIONARY.TryGetValue(id, out var info) && info.isWater;
        }

        private static bool IsSeeThrough(uint id) => id == AIR_ID || IsWater(id);

        //Tracks one column while walking down it. Returns true once a solid block has been found.
        private class ColumnScan
        {
            public readonly bool fromWater; //The current-layer tile is water.
            public uint? waterID;
            public byte waterDepth;
            public ColumnScan(bool fromWater) { this.fromWater = fromWater; }

            public bool Step(TileContainer tile, uint id, byte depth)
            {
                if (id == AIR_ID) return false;
                if (IsWater(id))
                {
                    //Remember the top of the water. Water under water is just more of the same body.
                    if (!fromWater && waterID == null) { waterID = id; waterDepth = depth; }
                    return false;
                }
                tile.SetGhost(id, depth, waterID, waterDepth);
                return true;
            }

            //Reached the bottom without finding anything solid.
            public void Finish(TileContainer tile)
            {
                if (waterID != null) tile.SetGhost(null, 0, waterID, waterDepth);
                else tile.ClearGhost();
            }
        }

        private void UpdateSeeThrough(BlockInstance[] currentLayerBlocks)
        {
            if (!viewModel.ShowSeeThrough || displayedChunk.IsEmpty() || layer == 0)
            {
                for (int i = 0; i < tiles.Length; i++) tiles[i].ClearGhost();
                return;
            }

            //Tiles that still need a block found under them.
            var pending = new Dictionary<int, ColumnScan>();
            for (int i = 0; i < tiles.Length; i++)
            {
                var id = currentLayerBlocks[i].publicBlockID;
                if (IsSeeThrough(id)) pending[i] = new ColumnScan(id != AIR_ID);
                else tiles[i].ClearGhost();
            }

            //Walk down one layer at a time, only checking columns that are still see-through.
            var done = new List<int>();
            for (int y = layer - 1; y >= 0 && pending.Count > 0; y--)
            {
                var below = displayedChunk.GetBlocksFromLayer((byte)y);
                var depth = (byte)(layer - y);
                foreach (var (i, scan) in pending)
                    if (scan.Step(tiles[i], below[i].publicBlockID, depth)) done.Add(i);
                foreach (var i in done) pending.Remove(i);
                done.Clear();
            }

            //Nothing solid below at all (open sea/void).
            foreach (var (i, scan) in pending) scan.Finish(tiles[i]);
        }

        //Recalculate a single column after an edit on the current layer.
        private void UpdateSeeThroughAt(ushort offset)
        {
            var tile = tiles[offset];
            var currentID = tile.blockInstance.publicBlockID;
            if (!viewModel.ShowSeeThrough || displayedChunk.IsEmpty() || !IsSeeThrough(currentID))
            {
                tile.ClearGhost();
                return;
            }
            byte x = (byte)(offset % Chunk.X_DIMENSION);
            byte z = (byte)(offset / Chunk.X_DIMENSION);
            var scan = new ColumnScan(currentID != AIR_ID);
            for (int y = layer - 1; y >= 0; y--)
            {
                var id = displayedChunk.GetBlockFromCoords(x, z, (byte)y).publicBlockID;
                if (scan.Step(tile, id, (byte)(layer - y))) return;
            }
            scan.Finish(tile);
        }

        private void UpdateItemsOnGrid(List<ItemInstance> items)
        {
            ItemGrid.Children.Clear();
            itemContainerGrid.Clear();
            foreach (var item in items)
            {
                ItemContainer itemContainer = new ItemContainer(item, 32); //  1024/32

                itemContainer.ItemBorder.PreviewMouseDown += (_, _) => { viewModel.ItemTile_LeftClick(itemContainer); };
                itemContainer.ItemBorder.MouseEnter += (_, _) => { viewModel.ItemTile_TileEnter(itemContainer); };
                itemContainer.ItemBorder.MouseLeave += (_, _) => { viewModel.ItemTile_TileLeave(itemContainer); };
                itemContainer.ItemBorder.MouseLeftButtonUp += (_, _) => { viewModel.ItemTile_Release(itemContainer); };

                ItemGrid.Children.Add(itemContainer);
                //Alright, here comes the funky stuff
                //You can have multiple items in the same tile, meaning, chaos
                //This will create the need to have multiple item panels for each item in that tile
                //I will also have to keep track of every item in the tile
                //So, the dictionary holds the offset (x + z*32), with a list of all the items on the offset
                //That is all.
                if (!itemContainerGrid.ContainsKey(item.worldOffset))
                    itemContainerGrid[item.worldOffset] = new List<ItemContainer>();
                itemContainerGrid[item.worldOffset].Append(itemContainer);
                //Hope this doesnt tank the performance tbh
            }
        }

        //------------------------------------------------------------------
        //GETTING BLOCKS / ITEMS
        public BlockInstance GetBlockInstanceInOffset(ushort offset)
        {
            return tiles[offset].blockInstance;
        }
        public BlockInfo GetBlockInfoInOffset(ushort offset)
        {
            return tiles[offset].blockInfo.Value;
        }

        //this gets the items that are in a specific coordinate.
        public ItemInstance[] GetItemsInOffset(ushort offset)
        {
            if (!itemContainerGrid.ContainsKey(offset)) return null;
            var items = itemContainerGrid[offset];
            ItemInstance[] list = new ItemInstance[items.Count];
            for (int i = 0; i < items.Count; i++) list[i] = items.ElementAt(i).itemInstance;

            return list;
        }
        //------------------------------------------------------------------
        //Setting blocks and items
        public void AddItemToOffset(ushort offset, ItemInfo Item)
        {
            //Not implemented yet.
        }


        //--------------------------------------------------------------------
        //Asthetic:
        public void TileAura_Create(ushort[] offsets)
        {
            foreach (var offset in offsets)
            {
                tiles[offset].IsHovered();
            }
        }
        public void TileAura_SetBlock(ushort[] offsets,BlockInstance bi)
        {
            foreach (var offset in offsets)
            {
                Offset_SetBlock(offset, bi);
            }
        }

        public void TileAura_Destroy(ushort[] offsets)
        {
            foreach (var offset in offsets)
            {
                tiles[offset].IsUnHovered();
            }
        }

        public void Tile_Click(ushort offset)
        {
            tiles[offset].IsClicked();
        }
        public void Tile_Unclick(ushort offset)
        {
            tiles[offset].IsUnClicked();
        }

        private void Offset_SetBlock(ushort offset, BlockInstance bi)
        {
            displayedChunk.SetBlockFromCoords(bi, (byte)(offset % Chunk.X_DIMENSION), (byte)(offset / Chunk.Z_DIMENSION), viewModel.CurrentLayer);
            tiles[offset].blockInstance = bi;
            UpdateSeeThroughAt(offset);
        }

    }
}
