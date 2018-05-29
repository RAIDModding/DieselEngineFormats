using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DieselEngineFormats
{
    public class DieselFormat
    {
        public DieselFormat()
        {

        }

        public DieselFormat(string filePath) : this()
        {
            Load(filePath);
        }

        public DieselFormat(Stream fileStream) : this()
        {
            ReadFile(fileStream);
        }

        public void WriteFile(string filepath)
        {
            using (var fs = new FileStream(filepath, FileMode.Create, FileAccess.Write))
                WriteFile(fs);
        }

        public virtual void WriteFile(Stream fs)
        {
            using (var bw = new BinaryWriter(fs))
                WriteFile(bw);
        }

        public virtual void WriteFile(BinaryWriter bw)
        {

        }

        public void ReadFile(Stream fileStream)
        {
            using (BinaryReader br = new BinaryReader(fileStream))
                ReadFile(br);
        }

        public virtual void ReadFile(BinaryReader br)
        {

        }
    
        public void Load(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                    this.ReadFile(br);
        }
    }
}
