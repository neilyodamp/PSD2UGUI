using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PhotoshopFile
{
    public class BlendingRanges
    {
        
        public BlendingRanges(BinaryReverseReader reader)
        {
            //读文档 五 - 4 - 15)  到 五 - 4 - 20)
            int count = reader.ReadInt32();
            if (count <= 0) return;

            byte[] data = reader.ReadBytes(count);
        }
    }
}
