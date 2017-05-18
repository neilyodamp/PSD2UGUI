using System.Collections.Specialized;
using UnityEngine;

namespace PhotoshopFile
{
    public class Mask
    {
        private static readonly int PositionIsRelativeBit = BitVector32.CreateMask();
        private Rect _rect;
        private BitVector32 _flags;
        private byte _defaultColor;
        public Layer Layer { get; private set; }

        public Rect Rect
        {
            get { return _rect; }
        }

        public bool PositionIsRelative
        {
            get { return _flags[PositionIsRelativeBit]; }
        }

        public byte[] ImageData
        {
            get;
            private set;
        }
        
        internal Mask(BinaryReverseReader reader, Layer layer)
        {
            Layer = layer;
            // 从文档 五 - 4 - 14）
            uint num1 = reader.ReadUInt32();
            if (num1 <= 0U)
            {
                return;
            }

            long position = reader.BaseStream.Position;
            _rect = new Rect();
            _rect.y = reader.ReadInt32();
            _rect.x = reader.ReadInt32();
            _rect.height = reader.ReadInt32() - _rect.y;
            _rect.width = reader.ReadInt32() - _rect.x;
            _defaultColor = reader.ReadByte();
            _flags = new BitVector32(reader.ReadByte());

            int tempNum1 = -1;
            int tempNum2 = -1;
            int tempNum3 = -1;
            int tempNum4 = -1;
            int tempNum5 = -1;
            int tempNum6 = -1;

            if ((int)num1 == 36)
            {
                tempNum1 = reader.ReadByte();  // bit vector
                tempNum2 = reader.ReadByte();  // ???
                tempNum3 = reader.ReadInt32(); // rect Y
                tempNum4 = reader.ReadInt32(); // rect X
                tempNum5 = reader.ReadInt32(); // rect total height (actual height = this - Y)
                tempNum6 = reader.ReadInt32(); // rect total width (actual width = this - Y)
            }
            reader.BaseStream.Position = position + num1;
        }

        internal void LoadPixelData(BinaryReverseReader reader)
        {
            if (_rect.width <= 0 || !Layer.SortedChannels.ContainsKey(-2))
            {
                return;
            }

            Channel channel = Layer.SortedChannels[-2];
            channel.Data = reader.ReadBytes(channel.Length);
            using (BinaryReverseReader dataReader = channel.DataReader)
            {
                channel.ImageCompression = (ImageCompression)dataReader.ReadInt16();
                int columns = 0;
                switch (Layer.PsdFile.Depth)
                {
                    case 1:
                        columns = (int)_rect.width;
                        break;
                    case 8:
                        columns = (int)_rect.width;
                        break;
                    case 16:
                        columns = (int)_rect.width * 2;
                        break;
                }

                channel.ImageData = new byte[(int)_rect.height * columns];
                for (int index = 0; index < channel.ImageData.Length; ++index)
                {
                    channel.ImageData[index] = 171;
                }

                ImageData = (byte[])channel.ImageData.Clone();
                switch (channel.ImageCompression)
                {
                    case ImageCompression.Raw:
                        dataReader.Read(channel.ImageData, 0, channel.ImageData.Length);
                        break;
                    case ImageCompression.Rle:
                        int[] nums = new int[(int)_rect.height];
                        for (int i = 0; i < (int)_rect.height; i++)
                        {
                            nums[i] = dataReader.ReadInt16();
                        }

                        for (int index = 0; index < (int)_rect.height; ++index)
                        {
                            int startIdx = index * (int)_rect.width;
                            RleHelper.DecodedRow(dataReader.BaseStream, channel.ImageData, startIdx, columns);
                        }

                        break;
                }

                ImageData = (byte[])channel.ImageData.Clone();
            }
        }
    }
}
