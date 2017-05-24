

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.UI;
using UnityEngine;
using PhotoshopFile;

namespace PsdLayoutTool
{
    public enum GroupClass
    {
        Empty = 0,          //空组 会在生成的时候删除
        RectTransform = 1,      //这个会生成一个RectTransform，会根据子节点的UI 决定大小
        Image = 2,
        Texture = 3,
        ScrollRect = 4,
        Progress = 5,
        Slider = 6,
        Button = 7,
        Text = 8,
    }

    public class PsdControl
    {
        public const string BTN = "btn_";
        public const string IMAGE = "img_";
        public const string TEXTURE = "tex_";
        public const string SCROLL = "src_";
        public const string PROGRESS = "pgr_";
        public const string SLIDER = "sld_";

        public static GroupClass CheckGroupClass(Layer layer)
        {
            if (layer.Children.Count == 0)
                return GroupClass.Empty;
            
            if(layer.Name.StartsWith(BTN))
            {
                return GroupClass.Button;
            }
            if (layer.Name.StartsWith(IMAGE))
            {
                return GroupClass.Image;
            }
            if (layer.Name.StartsWith(TEXTURE))
            {
                return GroupClass.Texture;
            }
            if (layer.Name.StartsWith(SCROLL))
            {
                return GroupClass.ScrollRect;
            }
            if (layer.Name.StartsWith(PROGRESS))
            {
                return GroupClass.Progress;
            }
            if (layer.Name.StartsWith(SLIDER))
            {
                return GroupClass.Slider;
            }

            int imgLayerCount = 0;
            foreach(var child in layer.Children)
            {
                // 如果有图像layer
                if (!child.IsTextLayer && !PsdUtils.IsGroupLayer(child))
                    imgLayerCount ++;
            }

            if(imgLayerCount == 1)
            {
                return GroupClass.Image;
            }

            if (layer.Children.Count > 0)
                return GroupClass.RectTransform;

            return GroupClass.Empty;
        }

        public static UINode CreateImage(Layer layer)
        {
            Layer imgLayer = layer;
            foreach(var child in layer.Children)
            {
                if (!child.IsTextLayer && !PsdUtils.IsGroupLayer(child))
                {
                    imgLayer = child;
                    break;
                }
            }
            layer.Children.Remove(imgLayer);
            float width = imgLayer.Rect.width / PsdImporter.PixelsToUnits;
            float height = imgLayer.Rect.height / PsdImporter.PixelsToUnits;

            GameObject go = new GameObject(layer.Name);
            RectTransform goRectTransform = go.AddComponent<RectTransform>();
            Image img = go.AddComponent<Image>();
            goRectTransform.sizeDelta = new Vector2(width,height);

            img.sprite = PsdImporter.CreateSprite(imgLayer);

            UINode node = new UINode();
            node.rect = imgLayer.Rect;
            node.go = go;

            return node;
        }

        public static UINode CreateRectTransform(Layer layer)
        {
            GameObject go = new GameObject(layer.Name);
            RectTransform goRectTransform = go.AddComponent<RectTransform>();

            UINode node = new UINode();
            node.rect = layer.Rect;
            node.go = go;

            return node;
        }

        public static UINode CreateUIText(Layer layer)
        {
            Color color = layer.FillColor;
            GameObject gameObject = new GameObject(layer.Name);

            Font font = PsdImporter.GetFontInfo();

            Text textUI = gameObject.GetComponent<Text>();
            if (textUI == null)
            {
                textUI = gameObject.AddComponent<Text>();
            }

            textUI.text = layer.Text;
            textUI.font = font;
            textUI.horizontalOverflow = HorizontalWrapMode.Overflow;//yanruTODO修改
            textUI.verticalOverflow = VerticalWrapMode.Overflow;
            textUI.raycastTarget = false;//can not  click text by yanru 2016-06-16 19:27:41

            //描边信息
            if (layer.TextOutlineColor.a != 0f)
            {
                Outline outline = textUI.GetComponent<Outline>();
                if (outline == null)
                    outline = textUI.gameObject.AddComponent<Outline>();

                outline.effectColor = layer.TextOutlineColor;
                outline.effectDistance = new Vector2(layer.OutLineDis, layer.OutLineDis);
            }

            float fontSize = layer.FontSize;
            float ceiling = Mathf.Ceil(fontSize);

            textUI.fontSize = (int)fontSize;
            textUI.color = color;
            textUI.alignment = TextAnchor.MiddleCenter;

            switch (layer.Justification)
            {
                case TextJustification.Left:
                    textUI.alignment = TextAnchor.MiddleLeft;
                    break;
                case TextJustification.Right:
                    textUI.alignment = TextAnchor.MiddleRight;
                    break;
                case TextJustification.Center:
                    textUI.alignment = TextAnchor.MiddleCenter;
                    break;
            }

            UINode node = new UINode();
            node.rect = layer.Rect;
            node.go = gameObject;
            return node;
        }

    }
}