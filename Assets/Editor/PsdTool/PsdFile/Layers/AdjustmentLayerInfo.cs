using System.IO;
using UnityEngine;

namespace PhotoshopFile
{
    public class AdjustmentLayerInfo
    {
        public string Key { get; private set; }

        public BinaryReverseReader DataReader
        {
            get { return new BinaryReverseReader(new MemoryStream(Data)); }
        }

        private byte[] Data { get; set; }

        public AdjustmentLayerInfo(BinaryReverseReader reader, Layer layer)
        {
            // 从文档 五 - 4 - 22) 开始读取
            string head = reader.ReadStringNew(4);
            if (head != "8BIM")
            {
                throw new IOException("Could not read an image resource");
            }

            Key = reader.ReadStringNew(4);
            if (Key == "lfx2" || Key == "lrFX")
            {
                layer.HasEffects = true;
            }

            uint length = reader.ReadUInt32();
            Data = reader.ReadBytes((int)length);
        }

    }
}
