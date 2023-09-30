using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using DieselEngineFormats.Utils;

namespace DieselEngineFormats.Bundle
{
    public class Idstring : IComparable, ICloneable
    {
        private string unhashed;
        public string UnHashed { get => unhashed ?? HashedString; set => unhashed = value; }

        public bool HasUnHashed { get; set; }

        public bool Same { get; set; }

        public string HashedString
        {
            get
            {
                if (Same)
                    return UnHashed;

                string _HashedString = string.Format("{0:x}", Hashed);
                if (_HashedString.Length != 16)
                {
                    _HashedString.Reverse();
                    for (int i = 0; i < 16 - _HashedString.Length; i++)
                        _HashedString += "0";
                    _HashedString.Reverse();
                }

                return _HashedString;
            }
        }

        public ulong _hashed = 0;

        public ulong Hashed
        {
            get
            {
                if (_hashed == 0)
                    _hashed = Hash64.HashString(UnHashed);

                return this._hashed;
            }
        }

        public Idstring(string str, bool same = false)
        {
            Same = same;
            UnHashed = str;
            HasUnHashed = true;
        }

        public Idstring(ulong hashed)
        {
            _hashed = hashed;
            HasUnHashed = false;
        }

        public void SwapEndianness()
        {
            _hashed = General.SwapEndianness(Hashed);
        }

        public string Tag { get; set; }

        public override string ToString()
        {
            return HasUnHashed ? UnHashed : HashedString;
        }

        public int CompareTo(object obj)
        {
            return ToString().CompareTo((obj as Idstring)?.ToString() ?? "");
        }

        public object Clone()
        {
            return MemberwiseClone();
        }

        public override int GetHashCode()
        {
            return Hashed.GetHashCode();
        }
    }

    public static class HashIndex
    {
        public enum HashType
        {
            Path,
            Object,
            Ext = 4,
            Others = 8
        }

        private static Dictionary<ulong, Idstring> used_hashes = new Dictionary<ulong, Idstring>();
        public static Dictionary<ulong, Idstring> hashes = new Dictionary<ulong, Idstring>();
        public static Dictionary<ulong, Idstring> temp = new Dictionary<ulong, Idstring>();
        public static Dictionary<int, string> StringLookup = new Dictionary<int, string>();

        public static string version;

        public static Idstring Get(Idstring ids) => Get(ids.Hashed);
        public static string GetUnhashed(ulong ids) => Get(ids).UnHashed;
 
        public static Idstring Get(string hash)
        {
            AddHash(hash, out Idstring ids);
            return ids;
        }

        public static Idstring Get(ulong hash)
        {
            if (hashes.ContainsKey(hash))
            {
                if (!used_hashes.ContainsKey(hash))
                    used_hashes.Add(hash, hashes[hash]);

                return hashes[hash];
            }
            else if (used_hashes.ContainsKey(hash))
                return used_hashes[hash];
            else
                return new Idstring(hash);
        }

        public static void Load(ref HashSet<string> new_hashes)
        {
            foreach (string path in new_hashes)
            {
                Idstring ids = new Idstring(path);
                if(!hashes.ContainsKey(ids.Hashed))
                    hashes[ids.Hashed] = ids;
            }
        }

        public static void Clear()
        {
            hashes.Clear();
            temp.Clear();
        }

        public static void GenerateHashList(string HashlistFile, string tag = null)
        {
            StreamWriter fs = new StreamWriter(HashlistFile, false);

            if (version != null)
                fs.WriteLine("//" + version);

            foreach (KeyValuePair<ulong, Idstring> pair in hashes)
            {
                if (tag == null || (pair.Value.Tag == tag))
                    fs.WriteLine(pair.Value.UnHashed);
            }

            foreach (KeyValuePair<ulong, Idstring> pair in temp)
            {
                if (tag == null || (pair.Value.Tag == tag))
                    fs.WriteLine(pair.Value.UnHashed);
            }

            fs.Close();
        }

        public static Idstring CreateHash(string hash, string tag = null)
        {
            if (TypeOfHash(hash) == HashType.Path)
                hash = hash.ToLower();

            return new Idstring(hash) { Tag = tag };
        }

        public static HashType TypeOfHash(string hash)
        {
            if (hash.StartsWith("g_") && hash.StartsWith("c_") && hash.StartsWith("rp_") && hash.StartsWith("s_"))
                return HashType.Object;

            return HashType.Others;
        }

        public static bool AddHash(string hash, out Idstring ids, string tag = null, bool temp = false)
        {
            ids = CreateHash(hash, tag);
            return AddHash(ids, temp);
        }

        public static bool HasHash(ulong hash)
        {
            return hashes.ContainsKey(hash) || temp.ContainsKey(hash);
        }

        public static bool AddHash(Idstring ids, bool temp = false)
        {
            Dictionary<ulong, Idstring> hash_tbl = temp ? HashIndex.temp : hashes;

            lock (hash_tbl)
            {
                if (!HasHash(ids.Hashed))
                {
                    hash_tbl.Add(ids.Hashed, ids);
                    return true;
                }
            }

            return false;
        }

        public static string LookupString(int hashcode)
        {
            return StringLookup.ContainsKey(hashcode) ? StringLookup[hashcode] : null;
        }

        public static bool Load(string HashlistFile, bool temp = false)
        {
            try
            {
                string tag = Path.GetFileName(HashlistFile);
                foreach (string hash in File.ReadLines(HashlistFile))
                {
                    if (string.IsNullOrEmpty(hash)) //Check for empty or comment
                        continue;

                    Idstring ids;
                    AddHash(hash, out ids, tag, temp);
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                return false;
            }
            return true;
        }

        public static bool LoadParallel(string HashlistFile, bool temp = false)
        {
            try
            {
                string tag = Path.GetFileName(HashlistFile);
                System.Threading.Tasks.Parallel.ForEach(File.ReadLines(HashlistFile), hash =>
                {
                    if (string.IsNullOrEmpty(hash)) //Check for empty or comment
                        return; //continue;

                    AddHash(hash, out Idstring ids, tag, temp);
                });
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                return false;
            }
            return true;
        }
    }
}
