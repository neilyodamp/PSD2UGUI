using System.IO;

namespace PhotoshopFile
{
    public class Channel
    {
        public int Length { get; private set; }
        public short ID { get; private set; }

        public byte[] Data { private get; set; }
        public byte[] ImageData { get; set; }
        public ImageCompression ImageCompression { get; set; }

        public BinaryReverseReader DataReader
        {
            get
            {
                if (Data == null)
                {
                    return null;
                }

                return new BinaryReverseReader(new MemoryStream(Data));
            }
        }
        private Layer Layer { get; set; }


        internal Channel(BinaryReverseReader reader, Layer layer)
        {
            //从文档 五 - 4- 6) 开始读
            ID = reader.ReadInt16();
            Length = reader.ReadInt32();
            Layer = layer;
        }
        internal void LoadPixelData(BinaryReverseReader reader)
        {
            Data = reader.ReadBytes(Length);

            using (BinaryReverseReader dataReader = DataReader)
            {
                //从文档 五 - 5 读取信息
                ImageCompression = (ImageCompression)dataReader.ReadInt16();
                int columns = 0;
                switch (Layer.PsdFile.Depth)
                {
                    case 1:
                        columns = (int)Layer.Rect.width;
                        break;
                    case 8:
                        columns = (int)Layer.Rect.width;
                        break;
                    case 16:
                        columns = (int)Layer.Rect.width * 2;
                        break;
                }

                ImageData = new byte[(int)Layer.Rect.height * columns];
                switch (ImageCompression)
                {
                    case ImageCompression.Raw:
                        dataReader.Read(ImageData, 0, ImageData.Length);
                        break;
                    case ImageCompression.Rle:
                        int[] nums = new int[(int)Layer.Rect.height];

                        for (int i = 0; i < Layer.Rect.height; i++)
                        {
                            nums[i] = dataReader.ReadInt16();
                        }

                        for (int index = 0; index < Layer.Rect.height; ++index)
                        {
                            int startIdx = index * (int)Layer.Rect.width;
                            RleHelper.DecodedRow(dataReader.BaseStream, ImageData, startIdx, columns);
                        }

                        break;
                }
            }
        }

    }
}
