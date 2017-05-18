namespace PhotoshopFile
{
    public class ResolutionInfo : ImageResource
    {
        public ResolutionInfo(ImageResource imgRes)
            : base(imgRes)
        {

            //文档 四 - 1 ID 1005 
            BinaryReverseReader dataReader = imgRes.DataReader;

            //这里解析是错的, 但解的字节数没错,反正这些数据没用，就没修改了，要用的时候参考文档修改
            dataReader.ReadInt16();
            dataReader.ReadInt32();
            dataReader.ReadInt16();
            dataReader.ReadInt16();
            dataReader.ReadInt32();
            dataReader.ReadInt16();

            dataReader.Close();
        }
    }
}
