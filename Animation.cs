using DieselEngineFormats.Utils;
using DieselEngineFormats.ZLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DieselEngineFormats
{
    //Some things I wrote might be a little weird as I didn't deal with binary things as much.
    //A little messy too

    //Structure: https://wiki.modworkshop.net/books/payday-2/page/animation-format Thanks Cpone for further research.

    public class AnimationSoundCue //Assumption, please confirm
    {
        public string Name;
        public float Frame; //To be confirmed.

        public AnimationSoundCue(string Name, float Frame)
        {
            Console.WriteLine("Sound cue object name " + Name);
            this.Name = Name;
            this.Frame = Frame;
        }
    }

    public class AnimPosition
    {
        public float Seconds;
        public short X;
        public short Y;
        public short Z;
        public short Unknown;

        public AnimPosition(float? seconds = null, short? x = null, short? y = null, short? z = null, short? unknown = null)
        {
            Seconds = seconds ?? 0f;
            X = x ?? 0;
            Y = y ?? 0;
            Z = z ?? 0;
            Unknown = unknown ?? 0;
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(Seconds);
            bw.Write(X);
            bw.Write(Y);
            bw.Write(Z);
            bw.Write(Unknown);
        }
    }

    public class AnimaRotation : Quaternion
    {
        public float currentFrame;
        public AnimaRotation(float? frame = null, float? x = null, float? y = null, float? z = null, float? w = null) : base(x, y, z, w)
        {
            currentFrame = frame ?? 0f;
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write(currentFrame);
            bw.Write(X);
            bw.Write(Y);
            bw.Write(Z);
            bw.Write(W);
        }
    }

    public class AnimationObject
    {
        public string Name;
        public uint positionFrames;
        public uint rotationFrames;

        public List<AnimPosition> Positions;
        public List<AnimaRotation> Rotations;

        public AnimationObject()
        {
            Positions = new List<AnimPosition>();
            Rotations = new List<AnimaRotation>();
        }

        public AnimationObject(string Name) : this()
        {
            Console.WriteLine("Animation object name " + Name);
            this.Name = Name;
        }
    }

    public class Animation
    {
        public List<AnimationObject> Objects;
        public List<AnimationSoundCue> soundCues;

        public uint Length;

        uint startPos = 0;

        uint objectsNum;
        uint objectNamesOffsetsPos;
        uint objectNamesPos;

        uint soundCuesNum;
        uint soundCueNamesOffsetsPos;
        uint soundCueNamesPos;

        uint objectPositionsNum;
        uint objectPositionsOffsetsPos;
        uint objectPositionsPos;

        uint objectRotationsNum;
        uint objectRotationsOffsetsPos;
        uint objectRotationsPos;

        public Animation(string filePath) => Read(filePath);
        public Animation(FileStream fs) => Read(fs);

        public AnimationObject GetObjectByName(string Name)
        {
            foreach (AnimationObject animObject in Objects)
            {
                if (animObject.Name.Equals(Name))
                    return animObject;
            }

            return null;
        }

        public void Read(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            Read(fs);
        }

        public void Read(Stream stream)
        {
            using MemoryStream ms = new MemoryStream();
            General.ZLibDecompress(stream, ms);
            Console.WriteLine("Memory stream length " + ms.Length);
            using var br = new BinaryReader(ms);
            ReadRaw(br);
        }

        public void Write(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            Write(fs);
        }

        public void Write(Stream stream)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            WriteRaw(bw);
            General.ZLibCompress(ms, stream);
        }

        public void WriteRaw(BinaryWriter bw)
        {
            Console.WriteLine("Writing animation..");

            //Start
            //Row 1
            bw.Write(0x0883CC85);
            bw.Write((ulong)0);
            bw.Write(0); //Might be ignored by zlib compression or pd2 ignores it, adding the size to the last 4 bytes after compressing seems to make it work.

            //Row 2
            bw.Write(Length);
            bw.Write(Objects.Count);
            bw.Write(60); //Offset
            bw.Write(0); //Amount of unknown

            //Row 3
            bw.Write(0); //Unknown offset
            bw.Write(soundCues.Count); //Amount sound cue names offsets
            bw.Write(0); //Sound cue names offset
            bw.Write(Objects.Count); //Amount

            //Row 4
            bw.Write(0); //Offset
            bw.Write(Objects.Count); //Amount
            bw.Write(0); //Offset

            Console.WriteLine(Objects.Count);

            long lastObjectNameOffsetPos = bw.BaseStream.Position;
            for (int i = 0; i < Objects.Count; i++)
            {
                bw.Write(0); //Offset
            }

            long lastObjectNamePos = bw.BaseStream.Position;

            //Object names offsets + object names

            for (int i = 0; i < Objects.Count; i++)
            {
                bw.BaseStream.Seek(lastObjectNamePos, SeekOrigin.Begin);

                string name = Objects[i].Name;
                Console.WriteLine("Writing name '" + name + "'");
                for (int x = 0; x < name.Length; x++) { 
                    bw.Write(name[x]);
                }
                bw.Write((byte)0);

                long nextObjectNamePos = bw.BaseStream.Position;

                bw.BaseStream.Seek(lastObjectNameOffsetPos, SeekOrigin.Begin);
                bw.Write((uint)lastObjectNamePos);

                lastObjectNamePos = nextObjectNamePos;
                lastObjectNameOffsetPos = bw.BaseStream.Position;
            }


            bw.BaseStream.Seek(lastObjectNamePos, SeekOrigin.Begin);
            
            //Small thing for the animation to be almost the same as original
            long posCheck = (lastObjectNamePos % 4);
            if(posCheck > 0)
            {
                for (int i = 0; i < 4 - posCheck; i++)
                {
                    bw.Write((byte)0);
                }
            }

            //Sound cue names offsets + sound cue names

            if (soundCues.Count > 0)
            {
                long soundCuesPos = bw.BaseStream.Position;
                long lastSoundCueNameOffsetPos = soundCuesPos + 4;
                bw.BaseStream.Seek(40, SeekOrigin.Begin);
                bw.Write((uint)soundCuesPos);
                bw.BaseStream.Seek(soundCuesPos, SeekOrigin.Begin);

                for (int i = 0; i < soundCues.Count; i++)
                {
                    bw.Write(soundCues[i].Frame);
                    bw.Write(0); //Offset
                }


                long lastSoundCueNamePos = bw.BaseStream.Position;

                for (int i = 0; i < soundCues.Count; i++)
                {
                    bw.BaseStream.Seek(lastSoundCueNamePos, SeekOrigin.Begin);

                    string name = soundCues[i].Name;
                    Console.WriteLine("Writing sound cue name '" + name + "'");
                    for (int x = 0; x < name.Length; x++)
                    {
                        bw.Write(name[x]);
                    }
                    bw.Write((byte)0);

                    long nextSoundCueNamePos = bw.BaseStream.Position;

                    bw.BaseStream.Seek(lastSoundCueNameOffsetPos, SeekOrigin.Begin);
                    bw.Write((uint)lastSoundCueNamePos);

                    lastSoundCueNamePos = nextSoundCueNamePos;
                    lastSoundCueNameOffsetPos = bw.BaseStream.Position + 4;
                }

                bw.BaseStream.Seek(lastSoundCueNamePos, SeekOrigin.Begin);

                posCheck = (lastSoundCueNamePos % 4);
                if (posCheck > 0)
                {
                    for (int i = 0; i < 4 - posCheck; i++)
                    {
                        bw.Write((byte)0);
                    }
                }
            }

            //Object positions offsets + object positions

            long positionsPos = bw.BaseStream.Position;
            long lastOffsetPos = positionsPos + 4;

            bw.BaseStream.Seek(48, SeekOrigin.Begin);
            bw.Write((uint)positionsPos);
            bw.BaseStream.Seek(positionsPos, SeekOrigin.Begin);

            for (int i = 0; i < Objects.Count; i++)
            {
                bw.Write(0x1DC686E0); //Unknown
                bw.Write(0); //Offset
            }

            long lastPos = bw.BaseStream.Position;

            for (int i = 0; i < Objects.Count; i++)
            {
                bw.BaseStream.Seek(lastPos, SeekOrigin.Begin);
                AnimationObject animObject = Objects[i];

                Console.WriteLine("Writing positions for " + animObject.Name);

                bw.Write(0); //Unknown / always 0.
                bw.Write(animObject.Positions.Count);
                bw.Write((uint)bw.BaseStream.Position + 4);
                for (int x = 0; x < animObject.Positions.Count; x++)
                {
                    Console.WriteLine("Position #" + x + 1 + " offset " + bw.BaseStream.Position);
                    animObject.Positions[x].Write(bw);
                }

                long nextPos = bw.BaseStream.Position;

                bw.BaseStream.Seek(lastOffsetPos, SeekOrigin.Begin);
                bw.Write((uint)lastPos);

                lastPos = nextPos;
                lastOffsetPos = bw.BaseStream.Position + 4;
            }

            bw.BaseStream.Seek(lastPos, SeekOrigin.Begin);

            //Object rotations offsets + object rotations

            long rotationsPos = bw.BaseStream.Position;
            lastOffsetPos = rotationsPos + 4;
            bw.BaseStream.Seek(56, SeekOrigin.Begin);
            bw.Write((uint)rotationsPos);
            bw.BaseStream.Seek(rotationsPos, SeekOrigin.Begin);

            for (int i = 0; i < Objects.Count; i++)
            {
                bw.Write(0x9DFB92B6); //Unknown
                bw.Write(0); //Offset
            }

            lastPos = bw.BaseStream.Position;

            for (int i = 0; i < Objects.Count; i++)
            {
                bw.BaseStream.Seek((uint)lastPos, SeekOrigin.Begin);
                AnimationObject animObject = Objects[i];

                Console.WriteLine("Writing rotations for " + animObject.Name);

                bw.Write(0); //Unknown / always 0.
                bw.Write(animObject.Rotations.Count);
                bw.Write((uint)bw.BaseStream.Position + 4);
                for (int x = 0; x < animObject.Rotations.Count; x++)
                {
                    Console.WriteLine("Rotation #" + x + 1 + " offset " + bw.BaseStream.Position);
                    animObject.Rotations[x].Write(bw);
                }

                long nextPos = bw.BaseStream.Position;

                bw.BaseStream.Seek(lastOffsetPos, SeekOrigin.Begin);
                bw.Write((uint)lastPos);

                lastPos = nextPos;
                lastOffsetPos = bw.BaseStream.Position + 4;
            }

            bw.BaseStream.Seek(lastPos, SeekOrigin.Begin);
        }
        public void ReadRaw(BinaryReader br)
        {
            br.BaseStream.Seek(0, SeekOrigin.Begin);

            Objects = new List<AnimationObject>();
            soundCues = new List<AnimationSoundCue>();

            //--Start

            //Row 1
            br.ReadUInt32(); //Unknown(mostly 85 CC 83 08) -
            br.ReadUInt64(); //Unknown(mostly 0) -
            br.ReadUInt32(); //File size

            //Row 2
            Length = br.ReadUInt32();
            objectsNum = br.ReadUInt32(); //Object num
            objectNamesOffsetsPos = br.ReadUInt32(); //Offset of object names offsets
            br.ReadUInt32(); //Unknown / amount of something located in the offset defined in the first 4 bytes of row 3(like sound cues) 

            //Row 3
            br.ReadUInt32(); //Offset of unknown(can be 0 if the amount defined in last 4 bytes of row 2 are 0)
            soundCuesNum = br.ReadUInt32();
            soundCueNamesOffsetsPos = br.ReadUInt32(); //Offset of offsets of sound cue names(contains some unknown floats also)
            objectPositionsNum = br.ReadUInt32(); //Object num / amount of object positions offsets.

            //Row 4
            objectPositionsOffsetsPos = br.ReadUInt32(); //Offset of object positions offsets
            objectRotationsNum = br.ReadUInt32(); //Objects num / amount of object rotations offsets.
            objectRotationsOffsetsPos = br.ReadUInt32(); //Offset of Object rotations offsets 

            //--Object names offsets
            //each 4 bytes is an unsigned int.
            br.BaseStream.Seek(objectNamesOffsetsPos, SeekOrigin.Begin);
            for (int i = 0; i < objectsNum; i++)
            {
                uint namePos = br.ReadUInt32();
                if(namePos != 0)
                {
                    if (i == 0)
                        objectNamesPos = namePos;

                    long oldPos = br.BaseStream.Position;
                    br.BaseStream.Seek(namePos, SeekOrigin.Begin);

                    List<char> s = new List<char>();
                    char c = ' ';
                    while (c != '\0')
                    {
                        c = br.ReadChar();
                        if(c != '\0')
                            s.Add(c);
                    }
                    Objects.Add(new AnimationObject(new string(s.ToArray())));

                    br.BaseStream.Seek(oldPos, SeekOrigin.Begin);
                }
            }

            //--Object names
            //pretty readable, separated by 00.

            if(soundCuesNum > 0)
            {
                br.BaseStream.Seek(soundCueNamesOffsetsPos, SeekOrigin.Begin);
                for (int i = 0; i < soundCuesNum; i++)
                {
                    float frame = br.ReadSingle();
                    uint namePos = br.ReadUInt32();
                    if(namePos != 0)
                    {
                        if (i == 0)
                            soundCueNamesPos = namePos;

                        long oldPos = br.BaseStream.Position;
                        br.BaseStream.Seek(namePos, SeekOrigin.Begin);

                        List<char> s = new List<char>();
                        char c = ' ';
                        while (c != '\0')
                        {
                            c = br.ReadChar();
                            if (c != '\0')
                                s.Add(c);
                        }
                        soundCues.Add(new AnimationSoundCue(new string(s.ToArray()), frame));
                        br.BaseStream.Seek(oldPos, SeekOrigin.Begin);
                    }
                }

                //--Sound cue names offset
                br.ReadSingle(); // Unknown float
                soundCueNamesPos = br.ReadUInt32();

                //--Sound cue names
                //Pretty much the same as object names
            }

            //--Object positions offsets
            br.BaseStream.Seek(objectPositionsOffsetsPos, SeekOrigin.Begin);
            for (int i = 0; i < objectPositionsNum; i++)
            {
                br.ReadUInt32(); //Unknown / always E0 86 C6 1D
                uint vec3Pos = br.ReadUInt32();
                if (vec3Pos != 0)
                {
                    if (i == 0)
                        objectPositionsPos = vec3Pos;

                    AnimationObject animObj = Objects[i];

                    long oldPos = br.BaseStream.Position;
                    br.BaseStream.Seek(vec3Pos, SeekOrigin.Begin);

                    br.ReadUInt32(); //Unknown / always 0.
                    animObj.positionFrames = br.ReadUInt32();
                    br.BaseStream.Seek(br.ReadUInt32(), SeekOrigin.Begin); //It always points to the next 4 bytes but whatever.
                    //The first 4 bytes of each frame is 100% a growing float which is probably the frame but the othet two? no idea.
                    
                    for (int x = 0; x < animObj.positionFrames; x++)
                    {
                        animObj.Positions.Add(new AnimPosition(br.ReadSingle(), br.ReadInt16(), br.ReadInt16(), br.ReadInt16(), br.ReadInt16()));
                    }

                    br.BaseStream.Seek(oldPos, SeekOrigin.Begin);
                }
            }

            //--Offsets of object rotations
            br.BaseStream.Seek(objectRotationsOffsetsPos, SeekOrigin.Begin);
            for (int i = 0; i < objectRotationsNum; i++)
            {
                br.ReadUInt32(); //Unknown / always B6 92 FB 9D or 5C B8 EC 96
                uint rotPos = br.ReadUInt32();
                if (rotPos != 0)
                {
                    if (i == 0)
                        objectRotationsPos = rotPos;

                    AnimationObject animObj = Objects[i];

                    long oldPos = br.BaseStream.Position;
                    br.BaseStream.Seek(rotPos, SeekOrigin.Begin);

                    br.ReadUInt32(); //Unknown / always 0.
                    animObj.rotationFrames = br.ReadUInt32();
                    br.BaseStream.Seek(br.ReadUInt32(), SeekOrigin.Begin);
                    for (int x = 0; x < animObj.rotationFrames; x++)
                    {
                        animObj.Rotations.Add(new AnimaRotation(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                    }

                    br.BaseStream.Seek(oldPos, SeekOrigin.Begin);
                }
            }
        }
    }
}