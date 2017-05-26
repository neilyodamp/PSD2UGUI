using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Util9Slice
{

    public static Texture2D Create9Slice(Texture2D sourceTex, Params param)
    {

        SliceType sliceType = SliceType.Slice9;
        if((param.l <= 0 || param.r <= 0) && (param.t <= 0 || param.b <= 0))
        {
            Debug.LogWarning("l,t,r,b 设置异常 不能导出九宫格图");
            return sourceTex;
        }

        if(param.l <= 0 || param.r <= 0)
        {
            sliceType = SliceType.SliceTB;
        }
        else if(param.t <= 0 || param.b <= 0)
        {
            sliceType = SliceType.SliceLR;
        }
        else
        {
            sliceType = SliceType.Slice9;
        }

        Texture2D destTex = new Texture2D(param.destWidth, param.destHeight);
        Color[] destPix = new Color[destTex.width * destTex.height];

        TextureParam tParam = new TextureParam(sourceTex, destTex, destPix);

        int y = 0;
        while(y < destTex.height)
        {
            int x = 0;
            while(x < destTex.width)
            {
                SetPixels(x, y, param, tParam, sliceType);
                x++;
            }
            y++;
        }

        destTex.SetPixels(destPix);
        return destTex;

    }

    #region Left 3 Blocks
    private static bool IsBottomLeft(int x, int y, Params param)
    {

        return x <= param.l && y <= param.b;

    }

    private static bool IsTopLeft(int x, int y, Params param)
    {

        return x <= param.l && y >= param.destHeight - param.t;

    }

    private static bool IsLeft(int x, int y, Params param)
    {

        return x <= param.l && y >= param.b && y <= param.destHeight - param.t;

    }
    #endregion

    #region right 3 Blocks
    private static bool IsBottomRight(int x, int y, Params param)
    {

        return x >= param.destWidth - param.r && y <= param.b;

    }

    private static bool IsTopRight(int x, int y, Params param)
    {

        return x >= param.destWidth - param.r && y >= param.destHeight - param.t;

    }

    private static bool IsRight(int x, int y, Params param)
    {

        return x >= param.destWidth - param.r && y >= param.b && y <= param.destHeight - param.t;

    }
    #endregion

    #region middle 3 Blocks
    private static bool IsTop(int x, int y, Params param)
    {
        return x >= param.l && x <= param.destWidth - param.r && y >= param.destHeight - param.t;
    }

    private static bool IsBottom(int x, int y, Params param)
    {
        return x >= param.l && x <= param.destWidth - param.r && y <= param.b;
    }
    #endregion

    /// <summary>
    ///  SetPixes Bottom Left 左下角设置Pixels
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="param"></param>
    private static void SetPixelsBL(int x, int y, TextureParam tParam)
    {
        tParam.destPix[y * tParam.destTex.width + x] = tParam.sourceTex.GetPixel(x, y);
    }

    private static void SetPixelsLeft(int x, int y, Params param, TextureParam tParam)
    {
        float uvX = 1.0F / (tParam.sourceTex.width) * x;
        float uvY = param.b * 1.0F / tParam.sourceTex.height + 1.0F / tParam.sourceTex.height * param.scaleFactorY * (y - param.b);
        tParam.destPix[y * tParam.destTex.width + x] = tParam.sourceTex.GetPixelBilinear(uvX, uvY);
    }

    private static void SetPixelsTL(int x, int y, Params param, TextureParam tParam)
    {
        int sourceY = y + tParam.sourceTex.height - param.destHeight;
        tParam.destPix[y * tParam.destTex.width + x] = tParam.sourceTex.GetPixel(x, sourceY);
    }

    private static void SetPixelsTop(int x, int y, Params param, TextureParam tParam)
    {
        
        float uvX = param.l * 1.0f / tParam.sourceTex.width + 1.0f / tParam.sourceTex.width * param.scaleFactorX * (x - param.l);
        float uvY = (tParam.sourceTex.height * 1.0f - param.t) / tParam.sourceTex.height + 1.0f / tParam.sourceTex.height * (y - param.destHeight + param.t);
        tParam.destPix[y * tParam.destTex.width + x] = tParam.sourceTex.GetPixelBilinear(uvX, uvY);

    }

    private static void SetPixelsCenter(int x, int y, Params param, TextureParam tParam)
    {
        float uvX = param.l * 1.0f / tParam.sourceTex.width + 1.0f / tParam.sourceTex.width * param.scaleFactorX * (x - param.l);
        float uvY = param.b * 1.0f / tParam.sourceTex.height + 1.0f / tParam.sourceTex.height * param.scaleFactorY * (y - param.b);
        tParam.destPix[y * tParam.destTex.width + x] = tParam.sourceTex.GetPixelBilinear(uvX, uvY);
    }

    private static void SetPixelsBottom(int x, int y, Params param, TextureParam tParam)
    {

        float uvX = param.l * 1.0f / tParam.sourceTex.width + 1.0f / tParam.sourceTex.width * param.scaleFactorX * (x - param.l);
        float uvY = 1.0f / (tParam.sourceTex.height) * y;
        tParam.destPix[y * tParam.destTex.width + x] = tParam.sourceTex.GetPixelBilinear(uvX, uvY);

    }

    private static void SetPixelsBR(int x, int y, Params param, TextureParam tParam)
    {

        int sourceX = x + tParam.sourceTex.width - param.destWidth;
        tParam.destPix[y * tParam.destTex.width + x] = tParam.sourceTex.GetPixel(sourceX, y);

    }

    private static void SetPixelsRight(int x, int y, Params param, TextureParam tParam)
    {

        float uvX = ((tParam.sourceTex.width * 1.0f - param.r) / tParam.sourceTex.width) + 1.0f / tParam.sourceTex.width * (x - param.destWidth + param.r);
        float uvY = param.b * 1.0F / tParam.sourceTex.height + 1.0F / tParam.sourceTex.height * param.scaleFactorY * (y - param.b);
        tParam.destPix[y * tParam.destTex.width + x] = tParam.sourceTex.GetPixelBilinear(uvX, uvY);

    }

    private static void SetPixelsTR(int x, int y, Params param, TextureParam tParam)
    {

        int sourceX = x + tParam.sourceTex.width - param.destWidth;
        int sourceY = y + tParam.sourceTex.height - param.destHeight;
        tParam.destPix[y * tParam.destTex.width + x] = tParam.sourceTex.GetPixel(sourceX, sourceY);

    }

    private static void SetPixels(int x, int y, Params param, TextureParam tParam, SliceType type)
    {

        switch(type)
        {
            case SliceType.Slice9:

                if(IsBottomLeft(x, y, param))
                {
                    SetPixelsBL(x, y, tParam);
                }
                else if(IsLeft(x, y, param))
                {
                    SetPixelsLeft(x, y, param, tParam);
                }
                else if(IsTopLeft(x, y, param))
                {
                    SetPixelsTL(x, y, param, tParam);
                }
                else if(IsBottom(x, y, param))
                {
                    SetPixelsBottom(x, y, param, tParam);
                }
                else if(IsBottomRight(x, y, param))
                {
                    SetPixelsBR(x, y, param, tParam);
                }
                else if(IsRight(x, y, param))
                {
                    SetPixelsRight(x, y, param, tParam);
                }
                else if(IsTopRight(x, y, param))
                {
                    SetPixelsTR(x, y, param, tParam);
                }
                else if(IsTop(x, y, param))
                {
                    SetPixelsTop(x, y, param, tParam);
                }
                else
                {
                    SetPixelsCenter(x, y, param, tParam);
                }
                break;
            case SliceType.SliceLR:
                if(IsLeft(x, y, param))
                {
                    SetPixelsBL(x, y, tParam);
                }
                else if(IsRight(x, y, param))
                {
                    SetPixelsBR(x, y, param, tParam);
                }
                else
                {
                    SetPixelsBottom(x, y, param, tParam);
                }
                break;
            case SliceType.SliceTB:
                if(IsTop(x, y, param))
                {
                    SetPixelsTL(x, y, param, tParam);
                }
                else if(IsBottom(x, y, param))
                {
                    SetPixelsBL(x, y, tParam);
                }
                else
                {
                    SetPixelsLeft(x, y, param, tParam);
                }
                break;
            default:
                break;
        }

    }

}

public enum SliceType
{

    Slice9,        //九宫格
    SliceLR,       //左右
    SliceTB,       //上下

}
/// <summary>
/// 九宫格设置的参数
/// </summary>
public struct Params
{

    public int l;
    public int r;
    public int t;
    public int b;
    public int width;
    public int height;

    public int destWidth;   //最终9宫格图片的width
    public int destHeight;  //最终9宫格图片的height

    public float scaleFactorX;
    public float scaleFactorY;

    public Params(int l, int r, int t, int b, int width, int height, int destWidth, int destHeight, float factorX, float factorY)
    {

        this.l = l;
        this.r = r;
        this.t = t;
        this.b = b;
        this.width = width;
        this.height = height;
        this.destWidth = destWidth;
        this.destHeight = destHeight;
        scaleFactorX = factorX;
        scaleFactorY = factorY;
    }

}

public struct TextureParam
{

    public Texture2D sourceTex;
    public Texture2D destTex;
    public Color[] destPix;

    public TextureParam(Texture2D srcTex, Texture2D destTex, Color[] destPix)
    {
        sourceTex = srcTex;
        this.destTex = destTex;
        this.destPix = destPix;
    }
}
