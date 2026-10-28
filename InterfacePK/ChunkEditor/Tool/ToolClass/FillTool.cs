using DQB2IslandEditor.DataPK;
using DQB2IslandEditor.InterfacePK.ChunkEditor.Map.ChunkView;
using System.Collections.Generic;

namespace DQB2IslandEditor.InterfacePK.ChunkEditor.Tool.ToolClass
{
    //Paint mode: replaces every block connected to the clicked one (NSEW) with the same block ID.
    //Stays inside the clicked chunk and the current layer, like the paint aura.
    public class FillTool : AHoverTool
    {
        private const uint AIR_ID = 0;

        public FillTool(ChunkEditorViewModel viewModel, ChunkBlockGrid chunkDisplays) : base(viewModel, chunkDisplays)
        {
            usesAura = false; //Only the hovered tile is highlighted.
        }

        public override void BlockInstance_MouseLeftClick(ushort offset, ushort chunk)
        {
            base.BlockInstance_MouseLeftClick(offset, chunk);
            if (viewModel.SelectedBlock == null) return; //Nothing to fill with.

            var target = chunkDisplays.GetBlockInstance(offset, chunk);
            if (target == null || target.publicBlockID == AIR_ID) return; //Clicking an empty block does nothing.

            chunkDisplays.TileAura_SetBlock(FloodRegion(offset, chunk, target.publicBlockID), viewModel.currentBlockInstance, chunk);
        }

        //Every tile reachable from start through NSEW neighbours that have the same block ID.
        private ushort[] FloodRegion(ushort start, ushort chunk, uint blockID)
        {
            var region = new List<ushort>();
            var visited = new bool[Chunk.X_DIMENSION * Chunk.Z_DIMENSION];
            var queue = new Queue<ushort>();
            queue.Enqueue(start);
            visited[start] = true;

            void TryAdd(int x, int z)
            {
                if (x < 0 || x >= Chunk.X_DIMENSION || z < 0 || z >= Chunk.Z_DIMENSION) return;
                var offset = (ushort)(x + z * Chunk.X_DIMENSION);
                if (visited[offset]) return;
                visited[offset] = true;
                if (chunkDisplays.GetBlockInstance(offset, chunk).publicBlockID == blockID) queue.Enqueue(offset);
            }

            while (queue.Count > 0)
            {
                var offset = queue.Dequeue();
                region.Add(offset);
                int x = offset % Chunk.X_DIMENSION;
                int z = offset / Chunk.X_DIMENSION;
                TryAdd(x, z - 1); //North
                TryAdd(x, z + 1); //South
                TryAdd(x + 1, z); //East
                TryAdd(x - 1, z); //West
            }
            return region.ToArray();
        }
    }
}
