using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PhotoshopFile
{
    public class Layer
    {
        private static readonly int ProtectTransparencyBit = BitVector32.CreateMask();
        private static readonly int VisibleBit = BitVector32.CreateMask(ProtectTransparencyBit);
        private static readonly int ObsoleteBit = BitVector32.CreateMask(VisibleBit);
        private static readonly int Version5OrLaterBit = BitVector32.CreateMask(ObsoleteBit);
        private static readonly int PixelDataIrrelevantBit = BitVector32.CreateMask(Version5OrLaterBit);

        public static Regex ZH_REG = new Regex(@"[\u4E00-\u9FBF]");
        private static int _readIndex = 0;

        private BitVector32 _flags;
        private string _name = "";
        private BlendingRanges _blendingRangesData;
        private List<AdjustmentLayerInfo> _adjustmentInfo;
        private float _imageTransparent = 1f;
        private int _outLineDis = 1;

        #region TextLayer Properties
        public bool IsTextLayer { get; private set; }
        public string Text { get; private set; }
        public float FontSize { get; private set; }
        public string FontName { get; private set; }
        public TextJustification Justification { get; private set; }

        public Color FillColor { get; private set; }

        public Color TextOutlineColor { get; private set; }

        public int OutLineDis {
            get
            {
                return _outLineDis;
            }
        }
        public float ImageTransparent
        {
            get { return _imageTransparent; }
        }


        public string WarpStyle { get; private set; }


        #endregion


        public List<Layer> Children { get; private set; }
        public bool HasEffects { get; set; }
        public Rect Rect { get; private set; }
        public List<Channel> Channels { get; private set; }
        public SortedList<short, Channel> SortedChannels { get; private set; }
        public byte Opacity { get; private set; }

        public bool Visible
        {
            get
            {
                return !_flags[VisibleBit];
            }
        }
        public bool IsPixelDataIrrelevant
        {
            get
            {
                return _flags[PixelDataIrrelevantBit];
            }
        }

        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
            }
        }

        public Mask MaskData { get; private set; }
        internal PsdFile PsdFile { get; private set; }
        

        private static string DefaultLayerName
        {
            get
            {
                _readIndex++;
                return "";
                //return PsdImporter.NO_NAME_HEAD + _readIndex;
            }
        }

        public Layer(BinaryReverseReader reader, PsdFile psdFile)
        {
            //从文档 五 - 4 - 1) 开始读
            Children = new List<Layer>();
            PsdFile = psdFile;

            Rect rect = new Rect();
            rect.y = reader.ReadInt32();
            rect.x = reader.ReadInt32();
            rect.height = reader.ReadInt32() - rect.y;
            rect.width = reader.ReadInt32() - rect.x;
            Rect = rect;

            int channelCount = reader.ReadUInt16();
            Channels = new List<Channel>();
            SortedChannels = new SortedList<short, Channel>();
            for (int index = 0; index < channelCount; ++index)
            {
                Channel channel = new Channel(reader, this);
                Channels.Add(channel);
                SortedChannels.Add(channel.ID, channel);
            }

            string head = reader.ReadStringNew(4);

            if (head != "8BIM")
            {
                throw new IOException("Layer Channelheader error!");
            }

            string layerRecordsBlendModeKey = reader.ReadStringNew(4);

            Opacity = reader.ReadByte();

            int Clipping = reader.ReadByte();

            _flags = new BitVector32(reader.ReadByte());

            int Filler = reader.ReadByte();

            _imageTransparent = Convert.ToSingle(Opacity) / byte.MaxValue;

            //文档 五 - 4 - 13)
            uint num3 = reader.ReadUInt32();
            long position1 = reader.BaseStream.Position;
            MaskData = new Mask(reader, this);

            _blendingRangesData = new BlendingRanges(reader);
            long position2 = reader.BaseStream.Position;

            // 文档 五 - 4 - 21)
            Name = reader.ReadPascalString();
            // read the adjustment info
            int count = (int)((reader.BaseStream.Position - position2) % 4L);
            reader.ReadBytes(count);

            _adjustmentInfo = new List<AdjustmentLayerInfo>();
            long num4 = position1 + num3;

            while (reader.BaseStream.Position < num4)
            {
                try
                {
                    _adjustmentInfo.Add(new AdjustmentLayerInfo(reader, this));
                }
                catch
                {
                    reader.BaseStream.Position = num4;
                }
            }


            foreach (AdjustmentLayerInfo adjustmentLayerInfo in _adjustmentInfo)
            {

                if (adjustmentLayerInfo.Key == "TySh")
                {
                    ReadTextLayer(adjustmentLayerInfo.DataReader);
                }
                else if (adjustmentLayerInfo.Key == "luni")
                {
                    // read the unicode name
                    BinaryReverseReader dataReader = adjustmentLayerInfo.DataReader;
                    byte[] temp1 = dataReader.ReadBytes(3);
                    byte charCount = dataReader.ReadByte();
                    //本来 charCount 是文本串的长度，可以传入ReadString()限定读取长度，但Text除串头无文本长度信息，因此改为读一段Unicode字符串
                    Name = dataReader.ReadString();
                    if (Name == "")
                        Name = DefaultLayerName;

                }
                //此处针对字体  图层样式
                else if (adjustmentLayerInfo.Key == "lrFX")//样式 相关，对于字体来说，就是描边之类的
                {
                    ParseLrfxKeyword(adjustmentLayerInfo);//yanruTODO测试屏蔽
                }
                //仅对于图片的 
                else if (adjustmentLayerInfo.Key == "lspf")
                {
                    BinaryReverseReader dataReader = adjustmentLayerInfo.DataReader;
                    byte[] data = dataReader.ReadBytes(4);
                }
                else if (adjustmentLayerInfo.Key == "lclr")
                {
                    BinaryReverseReader dataReader = adjustmentLayerInfo.DataReader;
                    byte[] data = dataReader.ReadBytes(10);
                }
            }

            reader.BaseStream.Position = num4;


        }

        private void ParseLrfxKeyword(AdjustmentLayerInfo adjustmentLayerInfo)
        {
            BinaryReverseReader dataReader = adjustmentLayerInfo.DataReader;

            int version = dataReader.ReadInt16();
            int effectCount = dataReader.ReadInt16();

            for (int index = 0; index < effectCount; index++)
            {
                string sigNature = dataReader.ReadStringNew(4);
                string type = dataReader.ReadStringNew(4);

                switch (type)
                {
                    case "cmnS"://OK
                        int cmnsSize = dataReader.ReadInt32();
                        int cmnsVersion = dataReader.ReadInt32();
                        bool cmnsBool = dataReader.ReadBoolean();
                        int cmnsUnused = dataReader.ReadInt16();
                        break;
                    case "dsdw":// 投影效果
                        byte[] testbyte2 = dataReader.ReadBytes(55);        
                        break;
                    case "isdw": //内阴影效果
                        int dropSize = dataReader.ReadInt32();
                        int dropVersion = dataReader.ReadInt32();
                        int dropBlurValue = dataReader.ReadInt32();
                        int Intensityasapercent = dataReader.ReadInt32();
                        int angleindegrees = dataReader.ReadInt32();
                        int distanceinp = dataReader.ReadInt32();

                        byte[] colortest = dataReader.ReadBytes(10);

                        dataReader.ReadBytes(4);
                        string dropBlendmode = dataReader.ReadStringNew(4);

                        bool dropeffectEnable = dataReader.ReadBoolean();
                        byte usethisangle = dataReader.ReadByte();
                        int dropOpacity = dataReader.ReadByte();


                        int dropSpace11 = dataReader.ReadInt16();
                        int color111 = dataReader.ReadInt16();
                        int color211 = dataReader.ReadInt16();
                        int color311 = dataReader.ReadInt16();
                        int color411 = dataReader.ReadInt16();

                        dataReader.ReadBytes(4);
                        dataReader.ReadBytes(4);
                        dataReader.ReadBytes(4);
                        dataReader.ReadBytes(4);
                        dataReader.ReadBytes(4);
                        dataReader.ReadBytes(4);
                        dataReader.ReadBytes(10);
                        string sign1 = dataReader.ReadStringNew(4);
                        string key1 = dataReader.ReadStringNew(4);

                        dataReader.ReadBytes(1);
                        dataReader.ReadBytes(1);
                        dataReader.ReadBytes(1);
                        if (dropVersion == 2)
                        {
                            dataReader.ReadBytes(10);
                        }

                        break;
                    case "oglw"://有用:字体的描边！
                        int sizeofRemainItems = dataReader.ReadInt32();
                        int oglwversion = dataReader.ReadInt32();

                        byte[] blurdata = dataReader.ReadBytes(4);

                        _outLineDis = Convert.ToInt32(blurdata[1]); //也是小坑，四个故意放在第二个字节 也不说明( ▼-▼ )

                        //int blurvalue = dataReader.ReadInt32();

                        int intensityPercent = dataReader.ReadInt32();

                        byte outline_r = 0;
                        byte outline_g = 0;
                        byte outline_b = 0;
                        byte outline_a = 0;

                        dataReader.ReadBytes(2);
                        outline_r = dataReader.ReadByte();
                        dataReader.ReadByte();
                        outline_g = dataReader.ReadByte();
                        dataReader.ReadByte();
                        outline_b = dataReader.ReadByte();
                        dataReader.ReadByte();
                        outline_a = dataReader.ReadByte();
                        dataReader.ReadByte();

                        string curSign = dataReader.ReadStringNew(4);
                        string key = dataReader.ReadStringNew(4);

                        bool effectEnable = dataReader.ReadBoolean(); //yanruTODO 不可靠，如果整个effect 层 禁用了，子字段可能依然为true，暂时找不到上层effect开关


                        byte opacityPercent = dataReader.ReadByte();//描边透明度

                        if (oglwversion == 2)
                        {
                            byte[] oglwColor2 = dataReader.ReadBytes(10);
                        }


                        if (!effectEnable) //指明了没有描边
                        {
                            TextOutlineColor = new Color(0, 0, 0, 0);
                        }
                        else
                        {
                            TextOutlineColor = new Color(outline_r / 255f, outline_g / 255f, outline_b / 255f, opacityPercent / 255f);
                        }
                        break;
                    case "iglw":
                        byte[] testdata5 = dataReader.ReadBytes(47);
                        //effectStr += "\n" + printbytes(testdata5, "iglw");

                        //dataReader.ReadBytes(4);
                        //dataReader.ReadBytes(4);
                        //dataReader.ReadBytes(4);
                        //dataReader.ReadBytes(4);
                        //dataReader.ReadBytes(10);
                        //dataReader.ReadBytes(8);
                        //dataReader.ReadBytes(1);
                        //dataReader.ReadBytes(1);
                        //dataReader.ReadBytes(1);
                        //dataReader.ReadBytes(10);
                        break;
                    case "bevl":

                        int bevelSizeofRemain = dataReader.ReadInt32();//.ReadBytes(4);
                        int bevelversion = dataReader.ReadInt32();
                        //dataReader.ReadBytes(4);
                        dataReader.ReadBytes(4);
                        dataReader.ReadBytes(4);
                        dataReader.ReadBytes(4);

                        dataReader.ReadBytes(8);
                        dataReader.ReadBytes(8);

                        dataReader.ReadBytes(10);
                        dataReader.ReadBytes(10);

                        dataReader.ReadBytes(1);
                        dataReader.ReadBytes(1);
                        dataReader.ReadBytes(1);
                        dataReader.ReadBytes(1);
                        dataReader.ReadBytes(1);
                        dataReader.ReadBytes(1);

                        if (bevelversion == 2)
                        {
                            dataReader.ReadBytes(10);
                            dataReader.ReadBytes(10);
                        }

                        break;
                        case "sofi":
                            int solidSize = dataReader.ReadInt32();//.ReadBytes(4);
                            int solidVersion = dataReader.ReadInt32();// (4);
                            string sign = dataReader.ReadStringNew(4);
                            string solidBlendmode = dataReader.ReadStringNew(4);//.ReadBytes(4);

                            byte[] solidColor = dataReader.ReadBytes(10);

                            byte opacity = dataReader.ReadByte();
                            byte solidenable = dataReader.ReadByte();
                            dataReader.ReadBytes(10);
                            break;
                }
            }
        }
        private void ReadTextLayer(BinaryReverseReader dataReader)
        {
            //文档 五 - 4 - 22） -d - 15
            IsTextLayer = true;
            dataReader.Seek("/Text");
            byte[] temp = dataReader.ReadBytes(4);
           
            Text = dataReader.ReadString();// ( true);

            dataReader.Seek("/Justification");
            int justification = dataReader.ReadByte();// - 48;
            Justification = TextJustification.Left;
            if (justification == 1)
            {
                Justification = TextJustification.Right;
            }
            else if (justification == 2)
            {
                Justification = TextJustification.Center;
            }

            dataReader.Seek("/FontSize ");
            FontSize = dataReader.ReadFloat();

            // read the font fill color
            dataReader.Seek("/FillColor");
            dataReader.Seek("/Values [ ");
            float alpha = dataReader.ReadFloat();
            dataReader.ReadByte();
            float red = dataReader.ReadFloat();
            dataReader.ReadByte();
            float green = dataReader.ReadFloat();
            dataReader.ReadByte();
            float blue = dataReader.ReadFloat();
            FillColor = new Color(red, green, blue, alpha);

            //  read the font name
            dataReader.Seek("/FontSet ");

            dataReader.Seek("/Name");

            FontName = dataReader.ReadString();

            // read the warp style
            dataReader.Seek("warpStyle");
            dataReader.Seek("warpStyle");
            byte[] wrapBytes = dataReader.ReadBytes(3);

            int num13 = dataReader.ReadByte();
            WarpStyle = string.Empty;

            for (; num13 > 0; --num13)
            {
                string str = WarpStyle + dataReader.ReadChar();
                WarpStyle = str;
            }
        }
    }
}