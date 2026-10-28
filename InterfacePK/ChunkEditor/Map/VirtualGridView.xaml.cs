using DQB2IslandEditor.DataPK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
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

namespace DQB2IslandEditor.InterfacePK.ChunkEditor.Map
{
    /// <summary>
    /// Interaction logic for VirtualGridView.xaml
    /// </summary>
    public partial class VirtualGridView : UserControl
    {
        private ChunkEditorViewModel viewModel;

        private Dictionary<ushort, Border> allChunks;

        private List<Border> selectedChunks = new List<Border>();
        public VirtualGridView()
        {
            InitializeComponent();
        }

        public void Init(Island island, ChunkEditorViewModel ViewModel)
        {
            allChunks = new Dictionary<ushort, Border>();
            this.viewModel = ViewModel;
            viewModel.ChunkUpdateNotification += UpdateChunks;
            viewModel.GenerateModeChanged += (_, _) =>
                GenerateOverlay.Visibility = viewModel.GenerateMode ? Visibility.Visible : Visibility.Collapsed;
            viewModel.GenerateSelectionChanged += (_, _) => UpdateGenerateOverlay();
            var dimensions = island.ChunkGridFrame();

            if(dimensions.X0 > 1) dimensions.X0 -= 1;
            if(dimensions.Y0 > 1) dimensions.Y0 -= 1;
            if(dimensions.X1 < Island.GRID_DIMENSION-1) dimensions.X1 += 1;
            if(dimensions.Y1 < Island.GRID_DIMENSION-1) dimensions.Y1 += 1;
            //Set the grid to the size of the island
            ChunkGridButtons.Columns = (int)(dimensions.X1 - dimensions.X0 + 1);
            ChunkGridButtons.Rows = (int)(dimensions.Y1 - dimensions.Y0 + 1);
            GenerateOverlay.Columns = ChunkGridButtons.Columns;
            GenerateOverlay.Rows = ChunkGridButtons.Rows;
            gridX0 = (int)dimensions.X0;
            gridY0 = (int)dimensions.Y0;

            for (uint y = dimensions.Y0; y <= dimensions.Y1; y++)
                {
                for (uint x = dimensions.X0; x <= dimensions.X1; x++)
                {
                    var overlayCell = new Border();
                    GenerateOverlay.Children.Add(overlayCell);
                    generateCells[(ushort)(x + (64 * y))] = overlayCell;

                    if (island.IsChunkEmpty((ushort)(x + (64 * y))))
                    {
                        Border empty = new Border()
                        {
                            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0555555")),
                            BorderBrush = new SolidColorBrush(Colors.Gray),
                            BorderThickness = new Thickness(1),
                            Tag = (ushort)1000
                        };
                        ChunkGridButtons.Children.Add(empty);
                        allChunks[(ushort)(x + (64 * y))] = empty;
                    }
                    else
                    {
                        Border chunk = new Border()
                        {
                            Background = new SolidColorBrush(Colors.Transparent),
                            BorderBrush = new SolidColorBrush(Colors.Gray),
                            BorderThickness = new Thickness(1),
                            Tag = (ushort)(x + (64 * y))
                        };
                        chunk.MouseEnter += (s, e) => MouseEnterChunk(chunk);
                        chunk.MouseLeave += (s, e) => MouseLeaveChunk(chunk);
                        chunk.MouseLeftButtonUp += (s, e) => MouseClickChunk(chunk);
                        chunk.MouseRightButtonUp += (s, e) => MouseRightClickChunk(chunk);
                        ChunkGridButtons.Children.Add(chunk);
                        allChunks[(ushort)(x + (64 * y))] = chunk;
                    }


                }
            }
            //Scale for minimap
            //I think i need the sum
            if (dimensions.X1 < Island.GRID_DIMENSION - 1) dimensions.X1 += 1;
            if (dimensions.Y1 < Island.GRID_DIMENSION - 1) dimensions.Y1 += 1;

            dimensions.X0 = dimensions.X0 * Minimap.MINIMAP_DIMENSION_IN_CHUNK;
            dimensions.X1 = dimensions.X1 * Minimap.MINIMAP_DIMENSION_IN_CHUNK;
            dimensions.Y0 = dimensions.Y0 * Minimap.MINIMAP_DIMENSION_IN_CHUNK;
            dimensions.Y1 = dimensions.Y1 * Minimap.MINIMAP_DIMENSION_IN_CHUNK;
            MinimapImage.Source = island.shell.pMinimap.MinimapImage(2, false, dimensions);

            UpdateChunks(null, null);
        }

        private void MouseEnterChunk(Border chunk)
        {
            //Set the color of the border
            chunk.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#55FF9000"));
            chunk.BorderBrush = new SolidColorBrush(Colors.Gold);
        }
        private void MouseLeaveChunk(Border chunk)
        {
            //Set the color of the border
            if (selectedChunks.Contains(chunk))
            {
                chunk.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#55FF4000"));
                chunk.BorderBrush = new SolidColorBrush(Colors.OrangeRed);
            }
            else
            {
                chunk.Background = new SolidColorBrush(Colors.Transparent);
                chunk.BorderBrush = new SolidColorBrush(Colors.Gray);
            }
        }
        private void MouseRightClickChunk(Border chunk)
        {
            //On the Generate tab left click paints, so right click picks the chunk to view instead.
            if (viewModel.GenerateMode) viewModel.ChangeChunk((ushort)chunk.Tag);
        }

        //------------------------------------------------------------------
        //GENERATE SELECTION
        //On the Generate tab, click and drag paints a rectangle of chunks green.
        //A new drag replaces the previous selection, but only if every chunk in the rectangle
        //is loaded. If it touches any grey empty chunk, the previous selection is kept.
        private static readonly Brush GENERATE_FILL = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5532CD32"));
        private Dictionary<ushort, Border> generateCells = new Dictionary<ushort, Border>();
        private int gridX0, gridY0; //Chunk grid coords of the minimap's top-left cell.
        private bool painting;
        private (int col, int row) paintStart, paintEnd;
        private List<ushort> selectionBeforePaint = new List<ushort>();

        private (int col, int row) CellAt(Point position)
        {
            int col = (int)(position.X / ChunkGridButtons.ActualWidth * ChunkGridButtons.Columns);
            int row = (int)(position.Y / ChunkGridButtons.ActualHeight * ChunkGridButtons.Rows);
            return (Math.Clamp(col, 0, ChunkGridButtons.Columns - 1), Math.Clamp(row, 0, ChunkGridButtons.Rows - 1));
        }

        private void PaintStart(object sender, MouseButtonEventArgs e)
        {
            if (viewModel == null || !viewModel.GenerateMode) return;
            painting = true;
            selectionBeforePaint = viewModel.GenerateSelection.ToList();
            paintStart = paintEnd = CellAt(e.GetPosition(ChunkGridButtons));
            ChunkGridButtons.CaptureMouse();
            PaintRectangle();
            e.Handled = true;
        }

        private void PaintMove(object sender, MouseEventArgs e)
        {
            if (!painting) return;
            var cell = CellAt(e.GetPosition(ChunkGridButtons));
            if (cell == paintEnd) return;
            paintEnd = cell;
            PaintRectangle();
        }

        private void PaintEnd(object sender, MouseButtonEventArgs e)
        {
            if (!painting) return;
            painting = false;
            ChunkGridButtons.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void PaintCancel(object sender, MouseEventArgs e)
        {
            painting = false;
        }

        private void PaintRectangle()
        {
            var selection = new List<ushort>();
            for (int row = Math.Min(paintStart.row, paintEnd.row); row <= Math.Max(paintStart.row, paintEnd.row); row++)
                for (int col = Math.Min(paintStart.col, paintEnd.col); col <= Math.Max(paintStart.col, paintEnd.col); col++)
                {
                    var chunk = (ushort)(gridX0 + col + Island.GRID_DIMENSION * (gridY0 + row));
                    if (!allChunks.TryGetValue(chunk, out var cell) || (ushort)cell.Tag == 1000)
                    {
                        //Touches an empty chunk: this paint does nothing.
                        viewModel.SetGenerateSelection(selectionBeforePaint);
                        return;
                    }
                    selection.Add(chunk);
                }
            viewModel.SetGenerateSelection(selection);
        }

        private void UpdateGenerateOverlay()
        {
            var selection = viewModel.GenerateSelection;
            foreach (var (chunk, cell) in generateCells)
            {
                bool selected = selection.Contains(chunk);
                cell.Background = selected ? GENERATE_FILL : null;
                cell.BorderBrush = selected ? Brushes.LimeGreen : null;
                cell.BorderThickness = new Thickness(selected ? 1.5 : 0);
            }
        }

        private void MouseClickChunk(Border chunk)
        {
            if (viewModel.GenerateMode) return; //Left click paints on the Generate tab.
            //Get the chunk number
            ushort chunkNumber = (ushort)chunk.Tag;
            //Set the chunk data to the selected one
            //This will do a circle and end up notifying me.
            viewModel.ChangeChunk(chunkNumber);
        }

        public void UpdateChunks(object sender, PropertyChangedEventArgs e)
        {
            var newList = viewModel.CurrentChunks;

            foreach (var chunk in selectedChunks) {
                if ((ushort)chunk.Tag != 1000)
                {
                    chunk.Background = new SolidColorBrush(Colors.Transparent);
                    chunk.BorderBrush = new SolidColorBrush(Colors.Gray);
                }
                else
                {
                    chunk.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0555555"));
                    chunk.BorderBrush = new SolidColorBrush(Colors.Gray);
                }
            }
            selectedChunks.Clear();
            foreach (var chunk in newList)
            {
                if (allChunks.ContainsKey(chunk))
                {
                    selectedChunks.Add(allChunks[chunk]);
                    allChunks[chunk].Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#55FF4000"));
                    allChunks[chunk].BorderBrush = new SolidColorBrush(Colors.OrangeRed);
                }
            }
        }


        //The minimap cell for a chunk, or null if it's outside the displayed area.
        public FrameworkElement GetChunkElement(ushort chunk)
        {
            if (allChunks == null) return null;
            return allChunks.TryGetValue(chunk, out var border) ? border : null;
        }

        public ushort[] getCoords()
        {
            ushort[] c = new ushort[2];
            ushort key = 0;
            if (allChunks == null) return c;  
            foreach (var chunk in allChunks)
            {
                key = (ushort)selectedChunks[0].Tag;
                if (key != 1000) break;
            }
            c[0] = (ushort)(key % 64);
            c[1] = (ushort)(key /64);
            return c;
        }

    }
}
