using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PhotoshopFile
{
    public class BinaryReverseReader : BinaryReader
    {
        public BinaryReverseReader(Stream stream):base(stream,Encoding.Default)
        {
        }

        public string ReadStringNew(int charCount)
        {
            return GetEncodeStr(ReadBytes(charCount));
        }

        private string GetEncodeStr(byte [] bytes)
        {
            return new string(Encoding.Default.GetChars(bytes));
        }

        public override short ReadInt16()
        {
            short num = base.ReadInt16();
            num = ReverseBytes(num);
            return num;
        }

        public override int ReadInt32()
        {
            int num = base.ReadInt32();
            num = ReverseBytes(num);
            return num;
        }
        public override long ReadInt64()
        {
            long num = base.ReadInt64();
            num = ReverseBytes(num);
            return num;
        }

        public override ushort ReadUInt16()
        {
            ushort num = base.ReadUInt16();
            num = ReverseBytes(num);
            return num;
        }

        public override uint ReadUInt32()
        {
            uint num = base.ReadUInt32();
            num = ReverseBytes(num);
            return num;
        }

        public override ulong ReadUInt64()
        {
            ulong num = base.ReadUInt64();
            num = ReverseBytes(num);
            return num;
        }

        public string ReadPascalString()
        {
            byte num1 = ReadByte();
            byte[] bytes = ReadBytes(num1);
            if (num1 % 2 == 0)
            {
                ReadByte();
            }
            return GetEncodeStr(bytes);
        }

        public float ReadFloat()
        {
            string str = string.Empty;

            try
            {
                for (int index = PeekChar(); index != 10; index = PeekChar())
                {
                    if (index != 32)
                    {
                        str = str + ReadChar();
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (ArgumentException)
            {
                UnityEngine.Debug.LogError("An invalid character was found in the string.");
            }

            if (string.IsNullOrEmpty(str))
            {
                return 0.0f;
            }

            return Convert.ToSingle(str);
        }

        public string ReadString(bool testPrintLog = false)
        {
            string str = string.Empty;
            List<byte> bytelist = new List<byte>();
            int readCount = 0;
            try
            {
                while (BaseStream.Position < BaseStream.Length)
                {
                    byte byte1 = ReadByte();

                    readCount++;
                    if (byte1 == 0) //byte=0是ASCII码表中的空字符
                    {
                        byte byte2 = ReadByte();
                        if (byte2 != 0) //高地位都为0的话就真的 没有字符
                        {
                            bytelist.Add(byte2);
                            bytelist.Add(0);

                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        byte byte2 = ReadByte();

                        if (byte2 != 0)
                        {
                            if (byte1 != 0 && byte2 != 0 && !IsChinese(byte2, byte1))
                            {
                                break;
                            }
                            else
                            {
                                bytelist.Add(byte2);
                                bytelist.Add(byte1);
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            catch (ArgumentException)
            {
                UnityEngine.Debug.LogError("An invalid character was found in the string.");
            }
            byte[] res = new byte[bytelist.Count];
            for (int index = 0; index < bytelist.Count; index++)
                res[index] = bytelist[index];

            str = new string(Encoding.Unicode.GetChars(res));
            return str;
        }

        private bool IsChinese(byte item1, byte item2)
        {
            return item1 >= 0x00 && item1 <= 0xBF &&
                item2 >= 0x4e && item2 <= 0x9f;
        }

        public void Seek(string search)
        {
            byte[] bytes = Encoding.Default.GetBytes(search);
            Seek(bytes);
        }

        private Int16 ReverseBytes(Int16 value)
        {
            return (Int16)ReverseBytes((UInt16)value);
        }

        private Byte ReverseBytes(Byte value)
        {
            return (Byte)ReverseBytes((Byte)value);
        }

        private Int32 ReverseBytes(Int32 value)
        {
            return (Int32)ReverseBytes((UInt32)value);
        }

        private Int64 ReverseBytes(Int64 value)
        {
            return (Int64)ReverseBytes((UInt64)value);
        }

        private UInt16 ReverseBytes(UInt16 value)
        {
            return (UInt16)((value & 0xFFU) << 8 | (value & 0xFF00U) >> 8);
        }

        private UInt32 ReverseBytes(UInt32 value)
        {
            return (value & 0x000000FFU) << 24 | (value & 0x0000FF00U) << 8 |
                (value & 0x00FF0000U) >> 8 | (value & 0xFF000000U) >> 24;
        }

        private UInt64 ReverseBytes(UInt64 value)
        {
            return (value & 0x00000000000000FFUL) << 56 | (value & 0x000000000000FF00UL) << 40 |
                (value & 0x0000000000FF0000UL) << 24 | (value & 0x00000000FF000000UL) << 8 |
                    (value & 0x000000FF00000000UL) >> 8 | (value & 0x0000FF0000000000UL) >> 24 |
                    (value & 0x00FF000000000000UL) >> 40 | (value & 0xFF00000000000000UL) >> 56;
        }

        private void Seek(byte[] search)
        {
            // read continuously until we find the first byte
            while (BaseStream.Position < BaseStream.Length)
            {
                byte temp = ReadByte();

                if (temp == search[0])
                {
                    break;
                }
            }

            // ensure we haven't reached the end of the stream
            if (BaseStream.Position >= BaseStream.Length)
            {
                return;
            }

            // ensure we have found the entire byte sequence
            for (int index = 1; index < search.Length; ++index)
            {
                byte byteTemp = ReadByte();

                if (byteTemp != search[index])
                {
                    Seek(search);
                    break;
                }
            }
        }

    }
}
