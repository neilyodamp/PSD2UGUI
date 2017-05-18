using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace PhotoshopFile
{
    public class PsdFile
    {
        private short   _channels;
        private int     _height;
        private int     _width;
        private int     _depth;

        private XDocument               _metaData;
        private string                  _category;
        private short                   _version;
        private List<ImageResource>     _imageResources;
        private bool _absoluteAlpha;
        private byte[][] _imageData;
        private ImageCompression _imageCompression;


        public byte [] ColorModeData { get; private set; }

        public int Height
        {
            get { return _height; }
        }

        public int Width
        {
            get { return _width; }
        }

        public int Depth
        {
            get { return _depth; }
        }

        public ColorModes ColorMode { get; private set; }

        public List<Layer> Layers { get; private set; }

        

        public PsdFile(string fileName)
        {
            _category = string.Empty;
            _version = 1;
            Layers = new List<Layer>();
            _imageResources = new List<ImageResource>();

            using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                BinaryReverseReader reader = new BinaryReverseReader(fileStream);
                LoadHeader(reader);
                LoadColorModeData(reader);
                LoadImageResources(reader);
                LoadLayerAndMaskInfo(reader);
                LoadImage(reader);
            }
        }

        private void LoadHeader(BinaryReverseReader reader)
        {
            string strHead = reader.ReadStringNew(4);
            if (strHead != "8BPS")
            {
                UnityEngine.Debug.LogError("The given stream is not a valid PSD file");
                throw new IOException("The given stream is not a valid PSD file");
            }

            _version = reader.ReadInt16();
            if (_version != 1)
            {
                UnityEngine.Debug.LogError("The PSD file has an invalid version");
                throw new IOException("The PSD file has an invalid version");
            }

            reader.BaseStream.Position += 6L;
            _channels = reader.ReadInt16();
            _height = reader.ReadInt32();
            _width = reader.ReadInt32();

            _depth = reader.ReadInt16();

            ColorMode = (ColorModes)reader.ReadInt16();
        }

        private void LoadColorModeData(BinaryReverseReader reader)
        {
            uint num = reader.ReadUInt32();
            if (num <= 0U)
            {
                return;
            }
            ColorModeData = reader.ReadBytes((int)num);
        }

        private void LoadImageResources(BinaryReverseReader reader)
        {
            _imageResources.Clear();
            uint num = reader.ReadUInt32();

            if (num <= 0U)
            {
                return;
            }

            long position = reader.BaseStream.Position;
            while (reader.BaseStream.Position - position < num)
            {
                ImageResource imgRes = new ImageResource(reader);

                switch ((ResourceIDs)imgRes.ID)
                {
                    case ResourceIDs.XMLInfo:
                        _metaData = XDocument.Load(XmlReader.Create(new MemoryStream(imgRes.Data)));
                        IEnumerable<XElement> source = _metaData.Descendants(XName.Get("Category", "http://ns.adobe.com/photoshop/1.0/"));
                        if (source.Any())
                        {
                            _category = source.First().Value;
                        }
                        break;

                    case ResourceIDs.ResolutionInfo:
                        imgRes = new ResolutionInfo(imgRes);
                        break;

                    case ResourceIDs.AlphaChannelNames:
                        imgRes = new AlphaChannels(imgRes);
                        break;

                    case ResourceIDs.PsCCOrignPathInfo:
                        imgRes = new AlphaChannels(imgRes);
                        break;
                    case ResourceIDs.PsCCPathSelectionState:
                        imgRes = new AlphaChannels(imgRes);
                        break;
                    case ResourceIDs.TransparencyIndex:
                        Debug.Log("have transparent ");
                        break;
                }

                _imageResources.Add(imgRes);
            }

            reader.BaseStream.Position = position + num;
        }

        private void LoadLayerAndMaskInfo(BinaryReverseReader reader)
        {
            long num = reader.ReadUInt32();
            if (num <= 0U)
            {
                return;
            }

            long position = reader.BaseStream.Position;
            LoadLayers(reader);
            LoadGlobalLayerMask(reader);
            reader.BaseStream.Position = position + num;
        }

        private void LoadLayers(BinaryReverseReader reader)
        {
            // 文档 五 - 2
            int num1 = reader.ReadInt32();

            if (num1 <= 0U)
            {
                return;
            }

            long position = reader.BaseStream.Position;

            short num2 = reader.ReadInt16();

            if (num2 < 0)
            {
                _absoluteAlpha = true;
                num2 = Math.Abs(num2);
            }

            Layers.Clear();

            if (num2 == 0)
            {
                return;
            }

            for (int index = 0; index < (int)num2; ++index)
            {
                Layers.Add(new Layer(reader, this));
            }

            foreach (Layer layer in Layers)
            {
                foreach (Channel channel in layer.Channels)
                {
                    if (channel.ID != -2)
                    {
                        channel.LoadPixelData(reader);
                    }
                }

                layer.MaskData.LoadPixelData(reader);
            }

            if (reader.BaseStream.Position % 2L == 1L)
            {
                reader.ReadByte();
            }

            reader.BaseStream.Position = position + num1;
        }

        private void LoadGlobalLayerMask(BinaryReverseReader reader)
        {
            uint num = reader.ReadUInt32();
            if (num <= 0U)
            {
                return;
            }
            reader.ReadBytes((int)num);
        }

        private void LoadImage(BinaryReverseReader reader)
        {
            _imageCompression = (ImageCompression)reader.ReadInt16();
            _imageData = new byte[_channels][];
            if (_imageCompression == ImageCompression.Rle)
            {
                reader.BaseStream.Position += _height * _channels * 2;
            }

            int columns = 0;
            switch (_depth)
            {
                case 1:
                    columns = _width;
                    break;
                case 8:
                    columns = _width;
                    break;
                case 16:
                    columns = _width * 2;
                    break;
            }

            for (int index1 = 0; index1 < (int)_channels; ++index1)
            {
                _imageData[index1] = new byte[_height * columns];
                switch (_imageCompression)
                {
                    case ImageCompression.Raw:
                        reader.Read(_imageData[index1], 0, _imageData[index1].Length);
                        break;
                    case ImageCompression.Rle:
                        for (int index2 = 0; index2 < _height; ++index2)
                        {
                            int startIdx = index2 * _width;
                            RleHelper.DecodedRow(reader.BaseStream, _imageData[index1], startIdx, columns);
                        }

                        break;
                }
            }
        }
    }
}

