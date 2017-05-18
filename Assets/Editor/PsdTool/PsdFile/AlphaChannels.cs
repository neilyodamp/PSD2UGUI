namespace PhotoshopFile
{
    public class AlphaChannels : ImageResource
    {
        public AlphaChannels(ImageResource imgRes)
            : base(imgRes)
        {
            // 文档 四 - 2 ID 1006
            BinaryReverseReader dataReader = imgRes.DataReader;
            while (dataReader.BaseStream.Length - dataReader.BaseStream.Position > 0L)
            {
                byte length = dataReader.ReadByte();

                dataReader.ReadChars(length);
            }

            dataReader.Close();
        }
    }
}
