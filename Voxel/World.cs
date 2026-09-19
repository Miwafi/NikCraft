using System.Collections.Concurrent;
using NikCraft.Core;
using OpenTK.Mathematics;

namespace NikCraft.Voxel;

/// <summary>
/// Holds every loaded chunk and drives the background pipeline:
/// terrain generation -> mesh building -> upload to the GPU (main thread only).
/// </summary>
public sealed class World : IDisposable
{
    private readonly struct WorkItem
    {
        public readonly int ChunkX;
        public readonly int ChunkZ;
        public readonly bool Generate;

        public WorkItem(int chunkX, int chunkZ, bool generate)
        {
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            Generate = generate;
        }
    }

    private sealed class MeshUpload
    {
        public Chunk Chunk = null!;
        public ChunkMeshData Data = null!;
        public int Version;
    }

    private readonly ConcurrentDictionary<long, Chunk> _chunks = new();
    private readonly WorldGenerator _generator;

    private readonly ConcurrentQueue<WorkItem> _generateQueue = new();
    private readonly ConcurrentQueue<Chunk> _rebuildQueue = new();
    private readonly ConcurrentQueue<MeshUpload> _uploadQueue = new();
    private readonly ConcurrentDictionary<long, byte> _queuedCoords = new();

    private readonly Thread[] _workers;
    private volatile bool _running = true;
    private int _unloadTimer;

    public int Seed { get; }
    public int RenderDistance { get; set; } = 8;
    public int LoadedChunkCount => _chunks.Count;
    public int PendingWorkCount => _generateQueue.Count + _rebuildQueue.Count + _uploadQueue.Count;
    public int UploadBacklog => _uploadQueue.Count;

    public World(int seed, int renderDistance = 8)
    {
        Seed = seed;
        RenderDistance = renderDistance;
        _generator = new WorldGenerator(seed);

        int workerCount = Math.Clamp(Environment.ProcessorCount - 1, 1, 6);
        _workers = new Thread[workerCount];
        for (int i = 0; i < workerCount; i++)
        {
            var thread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = $"NikCraft-Worker-{i}",
                Priority = ThreadPriority.BelowNormal,
            };
            _workers[i] = thread;
            thread.Start();
        }
    }

    private static long Key(int chunkX, int chunkZ) => ((long)chunkX << 32) ^ (uint)chunkZ;

    public Chunk? GetChunk(int chunkX, int chunkZ) =>
        _chunks.TryGetValue(Key(chunkX, chunkZ), out Chunk? chunk) ? chunk : null;

    public IEnumerable<Chunk> LoadedChunks => _chunks.Values;

    public Biome GetBiomeAt(int worldX, int worldZ) => _generator.GetBiome(worldX, worldZ);

    public BlockType GetBlock(int worldX, int worldY, int worldZ)
    {
        if ((uint)worldY >= Chunk.SizeY)
        {
            return BlockType.Air;
        }

        Chunk? chunk = GetChunk(worldX >> 4, worldZ >> 4);
        if (chunk is null || !chunk.IsGenerated)
        {
            return BlockType.Air;
        }

        return chunk.GetBlock(worldX & 15, worldY, worldZ & 15);
    }

    public bool SetBlock(int worldX, int worldY, int worldZ, BlockType type)
    {
        if ((uint)worldY >= Chunk.SizeY)
        {
            return false;
        }

        int chunkX = worldX >> 4;
        int chunkZ = worldZ >> 4;

        Chunk? chunk = GetChunk(chunkX, chunkZ);
        if (chunk is null || !chunk.IsGenerated)
        {
            return false;
        }

        int localX = worldX & 15;
        int localZ = worldZ & 15;

        if (chunk.GetBlock(localX, worldY, localZ) == type)
        {
            return false;
        }

        chunk.SetBlock(localX, worldY, localZ, type);
        Interlocked.Increment(ref chunk.Version);

        MarkMeshDirty(chunk);

        // Only edits touching a chunk border can affect the neighbouring mesh (ambient occlusion samples x+-1/z+-1).
        if (localX == 0 || localX == Chunk.SizeX - 1 || localZ == 0 || localZ == Chunk.SizeZ - 1)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                for (int oz = -1; oz <= 1; oz++)
                {
                    if (ox == 0 && oz == 0)
                    {
                        continue;
                    }

                    Chunk? neighbour = GetChunk(chunkX + ox, chunkZ + oz);
                    if (neighbour is not null && neighbour.IsGenerated)
                    {
                        MarkMeshDirty(neighbour);
                    }
                }
            }
        }

        return true;
    }

    public void MarkMeshDirty(Chunk chunk)
    {
        chunk.MeshDirty = true;
        if (Interlocked.CompareExchange(ref chunk.MeshQueued, 1, 0) == 0)
        {
            _rebuildQueue.Enqueue(chunk);
        }
    }

    private void MarkMeshDirtyAt(int worldX, int worldZ)
    {
        Chunk? chunk = GetChunk(worldX >> 4, worldZ >> 4);
        if (chunk is not null && chunk.IsGenerated)
        {
            MarkMeshDirty(chunk);
        }
    }

    // ------------------------------------------------------------------ main thread update

    public void Update(Vector3 playerPosition)
    {
        int playerChunkX = (int)MathF.Floor(playerPosition.X) >> 4;
        int playerChunkZ = (int)MathF.Floor(playerPosition.Z) >> 4;

        QueueMissingChunks(playerChunkX, playerChunkZ);
        UploadFinishedMeshes();
        MaybeUnload(playerChunkX, playerChunkZ);
    }

    private void QueueMissingChunks(int playerChunkX, int playerChunkZ)
    {
        int radius = RenderDistance;
        int radiusSquared = radius * radius;

        for (int dz = -radius; dz <= radius; dz++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if ((dx * dx) + (dz * dz) > radiusSquared)
                {
                    continue;
                }

                int chunkX = playerChunkX + dx;
                int chunkZ = playerChunkZ + dz;
                long key = Key(chunkX, chunkZ);

                if (_chunks.ContainsKey(key))
                {
                    continue;
                }

                if (_queuedCoords.TryAdd(key, 1))
                {
                    _generateQueue.Enqueue(new WorkItem(chunkX, chunkZ, true));
                }
            }
        }
    }

    private void UploadFinishedMeshes()
    {
        // Bound the work per frame so big VBO uploads never cause a visible hitch.
        int budget = 6;

        while (budget-- > 0 && _uploadQueue.TryDequeue(out MeshUpload? upload))
        {
            Chunk chunk = upload.Chunk;

            if (upload.Version != Volatile.Read(ref chunk.Version))
            {
                // The player edited the chunk while we were meshing: throw the result away and retry.
                MarkMeshDirty(chunk);
                continue;
            }

            chunk.OpaqueMesh.Upload(upload.Data.OpaqueVertices, upload.Data.OpaqueIndices);
            chunk.TransparentMesh.Upload(upload.Data.TransparentVertices, upload.Data.TransparentIndices);
            chunk.MeshedVersion = upload.Version;
            chunk.MeshDirty = false;
        }
    }

    private void MaybeUnload(int playerChunkX, int playerChunkZ)
    {
        if (++_unloadTimer < 45)
        {
            return;
        }

        _unloadTimer = 0;

        int limit = RenderDistance + 3;
        int limitSquared = limit * limit;

        foreach (var pair in _chunks)
        {
            Chunk chunk = pair.Value;
            int dx = chunk.ChunkX - playerChunkX;
            int dz = chunk.ChunkZ - playerChunkZ;

            if ((dx * dx) + (dz * dz) <= limitSquared)
            {
                continue;
            }

            if (_chunks.TryRemove(pair.Key, out Chunk? removed))
            {
                removed.OpaqueMesh.Dispose();
                removed.TransparentMesh.Dispose();
            }
        }
    }

    // ------------------------------------------------------------------ worker threads

    private void WorkerLoop()
    {
        while (_running)
        {
            Chunk? rebuild;
            if (_rebuildQueue.TryDequeue(out rebuild))
            {
                Interlocked.Exchange(ref rebuild.MeshQueued, 0);
                ProcessRebuild(rebuild);
                continue;
            }

            WorkItem item;
            if (_generateQueue.TryDequeue(out item))
            {
                if (item.Generate)
                {
                    ProcessGeneration(item);
                }
                else
                {
                    Chunk? chunk = GetChunk(item.ChunkX, item.ChunkZ);
                    if (chunk is not null && chunk.IsGenerated)
                    {
                        BuildAndQueueUpload(chunk);
                    }
                }

                continue;
            }

            Thread.Sleep(2);
        }
    }

    private void ProcessGeneration(WorkItem item)
    {
        _queuedCoords.TryRemove(Key(item.ChunkX, item.ChunkZ), out _);

        try
        {
            if (_chunks.ContainsKey(Key(item.ChunkX, item.ChunkZ)))
            {
                return;
            }

            var chunk = new Chunk(item.ChunkX, item.ChunkZ);
            _generator.GenerateChunk(chunk);
            chunk.IsGenerated = true;
            _chunks[Key(item.ChunkX, item.ChunkZ)] = chunk;

            // The fresh chunk and all of its neighbours must be re-meshed now that the border is known.
            MarkMeshDirty(chunk);
            for (int ox = -1; ox <= 1; ox++)
            {
                for (int oz = -1; oz <= 1; oz++)
                {
                    if (ox == 0 && oz == 0)
                    {
                        continue;
                    }

                    Chunk? neighbour = GetChunk(item.ChunkX + ox, item.ChunkZ + oz);
                    if (neighbour is not null && neighbour.IsGenerated)
                    {
                        MarkMeshDirty(neighbour);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Write($"[World] 生成区块 ({item.ChunkX},{item.ChunkZ}) 失败: {ex}");
            _queuedCoords.TryRemove(Key(item.ChunkX, item.ChunkZ), out _);
        }
    }

    private void ProcessRebuild(Chunk chunk)
    {
        if (!chunk.IsGenerated)
        {
            return;
        }

        BuildAndQueueUpload(chunk);
    }

    private void BuildAndQueueUpload(Chunk chunk)
    {
        try
        {
            int version = Volatile.Read(ref chunk.Version);
            ChunkMeshData data = ChunkMesher.Build(chunk, this);

            if (Volatile.Read(ref chunk.Version) != version)
            {
                MarkMeshDirty(chunk);
                return;
            }

            _uploadQueue.Enqueue(new MeshUpload
            {
                Chunk = chunk,
                Data = data,
                Version = version,
            });
        }
        catch (Exception ex)
        {
            Log.Write($"[World] 构建区块 ({chunk.ChunkX},{chunk.ChunkZ}) 网格失败: {ex}");
        }
    }

    // ------------------------------------------------------------------ teardown

    public void Dispose()
    {
        _running = false;

        foreach (Thread thread in _workers)
        {
            thread.Join(250);
        }

        foreach (Chunk chunk in _chunks.Values)
        {
            chunk.OpaqueMesh.Dispose();
            chunk.TransparentMesh.Dispose();
        }

        _chunks.Clear();
    }
}
