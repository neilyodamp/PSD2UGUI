using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using PhotoshopFile;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PsdLayoutTool
{
    public static class PsdImporter
    {
        public const string PUBLIC_IMG_HEAD = "public_";
        public const string IMG_REF = "&";

        public const string PSD_TAIL = ".psd";
        public const string IMG_TAIL = ".png";
        public const string NO_NAME_HEAD = "no_name_";
        public const string CURR_IMG_PATH_ROOT = "export_image/";

        private const string TEST_FONT_NAME = "";
        private const string PUBLIC_IMG_PATH = @"\public_images";
        private const string SCROLL = "@ScrollView";
        private const string SCROLL_SIZE = "@Size";

        private static string _currentPath;
        private static string _texturePath;

        private static GameObject _rootPsdGameObject;
        private static GameObject _currentGroupGameObject;
        private static float _currentDepth;
        private static float _depthStep;
        private static List<string> _btnNameList;
        private static bool _useUnityUI = true;
        private static bool _layoutInScene;
        private static bool _createPrefab;
        private static Vector2 _canvasSize;
        private static string _psdName;
        private static GameObject _canvasObj;
        private static string _textFont = "";
        private static bool _useRealImageSize = false;
        private static int _nullImageIndex = 0;
        private static Dictionary<GameObject, Vector3> _positionDic;
        private static Dictionary<string, Sprite> _imageDic;
        //放大缩小的IMG
        private static Dictionary<string, List<Image>> _scaleImgDic;

        private static UINode _rootNode;

        public static Vector3 GetCanvasPosition()
        {
            return _canvasObj.transform.position;
        }
        public static GameObject CanvasObj
        {
            get { return _canvasObj; }
        }


        public static float MaximumDepth { get; set; }
        public static float PixelsToUnits { get; set; }

        public static bool UseUnityUI
        {
            get { return _useUnityUI; }
            set { _useUnityUI = value; }
        }

        public static Vector2 ScreenResolution = new Vector2(1334, 750);
        public static Vector2 LargeImageAlarm = new Vector2(512, 512);

        public static string textFont
        {
            get
            {
                if (_textFont == "")
                {
                    _textFont = "Arial.ttf";
                }
                return _textFont;
            }
            set
            {
                _textFont = value;
            }
        }
        static PsdImporter()
        {
            _textFont = TEST_FONT_NAME;

            MaximumDepth = 1;
            PixelsToUnits = 100;
        }

        public static void LayoutInCurrentScene(string assetPath, bool useCurImageSize = false)
        {
            _layoutInScene = true;
            _createPrefab = false;
            _useRealImageSize = useCurImageSize;

            Import(assetPath);
        }

        public static void GeneratePrefab(string assetPath)
        {

        }

        public static void AddScaleImg(string path, Image img)
        {
            if (!_scaleImgDic.ContainsKey(path))
            {
                List<Image> imgs = new List<Image>();
                imgs.Add(img);
                _scaleImgDic[path] = imgs;
            }
            _scaleImgDic[path].Add(img);
        }

        private static void Import(string asset)
        {
            _scaleImgDic = new Dictionary<string, List<Image>>();

            _imageDic = new Dictionary<string, Sprite>();

            _btnNameList = new List<string>();

            _nullImageIndex = 0;

            _positionDic = new Dictionary<GameObject, Vector3>();

            _currentDepth = MaximumDepth;

            string fullPath = Path.Combine(PsdUtils.GetFullProjectPath(), asset.Replace('\\', '/'));

            PsdFile psd = new PsdFile(fullPath);

            _canvasSize = ScreenResolution;

            _depthStep = psd.Layers.Count != 0 ? MaximumDepth / psd.Layers.Count : 0.1f;

            int lastSlash = asset.LastIndexOf('/');

            string assetPathWithoutFilename = asset.Remove(lastSlash + 1, asset.Length - (lastSlash + 1));

            _psdName = asset.Replace(assetPathWithoutFilename, string.Empty).Replace(".psd", string.Empty);

            _currentPath = PsdUtils.GetFullProjectPath() + "Assets/" + CURR_IMG_PATH_ROOT;
            _texturePath = Path.Combine(_currentPath, "Textures");
            _currentPath = Path.Combine(_currentPath, _psdName);

            CreateDic(_texturePath);
            CreateDic(_currentPath);

            if (_layoutInScene || _createPrefab)
            {
                if (UseUnityUI)
                {
                    CreateUIEventSystem();
                    CreateUICanvas();
                }

                //create ui Root
                _rootPsdGameObject = CreateObj(_psdName);
                _rootPsdGameObject.transform.SetParent(_canvasObj.transform, false);
                _rootNode = new UINode();
                _rootNode.Go = _rootPsdGameObject;
                
                RectTransform rectRoot = _rootPsdGameObject.GetComponent<RectTransform>();
                
                rectRoot.anchorMin = new Vector2(0, 0);
                rectRoot.anchorMax = new Vector2(1, 1);
                rectRoot.offsetMin = Vector2.zero;
                rectRoot.offsetMax = Vector2.zero;

                _rootNode.rect = new Rect(0, 0, ScreenResolution.x, ScreenResolution.y);

                Vector3 rootPos = Vector3.zero;
                _rootPsdGameObject.transform.position = Vector3.zero;
                _currentGroupGameObject = _rootPsdGameObject;
            }

            List<Layer> tree = BuildLayerTree(psd.Layers);

            ExportTree(tree, _rootNode);
            PsdUtils.CreateUIHierarchy(_rootNode);
            PsdUtils.UpdateAllUINodeRectTransform(_rootNode);

            if (_createPrefab)
            {
                UnityEngine.Object prefab = PrefabUtility.CreateEmptyPrefab(asset.Replace(".psd", ".prefab"));
                PrefabUtility.ReplacePrefab(_rootPsdGameObject, prefab);
            }

            UpdateScaleImgSprite();
            AssetDatabase.Refresh();
        }

        private static void ResetRootRect()
        {
            RectTransform root_rect = _rootPsdGameObject.GetComponent<RectTransform>();
            root_rect.anchorMin = Vector2.zero;
            root_rect.anchorMax = new Vector2(1, 1);
            root_rect.offsetMin = Vector2.zero;
            root_rect.offsetMax = Vector2.zero;
        }

        private static Sprite RescriteBtnSprite(List<Sprite> canUseSpriteList, Sprite sprite)
        {
            return null;
        }



        private static void DestroyItem(Transform child)
        {
            if (child != null)
                GameObject.DestroyImmediate(child.gameObject);
        }

        private static List<Layer> BuildLayerTree(List<Layer> flatLayers)
        {
            if (flatLayers == null)
            {
                return null;
            }
            flatLayers.Reverse();

            List<Layer> tree = new List<Layer>();
            Layer currentGroupLayer = null;
            Stack<Layer> previousLayers = new Stack<Layer>();

            foreach (Layer layer in flatLayers)
            {
                if (IsEndGroup(layer))
                {
                    if (previousLayers.Count > 0)
                    {
                        Layer previousLayer = previousLayers.Pop();
                        previousLayer.Children.Add(currentGroupLayer);
                        currentGroupLayer = previousLayer;
                    }
                    else if (currentGroupLayer != null)
                    {
                        tree.Add(currentGroupLayer);
                        currentGroupLayer = null;
                    }
                }
                else if (IsStartGroup(layer))
                {
                    if (currentGroupLayer != null)
                    {
                        previousLayers.Push(currentGroupLayer);
                    }

                    currentGroupLayer = layer;
                }
                else if (layer.Rect.width != 0 && layer.Rect.height != 0)
                {
                    // It must be a text layer or image layer
                    if (currentGroupLayer != null)
                    {
                        currentGroupLayer.Children.Add(layer);
                    }
                    else
                    {
                        //      Debug.Log(Time.time + ",add layer layer.name=" + layer.Name);
                        tree.Add(layer);
                    }
                }
            }

            if (tree.Count == 0 && currentGroupLayer != null && currentGroupLayer.Children.Count > 0)
            {
                tree.Add(currentGroupLayer);
            }

            return tree;
        }

        private static string MakeNameSafe(string name)
        {
            return name;
        }

        private static bool IsStartGroup(Layer layer)
        {
            return layer.IsPixelDataIrrelevant;
        }

        private static bool IsEndGroup(Layer layer)
        {
            return layer.Name.Contains("</Layer set>") ||
                layer.Name.Contains("</Layer group>") ||
                (layer.Name == " copy" && layer.Rect.height == 0);
        }

        private static string GetRelativePath(string fullPath)
        {
            return fullPath.Replace(PsdUtils.GetFullProjectPath(), string.Empty);
        }

        public static void ExportTree(List<Layer> tree,UINode parentNode)
        {
            for (int i = tree.Count - 1; i >= 0; i--)
            {
                ExportTreeNode(tree[i] , parentNode);
            }
        }
        private static void ExportTreeNode(Layer layer,UINode parentNode)
        {
            UpdateLayerName(layer, MakeNameSafe(layer.Name));
            if (PsdUtils.IsGroupLayer(layer))
            {
                ExportGroup(layer,parentNode);
            }
            else
            {
                ExportLayer(layer,parentNode);
            }
        }
        //图像和text
        private static void ExportLayer(Layer layer,UINode parentNode) // 
        {
            if(!layer.IsTextLayer)
            {
               
            }
            else
            {
                UINode node = PsdControl.CreateUIText(layer);
                parentNode.children.Add(node);
            }
        }
        private static void ExportGroup(Layer layer,UINode parentNode)
        {
            GroupClass groupClass = PsdControl.CheckGroupClass(layer);
            UINode node = null;
            if(groupClass == GroupClass.Image)
            {
                node = PsdControl.CreateImage(layer);
            }
            else if(groupClass == GroupClass.RectTransform)
            {
                node = PsdControl.CreateRectTransform(layer);
            }
            else if(groupClass == GroupClass.Progress)
            {
                node = PsdControl.CreateProgress(layer);
            }
            else if(groupClass == GroupClass.Texture)
            {
                node = PsdControl.CreateImage(layer, true);
            }
            else if(groupClass == GroupClass.Button)
            {
                node = PsdControl.CreateUIButton(layer);
            }
            else if(groupClass == GroupClass.ScrollRect)
            {
                node = PsdControl.CreateScrollRect(layer);
            }
            else if(groupClass == GroupClass.Empty)
            {
                return;
            }
            else
            {
                return;
            }
            //添加了node
            if (node != null)
            {
                parentNode.children.Add(node);
                ExportTree(layer.Children,node);
            }
        }

        private static void CreateDic(string path)
        {
            Directory.CreateDirectory(path);
        }

        public static bool ContainsIgnoreCase(this string source, string toCheck)
        {
            return source.IndexOf(toCheck, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ReplaceIgnoreCase(this string str, string oldValue, string newValue)
        {
            return null;
        }

        private static string CreatePNG(Layer layer)
        {
            string file = string.Empty;

            if (layer.Children.Count == 0 && layer.Rect.width > 0)
            {
                Texture2D texture = DoCreatePNG(layer, out file);
                Vector2 size = layer.Rect.size;
                if (size.x >= LargeImageAlarm.x || size.y >= LargeImageAlarm.y)
                {
                    Debug.Log(Time.time + "图片=" + file + ",尺寸=" + size + "较大！考虑单拆图集！");
                }
                File.WriteAllBytes(file, texture.EncodeToPNG());
            }

            return file;
        }

        private static Texture2D DoCreatePNG(Layer layer, out string file, bool isTexture = false)
        {
            Texture2D texture = ImageDecoder.DecodeImage(layer);
            string writePath;
            file = isTexture ? GetTextureFilePath(layer, out writePath) : GetFilePath(layer, out writePath);
            if(!Directory.Exists(writePath))
            {
                Directory.CreateDirectory(writePath);
            }
            return texture;
        }

        private static string GetTextureFilePath(Layer layer, out string writePath)
        {
            string file = string.Empty;
            writePath = _texturePath;
            string layerName = PsdUtils.ClearNameTail(layer.Name);
            file = Path.Combine(writePath, layerName + ".png");
            return file;
        }

        public static string GetFilePath(Layer layer, out string writePath)
        {
            string file = string.Empty;
            writePath = _currentPath;
            string layerName = layer.Name;

            if(layerName.Contains(PUBLIC_IMG_HEAD))
            {
                int length = writePath.Length - 1;
                if(writePath.LastIndexOf(@"/") != -1)
                    length = writePath.LastIndexOf(@"/");

                writePath = writePath.Substring(0, length);
                writePath += PUBLIC_IMG_PATH;
            }
            //layerName = PsdUtils.TrimSliceReg(layerName);
            layerName = PsdUtils.ClearNameTail(layerName);
            file = Path.Combine(writePath, layerName + ".png");
            return file;
        }

        public static void CreateSprite(Layer layer, Image img)
        {
            Match match = PsdControl.colorImgRegex.Match(layer.Name);
            if(match.Success)
            {
                string numStr = match.Groups[1].Value;
                float alpha = float.Parse(numStr) / 100.0f;
                img.color = new Color(0, 0, 0, alpha);
            }
            else if(!layer.Name.StartsWith(PsdImporter.IMG_REF))
            {
                img.sprite = PsdImporter.CreateSpriteInternal(layer);
            }
            else
            {
                layer.Name = layer.Name.Replace(PsdImporter.IMG_REF, string.Empty);
                string writePath;
                string path = PsdImporter.GetFilePath(layer, out writePath);
                PsdImporter.AddScaleImg(path, img);
            }
        }

        private static Sprite CreateSpriteInternal(Layer layer)
        {
            return CreateSpriteInternal(layer, _psdName);
        }

        private static Sprite CreateSpriteInternal(Layer layer, string packingTag)
        {
            //Debug.Log("");
            Sprite sprite = null;

            if (layer.Children.Count == 0 && layer.Rect.width > 0)
            {
                string writePath;
                string file = GetFilePath(layer, out writePath);

                if (!_imageDic.ContainsKey(file))
                {
                    CreatePNG(layer);
                    sprite = ImportSprite(GetRelativePath(file), packingTag, layer.Is9Slice ? layer : null);
                    _imageDic.Add(file, sprite);
                }
                else
                {
                    return _imageDic[file];
                }

            }

            return sprite;
        }

        public static Texture2D CreateTexture2D(Layer layer)
        {
            string file = string.Empty;
            Texture2D texture = null;
            if(layer.Children.Count == 0 && layer.Rect.width > 0)
            {
                texture = DoCreatePNG(layer, out file, true);
                File.WriteAllBytes(file, texture.EncodeToPNG());
                string relativePathToSprite = GetRelativePath(file);

                AssetDatabase.ImportAsset(relativePathToSprite, ImportAssetOptions.ForceUpdate);
                TextureImporter textureImporter = AssetImporter.GetAtPath(relativePathToSprite) as TextureImporter;
                if(textureImporter != null)
                {
                    textureImporter.textureType = TextureImporterType.GUI;
                    textureImporter.mipmapEnabled = false;
                    //textureImporter.spriteImportMode = SpriteImportMode.Single;
                    textureImporter.maxTextureSize = 2048;
                }
                AssetDatabase.ImportAsset(relativePathToSprite, ImportAssetOptions.ForceUpdate);

                texture = (Texture2D)AssetDatabase.LoadAssetAtPath(relativePathToSprite, typeof(Texture2D));
            }
            
            return texture;

        }

        private static Sprite ImportSprite(string relativePathToSprite, string packingTag, Layer layer)
        {
            AssetDatabase.ImportAsset(relativePathToSprite, ImportAssetOptions.ForceUpdate);

            TextureImporter textureImporter = AssetImporter.GetAtPath(relativePathToSprite) as TextureImporter;
            if(textureImporter != null)
            {
                textureImporter.textureType = TextureImporterType.Sprite;
                textureImporter.mipmapEnabled = false;
                textureImporter.spriteImportMode = SpriteImportMode.Single;
                textureImporter.spritePivot = new Vector2(0.5f, 0.5f);
                textureImporter.maxTextureSize = 2048;
                textureImporter.spritePixelsPerUnit = PixelsToUnits;
                textureImporter.spritePackingTag = packingTag;
                textureImporter.alphaIsTransparency = true;
                if(null != layer)
                {
                    textureImporter.spriteBorder = layer.Border;
                }
            }

            AssetDatabase.ImportAsset(relativePathToSprite, ImportAssetOptions.ForceUpdate);

            Sprite sprite = (Sprite)AssetDatabase.LoadAssetAtPath(relativePathToSprite, typeof(Sprite));
            return sprite;
        }

        private static GameObject CreateObj(string objName)
        {
            string res = "";
            char[] charData = objName.ToCharArray();
            for (int i = 0; i < charData.Length; i++)
            {
                if (charData[i] >= 0x4e00 && charData[i] <= 0x9fbb)
                {
                    res += "n"; ;
                }
                else
                    res += charData[i];
            }

            objName = res;

            GameObject obj = new GameObject(objName);
            RectTransform rectTs = obj.AddComponent<RectTransform>();
            rectTs.sizeDelta = new Vector2(100 / PixelsToUnits, 100 / PixelsToUnits);

            return obj;
        }

        private static void CreateUIEventSystem()
        {
            if (!GameObject.Find("EventSystem"))
            {
                GameObject gameObject = CreateObj("EventSystem");
                gameObject.AddComponent<EventSystem>();
                gameObject.AddComponent<StandaloneInputModule>();
            }
        }

        private static void CreateUICanvas()
        {
            if (GameObject.Find("Canvas") != null)
            {
                _canvasObj = GameObject.Find("Canvas");
            }
            else
            {

                _canvasObj = CreateObj("Canvas");
            }

            Canvas canvas = _canvasObj.GetComponent<Canvas>();
            if (canvas == null)
                canvas = _canvasObj.AddComponent<Canvas>();

            CanvasScaler scaler = _canvasObj.GetComponent<CanvasScaler>();

            if (scaler == null)
                scaler = _canvasObj.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ScreenResolution;

#if UNITY_5
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

#else
            canvas.renderMode = RenderMode.WorldSpace;
#endif

            RectTransform transform = _canvasObj.GetComponent<RectTransform>();
            UpdateRectSize(ref transform, _canvasSize.x , _canvasSize.y);
            transform.position = new Vector3(_canvasSize.x/2,_canvasSize.y/2,0);

            scaler.dynamicPixelsPerUnit = PixelsToUnits;
            scaler.referencePixelsPerUnit = PixelsToUnits;

            GraphicRaycaster racaster = _canvasObj.GetComponent<GraphicRaycaster>();
            if (racaster == null)
                racaster = _canvasObj.AddComponent<GraphicRaycaster>();

        }

        private static void UpdateRectSize(ref RectTransform transform, float width, float height)
        {
            transform.sizeDelta = new Vector2(width, height);
        }

        private static void UpdateScaleImgSprite()
        {
            foreach(KeyValuePair<string, List<Image>> kvp in _scaleImgDic)
            {
                string path = kvp.Key.Replace(PsdUtils.GetFullProjectPath(), string.Empty);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Sprite sprite = (Sprite)AssetDatabase.LoadAssetAtPath(path, typeof(Sprite));
                if (null == sprite)
                {
                    Debug.LogWarning(string.Format("缺少引用资源 {0} ", path));
                }
                else
                {
                    foreach(var img in kvp.Value)
                    {
                        img.sprite = sprite;
                    }
                }
                
            }
        }  

        public static Font GetFontInfo()
        {
            Font font = null;

            if (textFont.Contains(TEST_FONT_NAME))
            {
                //font = Resources.Load<Font>(textFont);
                font = Resources.GetBuiltinResource<Font>(textFont);
            }
            else
            {
                
                font = Resources.GetBuiltinResource<Font>(textFont);
            }
            return font;
        }

        private static void UpdateLayerName(Layer child, string newName)
        {
            child.Name = newName;
        }
    }

    public class UINode
    {
        private GameObject _go;

        public Rect rect;
        public List<UINode> children;
        public Vector2 pivot;

        public GameObject Go
        {
            get
            {
                return _go;
            }
            set
            {    
                _go = value;
            }
        }

        public UINode()
        {
            pivot = new Vector2(0.5f,0.5f);
            children = new List<UINode>();

        }
    }

}
