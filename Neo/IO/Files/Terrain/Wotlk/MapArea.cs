﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Neo.Editing;
using Neo.IO.Files.Models;
using Neo.Scene;
using Neo.Scene.Texture;
using OpenTK;
using SlimTK;
using Warcraft.WDT.Chunks;

namespace Neo.IO.Files.Terrain.Wotlk
{
	public struct ChunkInfo
    {
        public int Offset;
        public int Size;
    }

	public class MapArea : Terrain.MapArea
    {
        private List<ChunkInfo> mChunkInfos = new List<ChunkInfo>();
        private List<MapChunk> mChunks = new List<MapChunk>();
        private List<LoadedModel> mWmoInstances = new List<LoadedModel>();

        private Mhdr mHeader;
        private Dictionary<uint, DataChunk> mSaveChunks = new Dictionary<uint, DataChunk>();
        private Mcin[] mChunkOffsets = new Mcin[0];
        private Mddf[] mDoodadDefs = new Mddf[0];
        private int[] mDoodadNameIds = new int[0];
        private readonly List<string> mDoodadNames = new List<string>();

        private bool mWasChanged;

        public MapArea(string continent, int ix, int iy)
        {
	        this.Continent = continent;
	        this.IndexX = ix;
	        this.IndexY = iy;
        }

        public override void AddDoodadInstance(int uuid, string modelName, BoundingBox box, Vector3 position, Vector3 rotation, float scale)
        {
            var mmidValue = 0;
            var nameFound = false;
            foreach (var s in this.mDoodadNames)
            {
                if (string.Equals(s, modelName, StringComparison.InvariantCultureIgnoreCase))
                {
                    nameFound = true;
                    break;
                }

                mmidValue += s.Length + 1;
            }

            int mmidIndex;
            if (nameFound == false)
            {
                mmidValue = this.mDoodadNames.Sum(s => s.Length + 1);
                mmidIndex = this.mDoodadNameIds.Length;
                Array.Resize(ref this.mDoodadNameIds, this.mDoodadNameIds.Length + 1);
	            this.mDoodadNameIds[this.mDoodadNameIds.Length - 1] = mmidValue;
	            this.mDoodadNames.Add(modelName);
            }
            else
            {
                mmidIndex = -1;
                for (var i = 0; i < this.mDoodadNameIds.Length; ++i)
                {
                    if (this.mDoodadNameIds[i] == mmidValue)
                    {
                        mmidIndex = i;
                        break;
                    }
                }

                if (mmidIndex < 0)
                {
                    mmidIndex = this.mDoodadNameIds.Length;
                    Array.Resize(ref this.mDoodadNameIds, this.mDoodadNameIds.Length + 1);
	                this.mDoodadNameIds[this.mDoodadNameIds.Length - 1] = mmidValue;
                }
            }

            var mcrfValue = this.mDoodadDefs.Length;
            Array.Resize(ref this.mDoodadDefs, this.mDoodadDefs.Length + 1);
	        this.mDoodadDefs[this.mDoodadDefs.Length - 1] = new Mddf
            {
                Position = new Vector3(position.X, position.Z, position.Y),
                Mmid = mmidIndex,
                Flags = 0,
                Scale = (ushort)(scale * 1024),
                UniqueId = uuid,
                Rotation = new Vector3(-rotation.X, 90 - rotation.Z, -rotation.Y)
            };

            var instance = WorldFrame.Instance.M2Manager.AddInstance(modelName, uuid, position, rotation,
                new Vector3(scale));

	        this.DoodadInstances.Add(new M2Instance
            {
                Hash = modelName.ToUpperInvariant().GetHashCode(),
                Uuid = uuid,
                BoundingBox = (instance != null ? instance.BoundingBox : new BoundingBox(new Vector3(float.MaxValue), new Vector3(float.MinValue))),
                RenderInstance = instance,
                MddfIndex = this.mDoodadDefs.Length - 1
            });

            foreach (var chunk in this.mChunks)
            {
	            if (chunk.BoundingBox.Contains(position) == ContainmentType.Contains)
	            {
		            chunk.AddDoodad(mcrfValue, box);
	            }
            }

	        this.mWasChanged = true;
        }

        public override void OnUpdateModelPositions(TerrainChangeParameters parameters)
        {
            var center = new Vector2(parameters.Center.X, parameters.Center.Y);
            foreach(var inst in this.DoodadInstances)
            {
                if (inst == null || inst.RenderInstance == null)
                {
	                continue;
                }

	            var pos = this.mDoodadDefs[inst.MddfIndex].Position;
                var old_pos = pos;
                var dist = (new Vector2(pos.X, pos.Z) - center).Length;
                if (dist > parameters.OuterRadius)
                {
	                continue;
                }

	            if(WorldFrame.Instance.MapManager.GetLandHeight(pos.X, pos.Z, out pos.Y))
                {
	                this.mDoodadDefs[inst.MddfIndex].Position = pos;
                    inst.RenderInstance.SetPosition(new Vector3(0, 0, pos.Y - old_pos.Y));
                }
            }
        }

        public void UpdateBoundingBox(BoundingBox chunkBox)
        {
            var minPos = chunkBox.Minimum;
            var maxPos = chunkBox.Maximum;

            var omin = this.BoundingBox.Minimum;
            var omax = this.BoundingBox.Maximum;

            omin.X = Math.Min(omin.X, minPos.X);
            omin.Y = Math.Min(omin.Y, minPos.Y);
            omin.Z = Math.Min(omin.Z, minPos.Z);
            omax.X = Math.Max(omax.X, maxPos.X);
            omax.Y = Math.Max(omax.Y, maxPos.Y);
            omax.Z = Math.Max(omax.Z, maxPos.Z);

	        this.BoundingBox = new BoundingBox(omin, omax);
        }

        public void UpdateModelBox(BoundingBox chunkBox)
        {
            var minPos = chunkBox.Minimum;
            var maxPos = chunkBox.Maximum;

            var omin = this.ModelBox.Minimum;
            var omax = this.ModelBox.Maximum;

            omin.X = Math.Min(omin.X, minPos.X);
            omin.Y = Math.Min(omin.Y, minPos.Y);
            omin.Z = Math.Min(omin.Z, minPos.Z);
            omax.X = Math.Max(omax.X, maxPos.X);
            omax.Y = Math.Max(omax.Y, maxPos.Y);
            omax.Z = Math.Max(omax.Z, maxPos.Z);

	        this.ModelBox = new BoundingBox(omin, omax);
        }

        public void UpdateVertices(MapChunk chunk)
        {
            if (chunk == null)
            {
	            return;
            }

	        var ix = chunk.IndexX;
            var iy = chunk.IndexY;

            var index = (ix + iy * 16) * 145;
            for (var i = 0; i < 145; ++i)
            {
	            this.FullVertices[i + index] = chunk.Vertices[i];
            }
        }

        public override void Save()
        {
            if (this.mWasChanged == false)
            {
	            return;
            }

	        var hasMccv = this.mChunks.Any(c => c != null && c.HasMccv);
            if(hasMccv)
            {
                var wdt = WorldFrame.Instance.MapManager.CurrentWdt;
                if(!wdt.Header.Flags.HasFlag(WorldTableFlags.UsesVertexShading))
                {
                    wdt.Header.Flags |= WorldTableFlags.UsesVertexShading;
	                // TODO: Where and how?
                    // wdt.Save(WorldFrame.Instance.MapManager.Continent);
                }
            }

            using (var strm = FileManager.Instance.GetOutputStream(string.Format(@"World\Maps\{0}\{0}_{1}_{2}.adt", this.Continent, this.IndexX, this.IndexY)))
            {
                var writer = new BinaryWriter(strm);
                writer.Write(0x4D564552); // MVER
                writer.Write(4);
                writer.Write(18);
                writer.Write(0x4D484452);
                writer.Write(SizeCache<Mhdr>.Size);

                var headerStart = writer.BaseStream.Position;
                writer.Write(this.mHeader);
                var header = this.mHeader;

                var chunkInfos = this.mChunkOffsets.ToArray();
                writer.Write(0x4D43494E);
                writer.Write(256 * SizeCache<Mcin>.Size);
                var mcinStart = writer.BaseStream.Position;
                writer.WriteArray(chunkInfos);

                header.ofsMcin = (int)(mcinStart - 28);

                header.ofsMtex = (int) (writer.BaseStream.Position - 20);
                writer.Write(0x4D544558);
                var textureData = new List<byte>();
                writer.Write(this.TextureNames.Sum(t =>
                {
                    var data = Encoding.ASCII.GetBytes(t);
                    textureData.AddRange(data);
                    textureData.Add(0);
                    return data.Length + 1;
                }));
                writer.Write(textureData.ToArray());

                header.ofsMmdx = (int) (writer.BaseStream.Position - 20);
                var m2NameData = new List<byte>();
                writer.Write(0x4D4D4458);
                writer.Write(this.mDoodadNames.Sum(t =>
                {
                    var data = Encoding.ASCII.GetBytes(t);
                    m2NameData.AddRange(data);
                    m2NameData.Add(0);
                    return data.Length + 1;
                }));
                writer.Write(m2NameData.ToArray());

                header.ofsMmid = (int) (writer.BaseStream.Position - 20);
                writer.Write(0x4D4D4944);
                writer.Write(this.mDoodadNameIds.Length * 4);
                writer.WriteArray(this.mDoodadNameIds);

                SaveChunk(0x4D574D4F, writer, out header.ofsMwmo);
                SaveChunk(0x4D574944, writer, out header.ofsMwid);

                if (this.mDoodadDefs != null && this.mDoodadDefs.Length > 0)
                {
                    header.ofsMddf = (int)(writer.BaseStream.Position - 20);
                    writer.Write(0x4D444446);
                    writer.Write(this.mDoodadDefs.Length * SizeCache<Mddf>.Size);
                    writer.WriteArray(this.mDoodadDefs);
                }
                else
                {
	                header.ofsMddf = 0;
                }

	            SaveChunk(0x4D4F4446, writer, out header.ofsModf);
                SaveChunk(0x4D48324F, writer, out header.ofsMh2o);

                for (var i = 0; i < this.mChunks.Count; ++i)
                {
                    var startPos = writer.BaseStream.Position;
	                this.mChunks[i].SaveChunk(writer);
                    var endPos = writer.BaseStream.Position;
                    chunkInfos[i].OfsMcnk = (int)startPos;
                    chunkInfos[i].SizeMcnk = (int)(endPos - startPos);
                }

                SaveChunk(0x4D545846, writer, out header.ofsMtxf);
                SaveChunk(0x4D46424F, writer, out header.ofsMfbo);

                writer.BaseStream.Position = headerStart;
                writer.Write(header);
                writer.BaseStream.Position = mcinStart;
                writer.WriteArray(chunkInfos);
            }
        }

        public override void AsyncLoad()
        {
            using (var file = FileManager.Instance.Provider.OpenFile(string.Format(@"World\Maps\{0}\{0}_{1}_{2}.adt", this.Continent, this.IndexX, this.IndexY)))
            {
                if (file == null)
                {
	                this.IsValid = false;
                    return;
                }

                var reader = new BinaryReader(file);
                reader.BaseStream.Position = 0;
                while(reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    var signature = reader.ReadUInt32();
                    var size = reader.ReadInt32();
                    if (reader.BaseStream.Position + size > reader.BaseStream.Length)
                    {
	                    break;
                    }

	                var bytes = reader.ReadBytes(size);
                    if (this.mSaveChunks.ContainsKey(signature))
                    {
	                    continue;
                    }

	                this.mSaveChunks.Add(signature, new DataChunk {Data = bytes, Signature = signature, Size = size});
                }

                reader.BaseStream.Position = 0;
                if (SeekChunk(reader, 0x4D484452) == false)
                {
	                throw new InvalidOperationException("ADT has no header chunk");
                }

	            reader.ReadInt32();
	            this.mHeader = reader.Read<Mhdr>();
                InitChunkInfos(reader);
                InitTextures(reader);
                InitM2Models(reader);
                InitWmoModels(reader);
                InitChunks(reader);
            }
        }

        public override Terrain.MapChunk GetChunk(int index)
        {
            if (index >= this.mChunks.Count)
            {
	            throw new IndexOutOfRangeException();
            }

	        return this.mChunks[index];
        }

        public override bool Intersect(ref Ray ray, out Terrain.MapChunk chunk, out float distance)
        {
            distance = float.MaxValue;
            chunk = null;

            var mindistance = float.MaxValue;
            if (this.BoundingBox.Intersects(ref ray) == false)
            {
	            return false;
            }

	        Terrain.MapChunk chunkHit = null;
            var hasHit = false;
            foreach (var cnk in this.mChunks)
            {
                float dist;
                if (cnk.Intersect(ref ray, out dist) == false)
                {
	                continue;
                }

	            hasHit = true;
                if (dist >= mindistance)
                {
	                continue;
                }

	            mindistance = dist;
                chunkHit = cnk;
            }

            chunk = chunkHit;
            distance = mindistance;
            return hasHit;
        }

        public override bool OnChangeTerrain(TerrainChangeParameters parameters)
        {
            var changed = false;
            foreach (var chunk in this.mChunks)
            {
                if (chunk == null)
                {
	                continue;
                }

	            if (chunk.OnTerrainChange(parameters))
	            {
		            changed = true;
	            }
            }

            if (changed)
            {
	            this.mWasChanged = true;
            }

	        return changed;
        }

        public override void UpdateNormals()
        {
            foreach (var chunk in this.mChunks)
            {
                if (chunk == null)
                {
	                continue;
                }

	            chunk.UpdateNormals();
            }
        }

        public override bool OnTextureTerrain(TextureChangeParameters parameters)
        {
            var changed = false;
            foreach (var chunk in this.mChunks)
            {
                if (chunk == null)
                {
	                continue;
                }

	            if (chunk.OnTextureTerrain(parameters))
	            {
		            changed = true;
	            }
            }

            if (changed)
            {
	            this.mWasChanged = true;
            }

	        return changed;
        }

        private void InitChunkInfos(BinaryReader reader)
        {
            if (SeekChunk(reader, 0x4D43494E))
            {
                reader.ReadInt32();
	            this.mChunkOffsets = reader.ReadArray<Mcin>(256);
            }

            reader.BaseStream.Position = 0;
            for (var i = 0; i < 256; ++i)
            {
                if (SeekNextMcnk(reader) == false)
                {
	                throw new InvalidOperationException("Area is missing chunks");
                }

	            this.mChunkInfos.Add(new ChunkInfo
                {
                    Offset = (int)(reader.BaseStream.Position),
                    Size = reader.ReadInt32()
                });

                reader.ReadBytes(this.mChunkInfos.Last().Size);
            }
        }

        private void InitTextures(BinaryReader reader)
        {
            if (SeekChunk(reader, 0x4D544558) == false)
            {
	            return;
            }

	        var size = reader.ReadInt32();
            var bytes = reader.ReadBytes(size);
            var fullString = Encoding.ASCII.GetString(bytes);
	        this.TextureNames.AddRange(fullString.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries));
            for (var i = 0; i < this.TextureNames.Count; ++i)
            {
	            this.mTextures.Add(TextureManager.Instance.GetTexture(this.TextureNames[i]));
	            this.TextureNames[i] = this.TextureNames[i].ToLowerInvariant();
            }

            LoadSpecularTextures();
        }

        private void InitChunks(BinaryReader reader)
        {
            var minPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var maxPos = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            var modelMin = new Vector3(float.MaxValue);
            var modelMax = new Vector3(float.MinValue);

            for (var i = 0; i < 256; ++i)
            {
                var chunk = new MapChunk(i % 16, i / 16, new WeakReference<MapArea>(this));
                if (chunk.AsyncLoad(reader, this.mChunkInfos[i]) == false)
                {
	                throw new InvalidOperationException("Unable to load chunk");
                }

	            var bbmin = chunk.BoundingBox.Minimum;
                var bbmax = chunk.BoundingBox.Maximum;
                if (bbmin.X < minPos.X)
                {
	                minPos.X = bbmin.X;
                }
	            if (bbmax.X > maxPos.X)
	            {
		            maxPos.X = bbmax.X;
	            }
	            if (bbmin.Y < minPos.Y)
	            {
		            minPos.Y = bbmin.Y;
	            }
	            if (bbmax.Y > maxPos.Y)
	            {
		            maxPos.Y = bbmax.Y;
	            }
	            if (bbmin.Z < minPos.Z)
	            {
		            minPos.Z = bbmin.Z;
	            }
	            if (bbmax.Z > maxPos.Z)
	            {
		            maxPos.Z = bbmax.Z;
	            }

	            bbmin = chunk.ModelBox.Minimum;
                bbmax = chunk.ModelBox.Maximum;
                if (bbmin.X < modelMin.X)
                {
	                modelMin.X = bbmin.X;
                }
	            if (bbmax.X > modelMax.X)
	            {
		            modelMax.X = bbmax.X;
	            }
	            if (bbmin.Y < modelMin.Y)
	            {
		            modelMin.Y = bbmin.Y;
	            }
	            if (bbmax.Y > modelMax.Y)
	            {
		            modelMax.Y = bbmax.Y;
	            }
	            if (bbmin.Z < modelMin.Z)
	            {
		            modelMin.Z = bbmin.Z;
	            }
	            if (bbmax.Z > modelMax.Z)
	            {
		            modelMax.Z = bbmax.Z;
	            }

	            this.mChunks.Add(chunk);
                Array.Copy(chunk.Vertices, 0, this.FullVertices, i * 145, 145);
            }

	        this.BoundingBox = new BoundingBox(minPos, maxPos);
	        this.ModelBox = new BoundingBox(modelMin, modelMax);
        }

        private void InitM2Models(BinaryReader reader)
        {
            if (SeekChunk(reader, 0x4D4D4458) == false)
            {
	            return;
            }

	        var size = reader.ReadInt32();
            var bytes = reader.ReadBytes(size);
            var fullString = Encoding.ASCII.GetString(bytes);
            var modelNames = fullString.Split('\0');
	        this.mDoodadNames.AddRange(modelNames.ToList());
            var modelNameLookup = new Dictionary<int, string>();
            var curOffset = 0;
            foreach (var name in modelNames)
            {
                modelNameLookup.Add(curOffset, name);
                curOffset += name.Length + 1;
            }

            if (SeekChunk(reader, 0x4D4D4944) == false)
            {
	            return;
            }

	        size = reader.ReadInt32();
	        this.mDoodadNameIds = reader.ReadArray<int>(size / 4);

            if (SeekChunk(reader, 0x4D444446) == false)
            {
	            return;
            }

	        size = reader.ReadInt32();
	        this.mDoodadDefs = reader.ReadArray<Mddf>(size / SizeCache<Mddf>.Size);

            var index = -1;
            foreach (var entry in this.mDoodadDefs)
            {
                ++index;
                if (entry.Mmid >= this.mDoodadNameIds.Length)
                {
	                continue;
                }

	            var nameId = this.mDoodadNameIds[entry.Mmid];
                string modelName;
                if (modelNameLookup.TryGetValue(nameId, out modelName) == false)
                {
	                continue;
                }

	            var position = new Vector3(entry.Position.X, entry.Position.Z, entry.Position.Y);
                var rotation = new Vector3(-entry.Rotation.X, -entry.Rotation.Z, 90 - entry.Rotation.Y);
                var scale = entry.Scale / 1024.0f;

                var instance = WorldFrame.Instance.M2Manager.AddInstance(modelName, entry.UniqueId, position, rotation,
                    new Vector3(scale));

	            this.DoodadInstances.Add(new M2Instance
                {
                    Hash = modelName.ToUpperInvariant().GetHashCode(),
                    Uuid = entry.UniqueId,
                    BoundingBox = (instance != null ? instance.BoundingBox : new BoundingBox(new Vector3(float.MaxValue), new Vector3(float.MinValue))),
                    RenderInstance = instance,
                    MddfIndex = index
                });
            }
        }

        private void InitWmoModels(BinaryReader reader)
        {
            if (SeekChunk(reader, 0x4D574D4F) == false)
            {
	            return;
            }

	        var size = reader.ReadInt32();
            var bytes = reader.ReadBytes(size);
            var modelNameLookup = new Dictionary<int, string>();
            var curOffset = 0;
            var curBytes = new List<byte>();

            for (var i = 0; i < bytes.Length; ++i)
            {
                if (bytes[i] == 0)
                {
                    if (curBytes.Count > 0)
                    {
	                    modelNameLookup.Add(curOffset, Encoding.ASCII.GetString(curBytes.ToArray()));
                    }

	                curOffset = i + 1;
                    curBytes.Clear();
                }
                else
                {
	                curBytes.Add(bytes[i]);
                }
            }

            if (SeekChunk(reader, 0x4D574944) == false)
            {
	            return;
            }

	        size = reader.ReadInt32();
            var modelNameIds = reader.ReadArray<int>(size / 4);

            if (SeekChunk(reader, 0x4D4F4446) == false)
            {
	            return;
            }

	        size = reader.ReadInt32();
            var modf = reader.ReadArray<Modf>(size / SizeCache<Modf>.Size);

            foreach (var entry in modf)
            {
                if (entry.Mwid >= modelNameIds.Length)
                {
	                continue;
                }

	            var nameId = modelNameIds[entry.Mwid];
                string modelName;
                if (modelNameLookup.TryGetValue(nameId, out modelName) == false)
                {
	                continue;
                }


	            var position = new Vector3(entry.Position.X, entry.Position.Z, entry.Position.Y);
                var rotation = new Vector3(entry.Rotation.Z, -entry.Rotation.X, 90 - entry.Rotation.Y);

                WorldFrame.Instance.WmoManager.AddInstance(modelName, entry.UniqueId, position, rotation);
	            this.mWmoInstances.Add(new LoadedModel(modelName, entry.UniqueId));
            }
        }

        private void SaveChunk(uint signature, BinaryWriter writer, out int offsetField)
        {
            if(this.mSaveChunks.ContainsKey(signature) == false)
            {
                offsetField = 0;
                return;
            }

            offsetField = (int)writer.BaseStream.Position - (12 + 8); // MVER signature + size + version = 12 bytes, MHDR signature + size = 8 bytes

            var chunk = this.mSaveChunks[signature];
            writer.Write(signature);
            writer.Write(chunk.Size);
            writer.Write(chunk.Data);
        }

        private static bool SeekNextMcnk(BinaryReader reader) { return SeekChunk(reader, 0x4D434E4B, false); }

        private static bool SeekChunk(BinaryReader reader, uint signature, bool begin = true)
        {
            if (begin)
            {
	            reader.BaseStream.Position = 0;
            }

	        try
            {
                var sig = reader.ReadUInt32();
                while (sig != signature)
                {
                    var size = reader.ReadInt32();
                    reader.ReadBytes(size);
                    sig = reader.ReadUInt32();
                }

                return sig == signature;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (this.mChunks != null)
            {
                foreach (var chunk in this.mChunks)
                {
	                chunk.Dispose();
                }

	            this.mChunks.Clear();
	            this.mChunks = null;
            }

            if (this.mWmoInstances != null)
            {
                foreach (var instance in this.mWmoInstances)
                {
	                WorldFrame.Instance.WmoManager.RemoveInstance(instance.FileName, instance.Uuid,false);
                }

	            this.mWmoInstances.Clear();
	            this.mWmoInstances = null;
            }

            if (this.mChunkInfos != null)
            {
	            this.mChunkInfos.Clear();
	            this.mChunkInfos = null;
            }

            if (this.mSaveChunks != null)
            {
	            this.mSaveChunks.Clear();
	            this.mSaveChunks = null;
            }

	        this.mChunkOffsets = null;
	        this.mDoodadDefs = null;

            base.Dispose(disposing);
        }

        public override void SetChanged()
        {
	        this.mWasChanged = true;
        }
    }
}