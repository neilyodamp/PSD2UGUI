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

    public enum Direction
    {
        LeftToRight = 1,
        RightToLeft = 2,
        ButtomToTop =3,
        TopToButtom = 4
    }

    public enum Anchor
    {
        LeftTop,
        LeftMiddle,
        LeftButtom,
        CenterTop,
        CenterMiddle,
        CenterButtom,
        RightTop,
        RightMiddle,
        RightButtom,
    }

    public class PsdControl
    {
        
        public const string BTN = "btn_";
        public const string IMAGE = "img_";
        public const string TEXTURE = "tex_";
        public const string SCROLL = "src_";
        public const string PROGRESS = "pgr_";
        public const string SLIDER = "sld_";

        public const string LEFT2RIGHT = "@L2R";
        public const string RIGHT2LEFT = "@R2L";
        public const string BUTTOM2TOP = "@B2T";
        public const string TOP2BUTTOM = "@T2B";

        public const string SIZE = "@size";

        public const string ANCHOR_LEFTTOP = "@lt";
        public const string ANCHOR_LEFTMIDDLE = "@lm";
        public const string ANCHOR_LEFTBUTTOM = "@lb";
        public const string ANCHOR_CENTERTOP = "@ct";
        public const string ANCHOR_CENTERMIDDLE = "@cm";
        public const string ANCHOR_CENTERBUTTOM = "@cb";
        public const string ANCHOR_RIGHTTOP = "@rt";
        public const string ANCHOR_RIGHTMIDDLE = "@rm";
        public const string ANCHOR_RIGHTBUTTOM = "@rb";

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

        public static UINode CreateScrollRect(Layer layer)
        {
            bool horizontal = false;
            bool vertical = true;

            if (layer.Name.Contains(RIGHT2LEFT) || layer.Name.Contains(LEFT2RIGHT))
            {
                horizontal = true;
            }
            if (layer.Name.Contains(TOP2BUTTOM) || layer.Name.Contains(BUTTOM2TOP))
            {
                vertical = true;
            }

            Layer sizeLayer = null;

            foreach (var child in layer.Children)
            {
                if (!child.IsTextLayer && !PsdUtils.IsGroupLayer(child))
                {
                    
                    if (child.Name.ContainsIgnoreCase("@size"))
                    {
                        sizeLayer = child;
                    }
                }
            }
            if (sizeLayer == null)
            {
                Debug.LogError("Scroll Rect can't find @size");
                return null;
            }

            GameObject scrollRectGo = new GameObject(layer.Name);
            RectTransform rectTransform = scrollRectGo.AddComponent<RectTransform>();
            ScrollRect scrollRect = scrollRectGo.AddComponent<ScrollRect>();

            float width = sizeLayer.Rect.width;
            float height = sizeLayer.Rect.height;

            rectTransform.sizeDelta = new Vector2(width, height);

            GameObject viewPortGo = new GameObject("Viewport");
            RectTransform viewPortTransform = viewPortGo.AddComponent<RectTransform>();
            viewPortGo.AddComponent<UnityEngine.UI.Mask>();
            Image maskImage = viewPortGo.AddComponent<Image>();
            maskImage.color = new Color(1, 1, 1, 0.01f);
            viewPortTransform.sizeDelta = new Vector2(width,height);
            
            GameObject contentGo = new GameObject("Content");
            RectTransform contentTransform = contentGo.AddComponent<RectTransform>();
            contentTransform.sizeDelta = new Vector2(width, height);

            scrollRect.viewport = viewPortTransform;
            scrollRect.content = contentTransform;

            scrollRect.vertical = vertical;
            scrollRect.horizontal = horizontal;

            viewPortTransform.anchorMax = new Vector2(1, 1);
            viewPortTransform.anchorMin = new Vector2(0, 0);
            viewPortTransform.pivot = new Vector2(0,1);

            contentTransform.anchorMax = new Vector2(0, 1);
            contentTransform.anchorMin = new Vector2(0, 1);
            contentTransform.pivot = new Vector2(0,1);


            UINode scrollNode = new UINode();
            scrollNode.rect = sizeLayer.Rect;
            scrollNode.Go = scrollRectGo;

            UINode viewportNode = new UINode();
            viewportNode.rect = sizeLayer.Rect;
            viewportNode.Go = viewPortGo;
            viewportNode.pivot = viewPortTransform.pivot;
            

            UINode contentNode = new UINode();
            contentNode.rect = sizeLayer.Rect;
            contentNode.Go = contentGo;
            contentNode.pivot = contentTransform.pivot;

            scrollNode.children.Add(viewportNode);
            viewportNode.children.Add(contentNode);

            layer.Children.Remove(sizeLayer);

            SetAnchor(rectTransform, layer.Name);

            PsdImporter.ExportTree(layer.Children, contentNode);

            layer.Children.Clear();

            return  scrollNode;
        }

        public static UINode CreateProgress(Layer layer)
        {
            Direction direction = Direction.LeftToRight;
            if(layer.Name.ContainsIgnoreCase(RIGHT2LEFT))
            {
                direction = Direction.RightToLeft;
            }else if(layer.Name.ContainsIgnoreCase(TOP2BUTTOM))
            {
                direction = Direction.TopToButtom;
            }
            else if(layer.Name.ContainsIgnoreCase(BUTTOM2TOP))
            {
                direction = Direction.ButtomToTop;
            }

            Layer fgLayer = null;
            Layer bgLayer = null;

            foreach(var child in layer.Children)
            {
                if(!child.IsTextLayer && !PsdUtils.IsGroupLayer(child))
                {
                    if(child.Name.ContainsIgnoreCase("@fg"))
                    {
                        fgLayer = child;       
                    }
                    if (child.Name.ContainsIgnoreCase("@bg"))
                    {
                        bgLayer = child;
                    }

                }
            }
            if(fgLayer == null)
            {
                Debug.LogError("progress can't find @fg");
                return null;
            }

            //GameObject go
            GameObject progressGo = new GameObject(layer.Name);

            float width = fgLayer.Rect.width;
            float height = fgLayer.Rect.height;
            RectTransform rectTransform = progressGo.AddComponent<RectTransform>();

            rectTransform.sizeDelta = new Vector2(width,height);

            GameObject fgGo = new GameObject("Foreground");
            RectTransform fgRectTransform = fgGo.AddComponent<RectTransform>();
            Image fgImage = fgGo.AddComponent<Image>();
            fgRectTransform.sizeDelta = new Vector2(width, height);
            PsdImporter.CreateSprite(fgLayer, fgImage);

            GameObject maskGo = new GameObject("Mask");
            RectTransform maskRectTransform = maskGo.AddComponent<RectTransform>();
            Image maskImg = maskGo.AddComponent<Image>();
            maskGo.AddComponent<UnityEngine.UI.Mask>();
            maskRectTransform.sizeDelta = new Vector2(width, height);
            //maskImg.sprite = Resources.Load<Sprite>("unity_builtin_extra/UIMask");
            maskImg.color = new Color(1, 1, 1, 0.01f);

            GameObject bgGo;
            RectTransform bgRectTransform = null;
            if (bgLayer != null)
            {
                float bgWidth = bgLayer.Rect.width;
                float bgHeight = bgLayer.Rect.height;

                bgGo = new GameObject("Background");
                bgRectTransform = bgGo.AddComponent<RectTransform>();
                Image bgImage = bgGo.AddComponent<Image>();
                bgRectTransform.sizeDelta = new Vector2(bgWidth, bgHeight);
                PsdImporter.CreateSprite(bgLayer, bgImage);
                bgRectTransform.SetParent(rectTransform);
                bgRectTransform.localPosition = Vector3.zero;
                layer.Children.Remove(bgLayer);
            }

           

            Vector2 pivotOrAnchor = Vector2.zero;

            switch (direction)
            {
                case Direction.LeftToRight: pivotOrAnchor = new Vector2(0, 0.5f);break;
                case Direction.RightToLeft: pivotOrAnchor = new Vector2(1, 0.5f);break;
                case Direction.TopToButtom: pivotOrAnchor = new Vector2(0.5f, 1); break;
                case Direction.ButtomToTop: pivotOrAnchor = new Vector2(0.5f,0);break;
            }


            maskRectTransform.SetParent(rectTransform);
            maskRectTransform.localPosition = new Vector3((pivotOrAnchor.x - 0.5f)*fgLayer.Rect.width,(pivotOrAnchor.y - 0.5f)*fgLayer.Rect.height,0);

            fgRectTransform.SetParent(maskRectTransform);
            fgRectTransform.localPosition = Vector3.zero;

            maskRectTransform.pivot = pivotOrAnchor;
            fgRectTransform.pivot = pivotOrAnchor;
            fgRectTransform.anchorMax = pivotOrAnchor;
            fgRectTransform.anchorMin = pivotOrAnchor;

            layer.Children.Remove(fgLayer);

            SetAnchor(rectTransform, layer.Name);

            UINode node = new UINode();
            node.rect = fgLayer.Rect;
            node.Go = progressGo;
            return node;

        }
        public static UINode CreateImage(Layer layer, bool isTexture = false)
        {
            Layer imgLayer = layer;
            foreach (var child in layer.Children)
            {
                if (!child.IsTextLayer && !PsdUtils.IsGroupLayer(child))
                {
                    imgLayer = child;
                    break;
                }
            }

            layer.Children.Remove(imgLayer);
            float width = imgLayer.Rect.width ;
            float height = imgLayer.Rect.height;

            GameObject go = new GameObject(layer.Name);
            RectTransform goRectTransform = go.AddComponent<RectTransform>();
            
            goRectTransform.sizeDelta = new Vector2(width,height);
            if (!isTexture)
            {
                Image img = go.AddComponent<Image>();
                PsdImporter.CreateSprite(imgLayer, img);
                if(imgLayer.Is9Slice)
                {
                    img.type = Image.Type.Sliced;
                }
            }
            else
            {
                RawImage img = go.AddComponent<RawImage>();
                img.texture = PsdImporter.CreateTexture2D(imgLayer);
            }

            SetAnchor(goRectTransform, layer.Name);
            UINode node = new UINode();
            node.rect = imgLayer.Rect;
            node.Go = go;

            return node;
        }

        public static UINode CreateUIButton(Layer layer)
        {
            Layer imgLayer = layer;
            foreach (var child in layer.Children)
            {
                if (!child.IsTextLayer && !PsdUtils.IsGroupLayer(child))
                {
                    imgLayer = child;
                    break;
                }
            }
            layer.Children.Remove(imgLayer);
            float width = imgLayer.Rect.width ;
            float height = imgLayer.Rect.height;

            GameObject go = new GameObject(layer.Name);
            RectTransform goRectTransform = go.AddComponent<RectTransform>();
            Image img = go.AddComponent<Image>();
            goRectTransform.sizeDelta = new Vector2(width, height);
            PsdImporter.CreateSprite(imgLayer, img);

            if(imgLayer.Is9Slice)
            {
                img.type = Image.Type.Sliced;
            }
            go.AddComponent<Button>();

            SetAnchor(goRectTransform, layer.Name);

            UINode node = new UINode();
            node.rect = imgLayer.Rect;
            node.Go = go;

            return node;
        }

        public static UINode CreateRectTransform(Layer layer)
        {
            GameObject go = new GameObject(layer.Name);
            RectTransform goRectTransform = go.AddComponent<RectTransform>();

            UINode node = new UINode();
            node.rect = layer.Rect;
            node.Go = go;

            SetAnchor(goRectTransform, layer.Name);

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

            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            if(rectTransform == null)
            {
                rectTransform = gameObject.AddComponent<RectTransform>();
            }

            textUI.text = layer.Text;
            textUI.font = font;
            textUI.horizontalOverflow = HorizontalWrapMode.Overflow;
            textUI.verticalOverflow = VerticalWrapMode.Overflow;
            textUI.raycastTarget = false;

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

            SetAnchor(rectTransform, layer.Name);

            UINode node = new UINode();
            node.rect = layer.Rect;
            node.Go = gameObject;
            return node;
        }

        public static void SetAnchor(RectTransform rectTransform,string name)
        {
            Debug.Log(name);
            Debug.Log(rectTransform.name);
            Vector4 anchor = PsdUtils.GetAnchorWithGroupName(name);
            Debug.Log(anchor);


            rectTransform.anchorMin = new Vector2(anchor.x, anchor.y);
            rectTransform.anchorMax = new Vector2(anchor.z, anchor.w);
        }

    }
}