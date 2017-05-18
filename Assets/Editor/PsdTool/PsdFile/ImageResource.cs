using System;
using System.IO;

namespace PhotoshopFile
{
    //对应 文档-四
    public class ImageResource
    {
        public short ID { get; private set; }
        public byte[] Data { get; private set; }

        public BinaryReverseReader DataReader
        {
            get { return new BinaryReverseReader(new MemoryStream(Data)); }
        }

        public string Name { get; set; }

        public ImageResource(BinaryReverseReader reader)
        {
            // 从文档中 四- signature 开始读取数据.
            string osType = reader.ReadStringNew(4);
            if (osType != "8BIM" && osType != "MeSa")
            {
                throw new InvalidOperationException("Could not read an image resource");
            }

            // read the ID
            ID = reader.ReadInt16();

            // read the name
            Name = string.Empty;
            Name = reader.ReadPascalString();

            // read the length of the data in bytes
            uint length = reader.ReadUInt32();

            // read the actual data
            Data = reader.ReadBytes((int)length);
            if (reader.BaseStream.Position % 2L != 1L)
            {
                return;
            }

            reader.ReadByte();
        }

        protected ImageResource(ImageResource imgRes)
        {
            ID = imgRes.ID;
            Name = imgRes.Name;
            Data = new byte[imgRes.Data.Length];
            imgRes.Data.CopyTo(Data, 0);
        }
    }
}
