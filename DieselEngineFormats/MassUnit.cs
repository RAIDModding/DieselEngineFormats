using DieselEngineFormats.Bundle;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DieselEngineFormats
{
    public class Vector3
    {
        public float X { get; set; }

        public float Y { get; set; }

        public float Z { get; set; }

        public Vector3(float? x = null, float? y = null, float? z = null)
        {
            this.X = x ?? 0f;
            this.Y = y ?? 0f;
            this.Z = z ?? 0f;
        }

    }

    public class Quaternion
    {
        public float X;

        public float Y;

        public float Z;

        public float W;

        public Quaternion(float? x = null, float? y = null, float? z = null, float? w = null)
        {
            this.X = x ?? 0f;
            this.Y = y ?? 0f;
            this.Z = z ?? 0f;
            this.W = w ?? 0f;
        }
    }

    public class MassUnitHeader
    {
        public Idstring Unit;
        public uint Offset;
        public uint InstanceCount;
        public uint posLocPos;

        public List<Quaternion> Rotations;
        public List<Vector3> Positions;

        public MassUnitHeader(Idstring Unit, List<Vector3> Positions, List<Quaternion> Rotations)
        {
            this.Unit = Unit;
            this.Positions = Positions;
            this.Rotations = Rotations;
        }

        public MassUnitHeader(ulong Unit, List<Vector3> Positions, List<Quaternion> Rotations) : this(HashIndex.Get(Unit), Positions, Rotations)
        {
            
        }

        public MassUnitHeader(BinaryReader br)
        {
            Positions = new List<Vector3>();
            Rotations = new List<Quaternion>();

            ulong UnitPathHash = br.ReadUInt64();
            this.Unit = HashIndex.Get(UnitPathHash);
            br.ReadSingle(); // Visiblity count?
            this.InstanceCount = br.ReadUInt32();
            br.ReadUInt32(); // Unknown
            this.Offset = br.ReadUInt32();
            br.BaseStream.Seek(8, SeekOrigin.Current);
        }

        public void Write(BinaryWriter bw)
        {
            InstanceCount = (uint)Positions.Count;
            bw.Write(Unit.Hashed);
            bw.Write(InstanceCount); // Assuming this is some sort of visibility count.
            bw.Write(InstanceCount);
            bw.Write(0); // Unknown
            posLocPos = (uint)bw.BaseStream.Position;
            bw.Write(0); // Location of positions and rotations, should be updated by WritePositionד
            bw.Write(0); // Skip
            bw.Write(0);
        }

        public void WritePositions(BinaryWriter bw)
        {
            Offset = (uint)bw.BaseStream.Position;
            for (int i = 0; i < InstanceCount; i++)
            {
                Vector3 Pos = Positions[i];
                Quaternion Rot = Rotations[i];

                bw.Write(Pos.X);
                bw.Write(Pos.Y);
                bw.Write(Pos.Z);

                bw.Write(Rot.X);
                bw.Write(Rot.Y);
                bw.Write(Rot.Z);
                bw.Write(Rot.W);
            }
            long lastPos = bw.BaseStream.Position;
            bw.BaseStream.Seek(posLocPos, SeekOrigin.Begin);
            bw.Write(Offset);
            bw.BaseStream.Seek(lastPos, SeekOrigin.Begin);
        }

        public void ReadPositions(BinaryReader br)
        {
            br.BaseStream.Seek(Offset, SeekOrigin.Begin);
            for (int i = 0; i < InstanceCount; ++i)
            {
                Positions.Add(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                Rotations.Add(new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
            }
        }
    }
    
    public class MassUnit
    {
        List<MassUnitHeader> Headers;

        public MassUnit()
        {
            this.Headers = new List<MassUnitHeader>();
        }

        public MassUnit(List<MassUnitHeader> Headers)
        {
            this.Headers = Headers;
        }

        public MassUnit(string filePath) : this()
        {
            this.Load(filePath);
        }

        public MassUnit(Stream fileStream) : this()
        {
            using (BinaryReader br = new BinaryReader(fileStream))
                this.ReadFile(br);
        }

        public void WriteFile(string filepath)
        {
            using (var fs = new FileStream(filepath, FileMode.Create, FileAccess.Write))
                WriteFile(fs);
        }

        public void WriteFile(Stream fs)
        {
            using (var bw = new BinaryWriter(fs))
                this.WriteFile(bw);
        }

        public void WriteFile(BinaryWriter bw)
        {
            bw.Write(Headers.Count);
            bw.Write(0); // Unknown
            bw.Write(16);
            bw.Write(0); // Skip
            for (int i = 0; i < Headers.Count; i++)
                Headers[i].Write(bw);

            for (int i = 0; i < Headers.Count; i++)
                Headers[i].WritePositions(bw);
        }

        private void ReadFile(BinaryReader br)
        {
            uint unitCount = br.ReadUInt32();
            br.ReadUInt32(); // Unknown
            uint unitsOffset = br.ReadUInt32();
            br.BaseStream.Seek(unitsOffset, SeekOrigin.Begin);
            for (int i = 0; i < unitCount; ++i)
                Headers.Add(new MassUnitHeader(br));

            foreach (var header in Headers)
                header.ReadPositions(br);
        }

        public void Load(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                    this.ReadFile(br);
        }
    }
}
