using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;

namespace DieselEngineFormats.Font
{
    public class FontKerning
    {
        public int char1, char2;
        public byte u1, u2, u3, u4;

        public FontKerning(BinaryReader instream)
        {
            this.char1 = instream.ReadInt32();
            this.char2 = instream.ReadInt32();
            this.u1 = instream.ReadByte();
            this.u2 = instream.ReadByte();
            this.u3 = instream.ReadByte();
            this.u4 = instream.ReadByte();
        }

        public void WriteStream(BinaryWriter bw)
        {
            bw.Write(this.char1);
            bw.Write(this.char2);
            bw.Write(this.u1);
            bw.Write(this.u2);
            bw.Write(this.u3);
            bw.Write(this.u4);
        }
    }

    public class FontCharacter
    {
        public int ID { get; set; }

        public short X { get; set; }

        public short Y { get; set; }

        public byte W { get; set; }

        public byte H { get; set; }

        public byte XAdvance { get; set; }

        public sbyte XOffset { get; set; }

        public short YOffset { get; set; }

        public int ASCIICode { get; set; }

        public char Character { get { return (char)this.ASCIICode; } set { this.ASCIICode = (int)value; } }

        public object Tag { get; set; }

        public FontCharacter() {}

        public short ReadInt(BinaryReader br, bool large)
        {
            return large ? br.ReadInt16() : br.ReadSByte();
        }

        public void WriteInt(BinaryWriter bw, short i, bool large)
        {
            if (large)
                bw.Write(i);
            else
                bw.Write((sbyte)i);
        }

        public FontCharacter(BinaryReader br, bool large=false)
        {
            ReadInt(br, large); //Unknown
            this.W = br.ReadByte();
            this.H = br.ReadByte();
            this.XAdvance = br.ReadByte();
            this.XOffset = br.ReadSByte();
            this.YOffset = ReadInt(br, large); //Oddly the only one that turned into a short?
            this.X = br.ReadInt16();
            this.Y = br.ReadInt16();
        }

        public void WriteMainSection(BinaryWriter bw, bool large=false)
        {
            WriteInt(bw, 0, large);
            bw.Write(this.W);
            bw.Write(this.H);
            bw.Write(this.XAdvance);
            bw.Write(this.XOffset);
            WriteInt(bw, this.YOffset, large);
            bw.Write(this.X);
            bw.Write(this.Y);
        }

        public void WriteCodeIDSection(BinaryWriter bw)
        {
            bw.Write(this.ASCIICode);
            bw.Write(this.ID);
        }
    }

    public class DieselFont
    {
        public List<FontKerning> kerning = new List<FontKerning>();

        public bool HasKerning { get { return this.kerning.Count > 0; } }

        public int TextureWidth, TextureHeight;

        public string Name { get; set; }

        private const int EndTag = 925913978;

        public long u1, u2, u3, u5, Info_Size;

        public int u4, u7, Common_Base, LineHeight;

        private List<FontCharacter> _characters = new List<FontCharacter>();

        public List<FontCharacter> Characters { 
            get 
            {
                this._characters.Sort(delegate(FontCharacter x, FontCharacter y)
                {
                    return x.ASCIICode.CompareTo(y.ASCIICode);
                });
                return this._characters; 
            }
            set => this._characters = value;
        }

        public DieselFont() {}

        public DieselFont(XmlReader xmlReader)
        {
            this.ReadBMFontXmlFile(xmlReader);
        }

        public DieselFont(string filepath, bool large)
        {
            using (FileStream str = new FileStream(filepath, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader br = new BinaryReader(str))
                {
                    try
                    {
                        this.ReadDieselFile(br, large);
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine(exc.Message);
                    }
                }
            }
        }

        public DieselFont(Stream str, bool large)
        {
            using (BinaryReader br = new BinaryReader(str))
            {
                try
                {
                    this.ReadDieselFile(br, large);
                }
                catch (Exception exc)
                {
                    Console.WriteLine(exc.Message);
                }
            }
        }

        public DieselFont(BinaryReader br, bool large)
        {
            try
            {
                this.ReadDieselFile(br, large);
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
            }
        }
        
        #region BMFont
        public void ReadBMFontXmlFile(XmlReader readXML)
        {
            while (readXML.Read())
            {
                if (readXML.NodeType == XmlNodeType.Element)
                {
                    switch(readXML.Name)
                    {
                        case "info":
                            this.Name = readXML.GetAttribute("face");
                            this.Info_Size = long.Parse(readXML.GetAttribute("size"));
                            break;
                        case "common":
                            this.LineHeight = int.Parse(readXML.GetAttribute("lineHeight"));
                            this.Common_Base = short.Parse(readXML.GetAttribute("base"));
                            this.TextureWidth = int.Parse(readXML.GetAttribute("scaleW"));
                            this.TextureHeight = int.Parse(readXML.GetAttribute("scaleH"));
                            break;
                        case "char":
                            FontCharacter fontChar = new FontCharacter{
                                ASCIICode = int.Parse(readXML.GetAttribute("id")),
                                ID = this.Characters.Count,
                                X = short.Parse(readXML.GetAttribute("x")),
                                Y = short.Parse(readXML.GetAttribute("y")),
                                W = byte.Parse(readXML.GetAttribute("width")),
                                H = byte.Parse(readXML.GetAttribute("height")),
                                XOffset = sbyte.Parse(readXML.GetAttribute("xoffset")),
                                YOffset = short.Parse(readXML.GetAttribute("yoffset")),
                                XAdvance = byte.Parse(readXML.GetAttribute("xadvance")),
                            };
                            this.Characters.Add(fontChar);
                            break;
                    }
                }
            }
        }
        #endregion

        public int ReadShortOrInt(BinaryReader br, bool large)
        {
            return large ? br.ReadInt32() : br.ReadInt16();
        }

        public long ReadInt(BinaryReader br, bool large)
        {
            return large ? br.ReadInt64() : br.ReadInt32();
        }

        public void WriteInt(BinaryWriter bw, long i, bool large)
        {
            if (large)
                bw.Write(i);
            else
                bw.Write((int)i);
        }

        public void WriteInt(BinaryWriter bw, int i, bool large)
        {
            if (large)
                bw.Write(i);
            else
                bw.Write((short)i);
        }

        #region Diesel
        public void ReadDieselFile(BinaryReader br, bool large=false)
        {
            //Normal: 92 bytes large: 168 bytes

            //Some values in raid seem to be complete 0 like u1.

            br.BaseStream.Position = 0;

            long charCount = ReadInt(br, large);//Count
            ReadInt(br, large);//Count

            long fontRectPos = ReadInt(br, large);

            this.u1 = ReadInt(br, large);//Unknown(seems like in the case of system_font in raid it was turned into a zero and long lol)
            this.u2 = ReadInt(br, large);//Unknown

            ReadInt(br, large);//Count
            ReadInt(br, large);//Count

            long asciiCharsPos = ReadInt(br, large);

            ReadInt(br, large);//Unknown, same as pos 12 or 24 in raid.
            this.u3 = ReadInt(br, large);//Unknown
            this.u4 = ReadShortOrInt(br, large);//Unknown Short at end seems to be base
            this.Common_Base = ReadShortOrInt(br, large);

            long kernings = ReadInt(br, large);
            ReadInt(br, large);//Unknown (the count of kernings again?)

            long kerningPos = ReadInt(br, large);

            bool hasKerning = kerningPos != 0;

            ReadInt(br, large);//Unknown, same as pos 12
            this.u5 = ReadInt(br, large);//Unknown
            ReadInt(br, large);//Unknown, same as pos 12

            long endPos = ReadInt(br, large);

            long kernCount = (endPos - kerningPos) / 12;

            this.LineHeight = br.ReadInt32(); //Line Height

            this.TextureWidth = br.ReadInt32();

            this.TextureHeight = br.ReadInt32();

            this.u7 = br.ReadInt32(); //Sometimes the same as LineHeight

            this.Info_Size = ReadInt(br, large); //base?

            if (br.BaseStream.Position != fontRectPos)
            {
                Console.WriteLine("Position is not correct for Font Texture Rectangle by " + (br.BaseStream.Position - fontRectPos));
                br.BaseStream.Position = fontRectPos;
            }

            for (int i = 0; i < charCount; i++)
                this._characters.Add(new FontCharacter(br));

            if ((charCount % 2) != 0)
                br.ReadInt16();

            if (br.BaseStream.Position != asciiCharsPos)
            {
                Console.WriteLine("Position is not correct for ASCII Characters by " + (br.BaseStream.Position - asciiCharsPos));
                br.BaseStream.Position = asciiCharsPos;
            }

            for (int i = 0; i < charCount; i++)
            {
                int ASCIICode = br.ReadInt32();
                int ID = br.ReadInt32();
                this._characters[ID].ID = ID;
                this._characters[ID].ASCIICode = ASCIICode;
            }

            if (hasKerning)
                for (int i = 0; i < kernCount; i++)
                    this.kerning.Add(new FontKerning(br));

            if (br.BaseStream.Position != endPos)
            {
                Console.WriteLine("End Position isn't correct");
                br.BaseStream.Position = endPos;
            }

            this.Name = "";

            char ch;
            while ((int)(ch = br.ReadChar()) != 0)
                this.Name += ch;

            br.ReadBytes((int)((br.BaseStream.Length - 4) - br.BaseStream.Position));
            br.ReadInt32(); //End Tag
        }

        public void WriteDieselData(BinaryWriter bw, bool large=false)
        {
            long charCount = this._characters.Count;
            WriteInt(bw, charCount, large);
            WriteInt(bw, charCount, large);

            long startPos = large ? 168 : 92;

            //font rect pos
            WriteInt(bw, startPos, large);

            WriteInt(bw, u1, large);
            WriteInt(bw, u2, large);

            WriteInt(bw, charCount, large);
            WriteInt(bw, charCount, large);

            //asciiID pos
            WriteInt(bw, startPos + (charCount * (large ? 12 : 10)) + ((charCount % 2) != 0 ? 2 : 0), large);

            WriteInt(bw, u1, large);
            WriteInt(bw, u3, large);
            WriteInt(bw, u4, large);
            WriteInt(bw, Common_Base, large);

            long kernings = kerning.Count;
            WriteInt(bw, kernings, large);
            WriteInt(bw, kernings, large);

            //Kerning pos
            WriteInt(bw, (HasKerning ? startPos + (charCount * 18) : 0) + ((charCount % 2) != 0 ? 2 : 0), large);

            WriteInt(bw, u1, large);
            WriteInt(bw, u5, large);
            WriteInt(bw, u1, large);

            //end pos
            WriteInt(bw, startPos + (charCount * 18) + (kerning.Count * 12) + ((charCount % 2) != 0 ? 2 : 0), large);

            bw.Write(LineHeight);
            bw.Write(TextureWidth);
            bw.Write(TextureHeight);
            bw.Write(u7 != 0 ? LineHeight : u7);
            WriteInt(bw, Info_Size, large);

            this._characters.Sort(delegate(FontCharacter x, FontCharacter y)
            {
                return x.ID.CompareTo(y.ID);
            });

            foreach(FontCharacter fontChar in this._characters)
                fontChar.WriteMainSection(bw, large);

            if ((charCount % 2) != 0)
                bw.Write((short)0);

            this._characters.Sort(delegate(FontCharacter x, FontCharacter y)
            {
                return x.ASCIICode.CompareTo(y.ASCIICode);
            });

            foreach (FontCharacter fontChar in this._characters)
                fontChar.WriteCodeIDSection(bw);

            foreach (FontKerning fontKern in this.kerning)
                fontKern.WriteStream(bw);
            
            foreach (char nameChar in this.Name)
                bw.Write(nameChar);

            bw.Write((byte)0);
            while (bw.BaseStream.Position % 4 != 0)
                bw.Write((byte)0);

            bw.Write(DieselFont.EndTag);
        }
        #endregion
    }
}
