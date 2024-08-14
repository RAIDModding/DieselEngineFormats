namespace DieselEngineFormats.Bundle
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;
    using Utils;

    public class PackageFooterEntry
    {
        public Idstring Extension { get; set; }

        public Idstring Path { get; set; }

		public PackageHeader Parent { get; set; }

        public PackageFooterEntry(BinaryReader br)
        {
            this.Extension = HashIndex.Get(br.ReadUInt64());
            this.Path = HashIndex.Get(br.ReadUInt64());
        }
    }
    
    /// <summary>
    ///     The bundle header.
    /// </summary>
    /// 
    public class PackageHeader
    {
        #region Fields

        /// <summary>
        ///     The has length field.
        /// </summary>
        public bool HasLengthField = false;

        /// <summary>
        ///     Does the header contain information about multiple bundles?
        /// </summary>
        public bool multiBundle;

        /// <summary>
        ///     Is the header from a 64 bit diesel game(such as Raid WW2)?
        /// </summary>
        public bool x64;

        private BundleHeaderConfig _config = Defs.PD2Config;

        public BundleHeaderConfig Config  { get => this._config; set => this._config = value; }

        /// <summary>
        /// The _entries.
        /// </summary>
        private List<PackageFileEntry> _entries = new List<PackageFileEntry>();

        /// <summary>
        /// The _references.
        /// </summary>
        //private List<PackageFooterEntry> _references = new List<PackageFooterEntry>();

		private Idstring _name;

        #endregion

        #region Public Properties

        /// <summary>
        ///     Gets list of bundle file entries
        /// </summary>
        public List<PackageFileEntry> Entries { get => this._entries; }

        /// <summary>
        ///     Gets list of reference entries
        /// </summary>
        //public List<PackageFooterEntry> References { get => this._references; }

        /// <summary>
        ///     Gets or sets
        /// </summary>
        public byte[] Footer { get; set; }

        /// <summary>
        ///     The header.
        /// </summary>
        public List<ulong> Header { get; set; }

        public Idstring Name { get => this._name; set => this._name = value; }

        public string BundleName { get; set; }

        public ulong Length { get; set; }
        #endregion

        #region Public Methods and Operators

		public PackageHeader () { }
		public PackageHeader (string bundleId) => Load(bundleId);
		

        /// <summary>
        /// The load.
        /// </summary>
        /// <param name="bundleId">
        /// The bundle id.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        public bool Load(string bundleFile)
        {

            string bundlePathExtensionLess = bundleFile.Replace(".bundle", "");
            string headerFile = bundlePathExtensionLess + "_h.bundle";

            BundleName = Path.GetFileName(bundlePathExtensionLess);

            int multiHeaderIndex = -1;
            if (!File.Exists(headerFile))
            {
                List<string> splt = BundleName.Split("_").ToList();
                string last = splt.Last();
                splt.RemoveAt(splt.Count - 1);
                string possibleHeader = Path.GetDirectoryName(bundlePathExtensionLess) + $"\\{string.Join("_", splt)}_h.bundle";
                if (File.Exists(possibleHeader))
                {
                    headerFile = possibleHeader;
                    multiHeaderIndex = int.Parse(last);
                }
                else
                {
                    Console.WriteLine("Package header does not exist: {0} Possible header does not exist either: {1}", headerFile, possibleHeader);
                    return false;
                }
            }

            if(BundleName.Contains("_"))
                _name = new Idstring(BundleName, true);
            else
            {
                _name = (Idstring)General.BundleNameToPackageID(BundleName).Clone();
                _name.SwapEndianness();
            }

            try
            {
                using (var fs = new FileStream(headerFile, FileMode.Open))
                {
                    using (var br = new BinaryReader(fs))
                    {
                        if (multiHeaderIndex != -1)
                            return ReadMultiBundleHeader(br, multiHeaderIndex);
                        else
                            return ReadHeader(br, bundleFile);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
        }

        public static ulong ReadCombinedCompressedSize(string path)
        {
            ulong result;
            using (FileStream fs = new FileStream(path, FileMode.Open))
            {
                using (BinaryReader br = new BinaryReader(fs))
                {
                    result = br.ReadUInt64();
                }
            }
            return result;
        }

        // Token: 0x06000150 RID: 336 RVA: 0x00009478 File Offset: 0x00007678
        public static byte[] ReadCombinedCompressed(string path)
        {
            byte[] result;
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open))
                {
                    using (BinaryReader br = new BinaryReader(fs))
                    {
                        result = PackageHeader.ReadCombinedCompressed(br);
                    }
                }
            }
            catch (Exception value)
            {
                Console.WriteLine(value);
                result = new byte[0];
            }
            return result;
        }

        // Token: 0x06000151 RID: 337 RVA: 0x000094E8 File Offset: 0x000076E8
        public static byte[] ReadCombinedCompressed(BinaryReader br)
        {
            ulong num = br.ReadUInt64();
            int sectionCount = (int)Math.Ceiling(num / 65536.0);
            byte[] output = new byte[num];
            int currentMax = (int)num;
            for (int i = 0; i < sectionCount; i++)
            {
                int sectionSize = br.ReadInt32();
                byte[] sectionData = br.ReadBytes(sectionSize);
                byte[] uncompressedSectionData;
                try
                {
                    using (MemoryStream inms = new MemoryStream(sectionData))
                    {
                        using (MemoryStream outms = new MemoryStream())
                        {
                            General.ZLibDecompress(inms, outms);
                            uncompressedSectionData = outms.ToArray();
                        }
                    }
                }
                catch
                {
                    uncompressedSectionData = sectionData;
                }
                int length = uncompressedSectionData.Length;
                if (length > currentMax)
                {
                    length = currentMax;
                }
                Array.Copy(uncompressedSectionData, 0, output, 65536 * i, length);
                currentMax -= 65536;
            }
            return output;
        }

        // Token: 0x06000152 RID: 338 RVA: 0x000095D8 File Offset: 0x000077D8
        public bool ReadHeader(BinaryReader cbr, string bundleFile)
        {
            bool result;
            using (BinaryReader br = new BinaryReader(new MemoryStream(PackageHeader.ReadCombinedCompressed(cbr))))
            {
                bool confSet = false;
                br.BaseStream.Position = 8L;
                if (br.ReadUInt32() == 0U && br.ReadUInt32() != 0U)
                {
                    this.x64 = true;
                }
                br.BaseStream.Position = 0L;
                if (this.x64)
                {
                    this.Header = new List<ulong>
                    {
                        (ulong)br.ReadUInt32(),
                        br.ReadUInt64(),
                        br.ReadUInt64(),
                        br.ReadUInt64()
                    };
                }
                else
                {
                    this.Header = new List<ulong>
                    {
                        (ulong)br.ReadUInt32(),
                        (ulong)br.ReadUInt32(),
                        (ulong)br.ReadUInt32(),
                        (ulong)br.ReadUInt32()
                    };
                }
                if (this.Header[1] != this.Header[2])
                {
                    this.Header.Add((ulong)br.ReadUInt32());
                }
                ulong itemCount = 0UL;
                ulong offset = 0UL;
                ulong refOffset = this.Header[0];
                int i = 1;
                while (i < this.Header.Count - 1)
                {
                    if (this.Header[i] == this.Header[i + 1])
                    {
                        itemCount = this.Header[i];
                        if (this.Header.Count <= i + 2)
                        {
                            this.Header.Add((ulong)br.ReadUInt32());
                        }
                        offset = this.Header[i + 2];
                        if (i != 1)
                        {
                            this.HasLengthField = true;
                            break;
                        }
                        break;
                    }
                    else
                    {
                        i++;
                    }
                }
                if (br.BaseStream.Position < (long)offset)
                {
                    ulong amount = (offset - (ulong)br.BaseStream.Position) / 4UL;
                    uint j = 0U;
                    while ((ulong)j < amount)
                    {
                        this.Header.Add((ulong)br.ReadUInt32());
                        j += 1U;
                    }
                }
                if (offset == 0UL)
                {
                    offset = refOffset - 4UL;
                }
                br.BaseStream.Position = (long)offset;
                uint entryTag = br.ReadUInt32();
                if (!confSet)
                {
                    List<BundleHeaderConfig> configs = Defs.ConfigLookup.FindAll(delegate (BundleHeaderConfig conf)
                    {
                        if (!this.HasLengthField)
                        {
                            return conf.EntryStartTag.Equals(entryTag);
                        }
                        return conf.EntryStartLengthTag.Equals(entryTag);
                    });
                    if (configs.Count == 1)
                    {
                        this.Config = configs[0];
                    }
                }
                for (int k = 0; k < (int)itemCount; k++)
                {
                    this.ReadEntry(br, k);
                }
                if (itemCount > 0UL && !this.HasLengthField)
                {
                    ulong length = PackageHeader.ReadCombinedCompressedSize(bundleFile);
                    this._entries[this._entries.Count - 1].Length = (int)(length - (ulong)this._entries[this._entries.Count - 1].Address);
                }
                this.Footer = br.ReadBytes((int)(br.BaseStream.Length - br.BaseStream.Position));
                result = true;
            }
            return result;
        }

        private void ReadEntry(BinaryReader br, int i)
        {
            var be = new PackageFileEntry(br, HasLengthField) { Parent = this };
            this._entries.Add(be);

            if (HasLengthField || i <= 0)
                return;

            PackageFileEntry pbe = this._entries[i - 1];
            pbe.Length = (int)(be.Address - pbe.Address);
        }

        public bool ReadMultiBundleHeader(BinaryReader br, long bundleNum)
        {
            //Based of https://steamcommunity.com/app/218620/discussions/15/1474221865204158003/
            HasLengthField = true;
            multiBundle = true;

            Header = new List<ulong>()
            {
                br.ReadUInt32(), //eof
                br.ReadUInt32(), //bundle count
                br.ReadUInt32(), //unknown
                br.ReadUInt32(), //offset
                br.ReadUInt32(), //unknown
            };

            x64 = Header[3] == 24;
            br.BaseStream.Position = (long)(Header[3] + 4);

            for (long i = 0; i < (long)Header[1]; i++)
            {
                long index;
                if (x64)
                {
                    index = br.ReadInt64();
                    br.BaseStream.Position += 8; // unknown
                }
                else
                {
                    index = br.ReadInt32();
                    br.BaseStream.Position += 4; // unknown
                }

                uint entryCount1 = br.ReadUInt32();
                uint entryCount2 = br.ReadUInt32();

                ulong Offset;
                ulong One;
                if (x64)
                {
                    Offset = br.ReadUInt64();
                    br.BaseStream.Position += 8; //unknown
                    One = br.ReadUInt64();
                }
                else
                {
                    Offset = br.ReadUInt32();
                    br.BaseStream.Position += 4; //unknown
                    One = br.ReadUInt32();
                }

                if (One == 1 && entryCount1 == entryCount2)
                {
                    if (index == bundleNum)
                    {
                        br.BaseStream.Position = (long)Offset + 4;
                        for (int x = 0; x < entryCount1; x++)
                            ReadEntry(br, x);

                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }

            return false;
        }

        //Unused atm
        private void ReadFooter(BinaryReader br)
        {
            //Footer breakdown
            /*
             * uint32 - tag
             * uint32 - section size
             * uint32 - count
             * uint32 - unknown
             * uint32 - unknown
             * uint32 - tag?
             * foreach (count):
             *  uint64 - hash (extension)
             *  uint64 - hash (path)
             * uint32 - end?
             * uint32 (0) - end
            */
            uint size = br.ReadUInt32();
            uint count = br.ReadUInt32();
            uint alloc_size = br.ReadUInt32();
            uint entry_size = br.ReadUInt32();
            uint tag = br.ReadUInt32();

            //for (int i = 0; i < count; i++)
              //  this._references.Add(new PackageFooterEntry(br));

        }

        /// <summary>
        /// Write the header contents to the BinaryWriter 'writer', with the default/detected config
        /// </summary>
        /// <param name="writer">
        /// The writer.
        /// </param>
        public void Write(BinaryWriter writer)
        {
            this.Write(writer, this.Config);
        }

        /// <summary>
        /// Write the header contents to the BinaryWriter 'writer', with the passed 'config'
        /// </summary>
        /// <param name="writer">
        /// The writer.
        /// </param>
        /// <param name="config">
        /// The config for writing the Header
        /// </param>
        public void Write(BinaryWriter writer, BundleHeaderConfig config)
        {
            writer.Write((uint)((this.HasLengthField ? 7 + this.Entries.Count * 3 : 5 + this.Entries.Count * 2) * 4));

            if (HasLengthField)
                writer.Write(config.HeaderStartLengthTag);
            else if (config.HeaderStartEmptyTag)
                writer.Write((uint)0);

            writer.Write(this.Entries.Count);
            writer.Write(this.Entries.Count);

            writer.Write(this.HasLengthField ? 24 : 16);

            if (HasLengthField)
            {
                writer.Write(config.HeaderEndLengthTag);
                writer.Write(config.EntryStartLengthTag);
            }
            else
                writer.Write(config.EntryStartTag);

            foreach (PackageFileEntry entry in Entries)
                entry.WriteEntry(writer, HasLengthField);

            /*
            if (config.EmptyEndInt && this.Footer[Footer.Length - 1] != 0)
                writer.Write((uint)0);
             */

            if (this.Footer != null)
                writer.Write(this.Footer);
        }

        /// <summary>
        ///     Sort list of bundle file entries by Id
        /// </summary>
        public void SortEntriesId()
        {
            int oldcount = this._entries.Count();
            this._entries = this._entries.OrderBy(o => o.ID).ToList();
            if (oldcount != this._entries.Count())
                Console.WriteLine();
        }

        /// <summary>
        ///     Sort list of bundle file entries by Address
        /// </summary>
        public void SortEntriesAddress()
        {
            int oldcount = this._entries.Count();
            this._entries = this._entries.OrderBy(o => o.Length).ToList(); // Order by length, so 0 is always first
            this._entries = this._entries.OrderBy(o => o.Address).ToList(); //Order by address
            if (oldcount != this._entries.Count())
                Console.WriteLine();
        }

        #endregion
    }

    public struct BundleHeaderConfig
    {
        public bool HeaderStartEmptyTag { get; set; }

        public uint HeaderStartLengthTag { get; set; }

        public uint HeaderEndLengthTag { get; set; }

        public uint EntryStartLengthTag { get; set; }

        public uint EntryStartTag { get; set; }

        public uint ReferenceStartTag { get; set; }

        public uint EndTag { get; set; }

        public bool EmptyEndInt { get; set; }

        public bool Is64;
    }
}
