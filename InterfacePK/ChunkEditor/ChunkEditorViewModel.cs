using DQB2IslandEditor.DataPK;
using DQB2IslandEditor.InterfacePK.ChunkEditor;
using DQB2IslandEditor.InterfacePK.ChunkEditor.Inventory;
using DQB2IslandEditor.InterfacePK.ChunkEditor.Tool.ToolClass;
using DQB2IslandEditor.ObjectPK;
using DQB2IslandEditor.ObjectPK.Container;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace DQB2IslandEditor.InterfacePK
{
    public class ChunkEditorViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public event PropertyChangedEventHandler? LayerUpdateNotification;
        public event PropertyChangedEventHandler? ChunkUpdateNotification;

        private SaveData saveData;
        public Island CurrentIsland => saveData.Island;
        private ChunkEditorWindow chunkEditorWindow;



        //-------------------------UPDATE CHUNK------------------------------
        //Colective Layer value.

        public void UpdatedChunks()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentChunks)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentChunk)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentChunkX)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentChunkY)));
            ChunkUpdateNotification?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentChunks)));
        }
        public void ChangeChunk(ushort vChunk)
        {
            chunkEditorWindow.chunkBlockGrid.SetChunk(vChunk);
        }
        private byte _currentLayer;
        public byte CurrentLayer
        {
            get
            {
                return _currentLayer;
            }
            set
            {
                if (value >= Chunk.Y_DIMENSION) return;
                _currentLayer = value;
                LayerUpdateNotification?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLayer)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLayer)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SeaBrush)));
            }
        }
        //Moves the view to the lowest layer that has air in any of the displayed chunks.
        public void JumpToFirstAirLayer()
        {
            CurrentLayer = CurrentChunks.Min(c => CurrentIsland.GetChunk(c).FirstLayerWithAir());
        }
        public ushort[] CurrentChunks => chunkEditorWindow.chunkBlockGrid.ChunkList;
        public ushort CurrentChunk => chunkEditorWindow.chunkBlockGrid.FirstChunk;
        public int CurrentChunkX => CurrentChunk % Island.GRID_DIMENSION;
        public int CurrentChunkY => CurrentChunk / Island.GRID_DIMENSION;


        //------------------------------------GENERATE-----------------------------
        //The Generate tab paints a selection of chunks on the minimap (green), completely
        //separate from the current chunk (orange).
        public event EventHandler? GenerateModeChanged;
        public event EventHandler? GenerateSelectionChanged;

        private bool _generateMode;
        public bool GenerateMode
        {
            get { return _generateMode; }
            set
            {
                if (_generateMode == value) return;
                _generateMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GenerateMode)));
                GenerateModeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private HashSet<ushort> _generateSelection = new HashSet<ushort>();
        public IReadOnlySet<ushort> GenerateSelection => _generateSelection;
        public bool HasGenerateSelection => _generateSelection.Count > 0;

        public void SetGenerateSelection(IEnumerable<ushort> chunks)
        {
            var newSelection = new HashSet<ushort>(chunks);
            if (newSelection.SetEquals(_generateSelection)) return;
            _generateSelection = newSelection;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GenerateSelection)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasGenerateSelection)));
            GenerateSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public const byte GENERATE_MIN_LAYER = 4;
        private const uint CHALK_ID = 8;

        //Builds a Chalk mountain across the selected chunks, from GENERATE_MIN_LAYER up to its
        //surface in each column. Blocks above the surface are left as they were.
        public void Generate(MountainParams parameters)
        {
            if (!HasGenerateSelection) return;
            int x0 = _generateSelection.Min(c => c % Island.GRID_DIMENSION);
            int z0 = _generateSelection.Min(c => c / Island.GRID_DIMENSION);
            int x1 = _generateSelection.Max(c => c % Island.GRID_DIMENSION);
            int z1 = _generateSelection.Max(c => c / Island.GRID_DIMENSION);
            int size = Chunk.X_DIMENSION;
            var heights = MountainGenerator.HeightMap((x1 - x0 + 1) * size, (z1 - z0 + 1) * size, parameters);

            var chalk = new BlockInstance(CHALK_ID, 0, false); //No chisel, not builder placed.
            foreach (var chunkPosition in _generateSelection)
            {
                var chunk = CurrentIsland.GetChunk(chunkPosition);
                int offsetX = (chunkPosition % Island.GRID_DIMENSION - x0) * size;
                int offsetZ = (chunkPosition / Island.GRID_DIMENSION - z0) * size;
                for (byte x = 0; x < size; x++)
                    for (byte z = 0; z < size; z++)
                    {
                        int top = heights[offsetX + x, offsetZ + z];
                        if (top >= 0) chunk.FillColumn(chalk, x, z, GENERATE_MIN_LAYER, (byte)top);
                    }
            }
            //Show the result: jump to the middle of the selection if none of it is on screen,
            //otherwise just redraw.
            if (!CurrentChunks.Any(_generateSelection.Contains))
                ChangeChunk((ushort)((x0 + x1) / 2 + Island.GRID_DIMENSION * ((z0 + z1) / 2)));
            LayerUpdateNotification?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLayer)));
        }

        //Lowest and highest top solid layer (ignoring water) of every column in the selection.
        //The mountain rises from the lowest so none of it is buried.
        public (byte Min, byte Max) GenerateGroundRange()
        {
            if (!HasGenerateSelection) return (GENERATE_MIN_LAYER, GENERATE_MIN_LAYER);
            byte min = byte.MaxValue, max = 0;
            foreach (var chunkPosition in _generateSelection)
            {
                var chunk = CurrentIsland.GetChunk(chunkPosition);
                for (byte x = 0; x < Chunk.X_DIMENSION; x++)
                    for (byte z = 0; z < Chunk.Z_DIMENSION; z++)
                    {
                        byte top = chunk.TopLayer(x, z, IsAirOrWater);
                        min = Math.Min(min, top);
                        max = Math.Max(max, top);
                    }
            }
            return (Math.Max(GENERATE_MIN_LAYER, min), Math.Max(GENERATE_MIN_LAYER, max));
        }

        private static bool IsAirOrWater(uint id) =>
            id == 0 || (DataBaseReading.BLOCK_INFO_DICTIONARY.TryGetValue(id, out var info) && info.isWater);

        //------------------------------------HUD-----------------------------
        //Colective hud stuffs
        private bool _showMinimapGrid = true;
        private bool _showFullGrid = false;
        private bool _showItems = true;
        public bool ShowMinimapGrid { get { return _showMinimapGrid; } set {
                _showMinimapGrid = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowMinimapGrid)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowMinimapGridVisibility)));
            }
        }
        public bool ShowFullGrid
        {
            get { return _showFullGrid; }
            set { 
                _showFullGrid = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowFullGrid)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowFullGridVisibility)));
            }
        }
        public bool ShowItems
        {
            get { return _showItems; }
            set
            {
                _showItems = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowItems)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowItemsVisibility)));
            }
        }
        //See-through: air tiles show the first solid block below them, darkened by depth.
        private bool _showSeeThrough = true;
        public bool ShowSeeThrough
        {
            get { return _showSeeThrough; }
            set
            {
                _showSeeThrough = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowSeeThrough)));
                //Chunk views redraw on this notification.
                LayerUpdateNotification?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowSeeThrough)));
            }
        }
        public Brush SeaBrush
        {
            get { return _currentLayer > 30 ? Brushes.Black : Brushes.DarkBlue; }
        }
        public Visibility ShowMinimapGridVisibility { get { return _showMinimapGrid ? Visibility.Visible : Visibility.Hidden; } }
        public Visibility ShowFullGridVisibility { get { return _showFullGrid ? Visibility.Visible : Visibility.Hidden; } }
        public Visibility ShowItemsVisibility { get { return _showItems ? Visibility.Visible : Visibility.Collapsed; } }
        //----------------------------------------------------------------------------------------------------------------
        //TOOL HANDELING:

        private byte _selectedTool;

        public byte auraSize = 1;
        public bool auraSquare = false;
        public bool paintFill = false; //Paint tool fills the connected area instead of using the aura.

        private ITool _currentTool; //Handler of the current tool.

        public byte SelectedTool { get { return _selectedTool; }
            set {
                //Paint is rebuilt even when already selected, since its mode (brush or fill) may have changed.
                if(_selectedTool != value || value == 2)
                {
                    switch (value)
                    {
                        case 0:
                            _currentTool = new SelectionTool(this, chunkEditorWindow.chunkBlockGrid);
                            break;
                        case 2:
                            _currentTool = paintFill ? new FillTool(this, chunkEditorWindow.chunkBlockGrid) : new TrowelTool(this, chunkEditorWindow.chunkBlockGrid);
                            break;
                    }
                }
                _selectedTool = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTool)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedToolImage)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedToolName)));
            }
        }

        //Tool display.
        public string SelectedToolName => _selectedTool == 0 ? "Select" : _selectedTool == 1 ? "Area" : _selectedTool == 2 ? (paintFill ? "Fill" : "Paint") : "Chisel";
        public ImageSource SelectedToolImage => DataBaseReading.toolImage(SelectedTool, true);
        public ImageSource ToolImageZero => DataBaseReading.toolImage(0, false);
        public ImageSource ToolImageOne => DataBaseReading.toolImage(1, false);
        public ImageSource ToolImageTwo => DataBaseReading.toolImage(2, false);
        public ImageSource ToolImageThree => DataBaseReading.toolImage(3, false);


        //----------------------------------------------------------------------------------------------------------------

        //Selected Tile:
        //This will get a rework, do not worry.

        private ObjectInfo _selectedObject; //In the inventory menu.

        //Selected block data.
        private BlockInfo _selectedBlock; 
        private Chisel _currentChisel;
        private bool _currentBuilderPlaced;

        public ObjectInfo SelectedObject
        {
            set
            {
                _selectedObject = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedObject)));
                if (value is BlockInfo blockInfo)
                {
                    SelectedBlock = blockInfo;
                }
            }
            get
            {

                return _selectedObject;
            }
        }

        public BlockInfo SelectedBlock
        {
            set
            {
                _selectedBlock = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedBlock)));
            }
            get
            {

                return _selectedBlock;
            }
        }

        public Chisel Chisel
        {
            set
            {
                _currentChisel = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chisel)));
            }
            get
            {

                return _currentChisel;
            }
        }
        public bool BuilderPlaced
        {
            set{
                _currentBuilderPlaced = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BuilderPlaced))); }
            get
            { return _currentBuilderPlaced; }}

        public BlockInstance currentBlockInstance
        {
            get
            {
                if (_selectedBlock == null) return new BlockInstance(0, 0);
                return new BlockInstance(_selectedBlock.objectId, _currentChisel, _currentBuilderPlaced);
            }
        }

        //Change selected object -> Selecting anything on the inventory
        public void UpdateSelectedObject(ObjectInfo selectedObject)
        {
            SelectedObject = selectedObject;
        }

        //Change selected block -> Selecting a tile on the map
        public void UpdateSelectedBlock(BlockInstance selectedBlock)
        {
            Chisel = selectedBlock.publicChiselID;
            BuilderPlaced = selectedBlock.publicBuilderPlaced;
        }

        //Update favourite -> Right clicking anything on the inventory, right click dropicking a chunk tile 
        public void UpdateFavouriteObject(ObjectInfo selectedObject)
        {
            chunkEditorWindow.favouriteList.UpdateFavourite(selectedObject); //Update favourite
        }

        //--------------------CREATION------------------------------
        public ChunkEditorViewModel(ChunkEditorWindow mainWindow, SaveData saveData) {
            this.chunkEditorWindow = mainWindow;
            this.saveData = saveData;
            
        }
        public void CreateInventory()
        {
            IDictionary<uint, ObjectInfo> objectDic = DataBaseReading.BLOCK_INFO_DICTIONARY.ToDictionary(kvp => kvp.Key, kvp => (ObjectInfo)kvp.Value);
            IDictionary<uint, ObjectInfo> itemDic = DataBaseReading.ITEM_INFO_DICTIONARY.ToDictionary(kvp => kvp.Key, kvp => (ObjectInfo)kvp.Value);
            chunkEditorWindow.inventoryMenu.CreateInventory(objectDic, itemDic, chunkEditorWindow);

            chunkEditorWindow.selectedObject.SelButton.Click += (_, _) => { UpdateSelectedObject(_selectedObject); };
            chunkEditorWindow.selectedObject.SelButton.MouseRightButtonDown += (_, _) => { UpdateFavouriteObject(_selectedObject); };

            _currentTool = new SelectionTool(this, chunkEditorWindow.chunkBlockGrid); //Default tool is select.
        }


        //--------------------------- Chunk view inputs ----------------------------
        public void BlockTile_LeftClick(ushort offset, ushort chunk)
        {
            _currentTool.BlockInstance_MouseLeftClick(offset, chunk);
            /*
            chunkLayer[offset].IsClicked();
            switch (SelectedTool)
            {
                case 0: //Select
                    //This is what happens when you click a block.
                    SelectedObject = DataBaseReading.BLOCK_INFO_DICTIONARY[blockID];
                    SelectedTileOffset = offset;
                    //Block
                    ValueChisel = chunkLayer[offset].blockInstance.publicChiselID;
                    ValueBuilderPlaced = chunkLayer[offset].blockInstance.publicBuilderPlaced;
                    break;
                case 1: //Area
                    //Do area calculation (for future...)
                    break;
                case 2: //Paint (change for size stuff)
                    //Update tile
                    TileAura_SetBlock(offset);
                    break;
                case 3: //Chisel
                    chunkLayer[offset].blockInstance.UpdateBlock(ValueChisel);
                    saveData.Island.SetBlock(_currentChunk, _currentLayer, (byte)(offset % Chunk.X_DIMENSION), (byte)(offset / Chunk.Z_DIMENSION), chunkLayer[offset].blockInstance);
                    break;

            }
                        */
        }

        public void BlockTile_TileEnter(ushort offset, ushort chunk)
        {
            _currentTool.BlockInstance_MouseEnter(offset, chunk);
            /*
            switch (SelectedTool)
            {
                case 2: //Paint (change for size stuff)
                        //Handle bigger pencil
                    TileAura_Create(offset);
                    if (Mouse.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
                    {
                        chunkLayer[offset].IsHovered();
                        return;
                    }
                    chunkLayer[offset].IsClicked();
                    TileAura_SetBlock(offset);
                    
                    break;
                default:
                    if (Mouse.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
                        chunkLayer[offset].IsHovered();
                    else
                        chunkLayer[offset].IsClicked();
                    break;
            }
            */
        }

        public void BlockTile_TileLeave(ushort offset, ushort chunk)
        {
            _currentTool.BlockInstance_MouseLeave(offset, chunk);
            /*
            switch (SelectedTool)
            {
                case 2:
                    TileAura_Destroy(offset);
                    chunkLayer[offset].IsUnHovered();
                    break;
                default:
                    chunkLayer[offset].IsUnHovered();
                    break;
            }
            */
        }

        public void BlockTile_Release(ushort offset, ushort chunk)
        {
            _currentTool.BlockInstance_MouseRelease(offset, chunk);
            /*
             chunkLayer[offset].IsHovered();
            */
        }
        public void ItemTile_TileEnter(ItemContainer target)
        {
            //This will actually be a problem for compatibility between paint block and paint item
            //Oh no, what do you mean I should have used the state pattern oh noo
            //If you dont say anything then I wont say anything either wink wink
            switch (SelectedTool)
            {
                default:
                    if (Mouse.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
                        target.IsHovered();
                    else
                        target.IsClicked();
                    break;
            }
        }
        public void ItemTile_TileLeave(ItemContainer target)
        {
            switch (SelectedTool)
            {
                default:
                    target.IsUnHovered();
                    break;
            }
        }

        public void ItemTile_LeftClick(ItemContainer target)
        {
            /*
            target.IsClicked();
            switch (SelectedTool)
            {
                default:
                case 0: //Select
                    //When the items are hoverable, this is when the block underneath will always be selected to the xyz.
                    //Or, at least the xz
                    SelectedObject = DataBaseReading.ITEM_INFO_DICTIONARY[0];
                    SelectedTileOffset = target.offset;
                    //Block (Hmmmmm)
                    ValueChisel = chunkLayer[target.offset].blockInstance.publicChiselID;
                    ValueBuilderPlaced = chunkLayer[target.offset].blockInstance.publicBuilderPlaced;
                    break;
            }
            */
        }
        public void ItemTile_Release(ItemContainer target)
        {
            target.IsHovered();
        }
        
    }
}
