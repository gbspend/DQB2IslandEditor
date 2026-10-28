using DQB2IslandEditor.DataPK;
using DQB2IslandEditor.ObjectPK;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Net.WebSockets;
using Microsoft.Win32;
using System.ComponentModel;
using DQB2IslandEditor.InterfacePK.ChunkEditor.Map;
using System.Windows.Media.Animation;
using System.Windows.Media;
using DQB2IslandEditor.ObjectPK.Container;


namespace DQB2IslandEditor.InterfacePK.ChunkEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class ChunkEditorWindow : Window
    {
        private ChunkEditorViewModel viewModel;
        private SaveData saveData;

        readonly double WINDOW_WIDTH = 1600;
        readonly double WINDOW_HEIGHT = 900;
        //stgdatPath: load this exact file instead of the island's STGDATxx.BIN (used by Reload).
        public ChunkEditorWindow(SaveData saveData, byte island, string? stgdatPath = null)
        {
            Task<ushort> readFileTask = Task.Run(() => ReadFile(saveData, island, stgdatPath));
            Task databaseBlockTask = Task.Run(() => DataBaseReading.ReadBlockFile());
            Task databaseItemTask = Task.Run(() => DataBaseReading.ReadItemFile());

            viewModel = new ChunkEditorViewModel(this, saveData);
            viewModel.SelectedTool = 0;

            this.DataContext = viewModel;
            this.saveData = saveData;

            InitializeComponent();
            //Keep the layer bar highlight in sync with the current layer.
            viewModel.LayerUpdateNotification += (_, e) =>
            {
                if (e.PropertyName == nameof(viewModel.CurrentLayer)) LayerBar.LayerChange(viewModel.CurrentLayer);
            };

            //The editor opens on the Map tab, so the inventory only gets a real size once its tab is shown.
            inventoryMenu.Loaded += (_, _) => inventoryMenu.Resize(inventoryMenu.ActualHeight, inventoryMenu.ActualWidth);
            inventoryMenu.shareViewModel(viewModel);
            favouriteList.shareViewModel(viewModel);
            chunkBlockGrid.ShareViewModel(viewModel);
            toolMenu.UpdateContext(viewModel);

            databaseBlockTask.Wait();
            databaseItemTask.Wait();
            viewModel.CreateInventory();

            chunkBlockGrid.SetChunk(readFileTask.Result);
            viewModel.JumpToFirstAirLayer();
            virtualGridView.Init(saveData.Island, viewModel);
            //Once the minimap has a size, scroll it so the starting chunk is in the middle.
            //Only the first time, Loaded fires again whenever the Map tab is reselected.
            RoutedEventHandler? centerOnce = null;
            centerOnce = (_, _) =>
            {
                MapScrollViewer.Loaded -= centerOnce;
                CenterMinimapOn(viewModel.CurrentChunk);
            };
            MapScrollViewer.Loaded += centerOnce;
            //Keep the selected chunk visible on the minimap as it moves.
            viewModel.ChunkUpdateNotification += (_, _) => BringMinimapChunkIntoView(viewModel.CurrentChunk);
            //The Generate tab paints on the minimap, so it needs the Map view and no Inventory.
            viewModel.GenerateModeChanged += (_, _) =>
            {
                if (viewModel.GenerateMode) MapTab.IsSelected = true;
                InventoryTab.IsEnabled = !viewModel.GenerateMode;
            };

            LayerBar.UpdateSeaLevel(30); //Change
        }
        public async Task<ushort> ReadFile(SaveData saveData, byte island, string? stgdatPath)
        {
            Task loadSaveFile = stgdatPath != null
                ? Task.Run(() => saveData.OpenSTGDATCompressedFile(stgdatPath))
                : Task.Run(() => saveData.OpenSTGDATCompressedFile(island));
            await loadSaveFile;
            Task<ushort> loadFirstChunk = Task.Run(() => saveData.Island.GetMiddleChunk());
            return await loadFirstChunk;
        }
        private void RecalculateSize(object sender, SizeChangedEventArgs e)
        {
            double width = e.NewSize.Width;
            double height = e.NewSize.Height;

            if (Math.Abs(e.PreviousSize.Width - e.NewSize.Width) > 1e-5)
                this.Height = (WINDOW_HEIGHT * width) / WINDOW_WIDTH;
            else if (Math.Abs(e.PreviousSize.Height - e.NewSize.Height) > 1e-5)
                this.Width = (WINDOW_WIDTH * height) / WINDOW_HEIGHT;

            if (width < WINDOW_WIDTH || height < WINDOW_HEIGHT)
            {
                double ratio = width / height;
                if (ratio < WINDOW_WIDTH / WINDOW_HEIGHT)
                {
                    WindowScale.ScaleX = width / WINDOW_WIDTH;
                    WindowScale.ScaleY = width / WINDOW_WIDTH;
                }
                else
                {
                    WindowScale.ScaleX = height / WINDOW_HEIGHT;
                    WindowScale.ScaleY = height / WINDOW_HEIGHT;
                }
               
            }
            else
            {
                WindowScale.ScaleX = 1;
                WindowScale.ScaleY = 1;
            }
            if (inventoryMenu != null)
            {
                inventoryMenu.Resize(inventoryMenu.ActualHeight, inventoryMenu.ActualWidth);
                LayerBar.Resize();
            }
        }

        private void OpenSTGDAT(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "STGDAT.BIN|STGDAT*.BIN|AUTOSTGDAT.BIN|AUTOSTGDAT.BIN|Backup File|*STGDAT*.BIN"
                };

                if (openFileDialog.ShowDialog() == false) return;

                saveData.OpenSTGDATCompressedFile(openFileDialog.FileName);
                viewModel.ChangeChunk(saveData.Island.GetMiddleChunk());
                viewModel.JumpToFirstAirLayer();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                MessageBox.Show(ex.Message, "Failed to open file", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        //Throws away all in-memory changes and loads the current island file again from disk.
        //Done by opening a fresh editor window on the same file (at the same chunk/layer) and closing this one,
        //so nothing from the old island lingers in the views or tools.
        private void ReloadSTGDAT(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure? Any unsaved changes will be lost.", "Reload island",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                string path = saveData.Island.STGDATPath;
                if (!System.IO.File.Exists(path))
                {
                    MessageBox.Show("Couldn't find " + path, "Failed to reload file", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                ushort chunk = viewModel.CurrentChunk;
                byte layer = viewModel.CurrentLayer;

                var reloaded = new ChunkEditorWindow(saveData, saveData.Island.islandNumber, path)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = Left,
                    Top = Top,
                    Width = Width,
                    Height = Height,
                    WindowState = WindowState,
                };
                reloaded.viewModel.ChangeChunk(chunk);
                reloaded.viewModel.CurrentLayer = layer;
                reloaded.Show();
                Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                MessageBox.Show(ex.Message, "Failed to reload file", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveSTGDAT(object sender, RoutedEventArgs e)
        {
            try
            {
                SavedText.Opacity = 1;
                SavedText.Text = "Saving...";
                SavedText.Foreground = Brushes.Gold;
                Task save = Task.Run(() => SaveSTGDATAsync(saveData.Island.STGDATPath));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                MessageBox.Show(ex.Message, "Failed to save file", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void SaveSTGDATAs(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "STGDAT.BIN|STGDAT*.BIN|AUTOSTGDAT.BIN|AUTOSTGDAT.BIN",
                    FileName = "STGDAT" + saveData.Island.islandNumber.ToString("D2") + ".BIN",

                };

                if (saveFileDialog.ShowDialog() == false) return;
                SavedText.Opacity = 1;
                SavedText.Text = "Saving...";
                SavedText.Foreground = Brushes.Gold;
                Task save = Task.Run(() => SaveSTGDATAsync(saveFileDialog.FileName));

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                MessageBox.Show(ex.Message, "Failed to save file", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SaveSTGDATAsync(string path)
        {
            //I think this should wooork....
            saveData.SaveSTGDATCompressedFile(path);

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                SavedText.Text = "Island Saved!";
                SavedText.Foreground = Brushes.Lime;
                var fadeIn = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    BeginTime = TimeSpan.FromSeconds(2),
                    Duration = TimeSpan.FromSeconds(2),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };
                SavedText.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }));
        }

        private void ToolClick(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            viewModel.SelectedTool = byte.Parse((button.Content as Image).Name.Split("_").Last());
        }

        private void ToolChange1(object sender, RoutedEventArgs e)
        {
            viewModel.auraSize = 0;
            viewModel.paintFill = false;
            viewModel.SelectedTool = 2;
            PaintPopup.IsOpen = false;
        }
        private void ToolChange2(object sender, RoutedEventArgs e)
        {
            viewModel.auraSize = 2;
            viewModel.auraSquare = false;
            viewModel.paintFill = false;
            viewModel.SelectedTool = 2;
            PaintPopup.IsOpen = false;
        }
        private void ToolChange3(object sender, RoutedEventArgs e)
        {
            viewModel.auraSize = 2;
            viewModel.paintFill = false;
            viewModel.SelectedTool = 2;
            viewModel.auraSquare = true;
            PaintPopup.IsOpen = false;
        }
        private void ToolChangeFill(object sender, RoutedEventArgs e)
        {
            viewModel.paintFill = true;
            viewModel.SelectedTool = 2;
            PaintPopup.IsOpen = false;
        }


        private void CoverGroundWith(object sender, RoutedEventArgs e)
        {
            saveData.Island.CoverGroundWith(
                new BlockInstance(209,0),
                new BlockInstance(4, 0));

            saveData.Island.CoverGroundWith(
                new BlockInstance(8, 0),
                new BlockInstance(19, 0));
        }


        private void CenterMinimapOn(ushort chunk)
        {
            var cell = virtualGridView.GetChunkElement(chunk);
            if (cell == null) return;
            MapScrollViewer.UpdateLayout();
            //Center of the cell in the scroll viewer's own (scaled, scrolled) coordinates.
            Point center = cell.TransformToAncestor(MapScrollViewer)
                .Transform(new Point(cell.ActualWidth / 2, cell.ActualHeight / 2));
            MapScrollViewer.ScrollToHorizontalOffset(MapScrollViewer.HorizontalOffset + center.X - MapScrollViewer.ViewportWidth / 2);
            MapScrollViewer.ScrollToVerticalOffset(MapScrollViewer.VerticalOffset + center.Y - MapScrollViewer.ViewportHeight / 2);
        }

        //Scrolls the minimap only as far as needed to show the chunk, plus half a cell of margin.
        private void BringMinimapChunkIntoView(ushort chunk)
        {
            var cell = virtualGridView.GetChunkElement(chunk);
            if (cell == null || !cell.IsVisible) return; //Map tab not shown, or chunk off the minimap.
            double w = cell.ActualWidth, h = cell.ActualHeight;
            cell.BringIntoView(new Rect(-w / 2, -h / 2, w * 2, h * 2));
        }

        //WASD moves the chunk selection north/west/south/east.
        private void WindowKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBoxBase) return; //Typing, e.g. in the inventory filter.
            if (Keyboard.Modifiers != ModifierKeys.None) return;
            string? command = e.Key switch
            {
                Key.W => "u",
                Key.A => "l",
                Key.S => "d",
                Key.D => "r",
                _ => null
            };
            if (command == null) return;
            chunkBlockGrid.ChangeChunk(command);
            e.Handled = true;
        }

        private void UpdateMapSize(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

            double newZoom = e.NewValue;
            double oldZoom = ((ScaleTransform)virtualGridView.LayoutTransform).ScaleX; // Assuming uniform scaling

            ushort[] coords = virtualGridView.getCoords();

            // Optional: get center before zoom
            Point scrollCenter = new Point(
                MapScrollViewer.HorizontalOffset + MapScrollViewer.ViewportWidth * ((double)(coords[0]) / 64),
                MapScrollViewer.VerticalOffset + MapScrollViewer.ViewportHeight * ((double)(coords[1]) / 64));

            // Update zoom
            ((ScaleTransform)virtualGridView.LayoutTransform).ScaleX = newZoom;
            ((ScaleTransform)virtualGridView.LayoutTransform).ScaleY = newZoom;

            // Optional: re-center after zoom
            MapScrollViewer.ScrollToHorizontalOffset((scrollCenter.X) * newZoom / oldZoom - MapScrollViewer.ViewportWidth * ((double)(coords[0]) / 64));
            MapScrollViewer.ScrollToVerticalOffset((scrollCenter.Y) * newZoom / oldZoom - MapScrollViewer.ViewportHeight * ((double)(coords[1]) / 64));
        }
        ///////STATIC

        //The reason for this is because I dont want to have 5000000 of this object, so 
        //Ill just have one and move it around.

        private static Grid _currentUser;
        private static InventoryInfoPanel infoPanel = new InventoryInfoPanel();

        public static void openInfoPanel(Grid request, ObjectInfo data)
        {
            if (_currentUser != null) _currentUser.Children.Clear();
            _currentUser = request;
            infoPanel.objectInfo = data;
            request.Children.Add(infoPanel);
        }

        
    }
}