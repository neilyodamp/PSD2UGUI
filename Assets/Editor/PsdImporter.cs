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
        public const string BTN_HEAD = "btn_";
        public const string BTN_TAIL_HIGH = "_highlight";
        public const string BTN_TAIL_DIS = "_disable";
        public const string PUBLIC_IMG_HEAD = "public_";

        public const string PSD_TAIL = ".psd";
        public const string IMG_TAIL = ".png";
        public const string NO_NAME_HEAD = "no_name_";
        public const string CURR_IMG_PATH_ROOT = "export_image/";

        private const string TEST_FONT_NAME = "";
        private const string PUBLIC_IMG_PATH = @"\public_images";
        private const string SCROLL = "@ScrollView";
        private const string SCROLL_SIZE = "@Size";

        private static string _currentPath;

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
        private static bool _fullScreenUI = true;
        private static Dictionary<GameObject, Vector3> _positionDic;
        private static Dictionary<string, Sprite> _imageDic;

        private static UINode _rootNode;
        private static UINode _currNode;

        public static Vector3 GetCanvasPosition()
        {
            return _canvasObj.transform.position;
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

        public static bool fullScreenUI
        {
            get { return _fullScreenUI; }
            set { _fullScreenUI = value; }
        }

        static PsdImporter()
        {
            _textFont = TEST_FONT_NAME;//.otf";//yanru测试字体

            MaximumDepth = 1;
            PixelsToUnits = 100;
        }

        public static void ExportLayersAsTextures(string assetPath)
        {

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

        private static void Import(string asset)
        {
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
            _currentPath = Path.Combine(_currentPath, _psdName);

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
                _rootNode.go = _rootPsdGameObject;
                
                RectTransform rectRoot = _rootPsdGameObject.GetComponent<RectTransform>();
                rectRoot.anchorMin = new Vector2(0, 0);
                rectRoot.anchorMax = new Vector2(1, 1);
                rectRoot.offsetMin = Vector2.zero;
                rectRoot.offsetMax = Vector2.zero;

                Vector3 rootPos = Vector3.zero;
                _rootPsdGameObject.transform.position = Vector3.zero;
                //updateRectPosition(rootPsdGameObject, rootPos, true);

                _currentGroupGameObject = _rootPsdGameObject;
                _currNode = _rootNode;
            }

            List<Layer> tree = BuildLayerTree(psd.Layers);

            ExportTree(tree);
            PsdUtils.CreateUIHierarchy(_rootNode);
            PsdUtils.UpdateAllUINodeRectTransform(_rootNode);
            if (_createPrefab)
            {
                UnityEngine.Object prefab = PrefabUtility.CreateEmptyPrefab(asset.Replace(".psd", ".prefab"));
                PrefabUtility.ReplacePrefab(_rootPsdGameObject, prefab);
            }

            //step1:刷新按钮SpriteState，删除按钮状态Image
            //UpdateBtnsSpriteState();

            //step2:最后删除多余的图片aaa(1),aaa(1)_highlight这种。并调整引用
            DeleteExtraImages();

            //step3:矫正 scale问题 
            Transform[] allChilds = _rootPsdGameObject.GetComponentsInChildren<Transform>();
            ResetRectSize(allChilds);

            //step4: 刷新九宫格图片
            ResetSlicedImage(allChilds);

            //step4:最后矫正 UI根节点坐标
            ResetRootRect();

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

        private static void ResetSlicedImage(Transform[] allChilds)
        {
            Regex reg = Layer.SLICE_REG;
            for (int index = 0; index < allChilds.Length; index++)
            {
                Transform tran = allChilds[index];
                if (_useRealImageSize == false)
                {
                    Image image = tran.GetComponent<Image>();
                    if (image != null && image.sprite != null)
                    {
                        string spriteName = image.sprite.name;

                        string str1 = spriteName;

                        Match match = reg.Match(str1);
                        Vector2 layoutSize = Vector2.zero;
                        if (match.ToString() != "")
                        {
                            string[] size = reg.Match(str1).ToString().Split(Layer.SLICE_SEPECTOR);
                            layoutSize.x = Convert.ToInt32(size[0]);
                            layoutSize.y = Convert.ToInt32(size[1]);
                        }
                        if (layoutSize.x != 0 && layoutSize.y != 0)
                        {
                            //image.GetComponent<RectTransform>().sizeDelta = layoutSize;
                            image.type = Image.Type.Sliced;

                            if (image.sprite.border == Vector4.zero)
                            {
                                Debug.LogError(Time.time + "need to set png=" + image.sprite.name + ",slice border");
                            }
                        }
                    }
                }
            }
        }

        private static void ResetRectSize(Transform[] allChilds)
        {
            for (int index = 0; index < allChilds.Length; index++)
            {
                RectTransform rect = allChilds[index].GetComponent<RectTransform>();
                allChilds[index].transform.localScale = Vector3.one;
                if (rect == null)
                    continue;

                Vector2 curSize = rect.sizeDelta;
                curSize.x *= PixelsToUnits;// rect.transform.localScale.x;
                curSize.y *= PixelsToUnits;// rect.transform.localScale.y;

                rect.sizeDelta = curSize;
            }
        }

        private static void DeleteExtraImages()
        {
            List<string> imgaePathList = new List<string>(_imageDic.Keys);
            for (int index = 0; index < imgaePathList.Count; index++)
            {
                string pathtemp = imgaePathList[index];
                if (SpriteNameExtra(pathtemp) != "")
                {
                    File.Delete(pathtemp);
                }
            }
        }

        private static void UpdateBtnsSpriteState()
        {
            Dictionary<Transform, bool> _dealDic = new Dictionary<Transform, bool>(); //flag if item will be deleted

            List<Transform> btnList = new List<Transform>();
            List<Transform> deleteList = new List<Transform>();

            Transform[] allChild = _rootPsdGameObject.GetComponentsInChildren<Transform>();

            List<Sprite> canUseSpriteList = new List<Sprite>(); //最终可用的Sprite列表

            for (int index = 0; index < allChild.Length; index++)
            {
                Transform tran = allChild[index];
                if (tran.name.IndexOf(BTN_HEAD) == 0) //is a button
                {
                    Button button = tran.gameObject.AddComponent<Button>();
                    tran.GetComponent<Image>().raycastTarget = true;
                    button.transition = Selectable.Transition.SpriteSwap;
                    btnList.Add(tran);
                }
            }

            for (int btnIndex = 0; btnIndex < btnList.Count; btnIndex++)
            {
                string btnName = btnList[btnIndex].name;

                for (int index = 0; index < allChild.Length; index++)
                {
                    Transform tran = allChild[index];

                    if (allChild[index].name.IndexOf(btnName) == 0)
                    {
                        if (allChild[index].name.Contains(BTN_TAIL_HIGH))//button highlight image
                        {
                            SpriteState sprite = btnList[btnIndex].GetComponent<Button>().spriteState;
                            sprite.pressedSprite = allChild[index].GetComponent<Image>().sprite;
                            btnList[btnIndex].GetComponent<Button>().spriteState = sprite;
                            deleteList.Add(tran);
                            CheckAddSprite(ref canUseSpriteList, sprite.pressedSprite);
                        }
                        else if (allChild[index].name.Contains(BTN_TAIL_DIS))//button disable image 
                        {
                            SpriteState sprite = btnList[btnIndex].GetComponent<Button>().spriteState;
                            sprite.disabledSprite = allChild[index].GetComponent<Image>().sprite;
                            btnList[btnIndex].GetComponent<Button>().spriteState = sprite;
                            deleteList.Add(tran);
                            CheckAddSprite(ref canUseSpriteList, sprite.disabledSprite);
                        }
                        else if (allChild[index].GetComponent<Image>() != null)
                        {
                            CheckAddSprite(ref canUseSpriteList, allChild[index].GetComponent<Image>().sprite);
                        }
                    }
                }
            }

            //delete no use items
            for (int index = 0; index < deleteList.Count; index++)
            {
                DestroyItem(deleteList[index]);
            }

            for (int index = 0; index < allChild.Length; index++)
            {
                if (allChild[index] == null)
                    continue;
                Button button = allChild[index].GetComponent<Button>();
                if (button == null)
                    continue;

                SpriteState sprite = button.spriteState;
                if (sprite.pressedSprite != null)
                {
                    sprite.pressedSprite = RescriteBtnSprite(canUseSpriteList, sprite.pressedSprite);
                }
                if (sprite.disabledSprite != null)
                {
                    sprite.disabledSprite = RescriteBtnSprite(canUseSpriteList, sprite.disabledSprite);
                }
                Sprite normalSprite = button.GetComponent<Image>().sprite;
                normalSprite = RescriteBtnSprite(canUseSpriteList, normalSprite);
                button.GetComponent<Image>().sprite = normalSprite;

                button.spriteState = sprite;
            }
        }

        private static Sprite RescriteBtnSprite(List<Sprite> canUseSpriteList, Sprite sprite)
        {
            return null;
        }

        private static void CheckAddSprite(ref List<Sprite> list, Sprite sprite)
        {
            if (SpriteNameExtra(sprite.name) == "")
            {
                list.Add(sprite);
            }
        }

        private static string SpriteNameExtra(string itemName)
        {
            Regex reg = new Regex(@"[(]+\d+[)]");
            string tempLayerName = itemName;
            tempLayerName = tempLayerName.TrimEnd(BTN_TAIL_DIS.ToCharArray());
            tempLayerName = tempLayerName.TrimEnd(BTN_TAIL_HIGH.ToCharArray());
            Match match = reg.Match(tempLayerName);
            if (match.ToString() != "")
            {
                string res = tempLayerName.Replace(match.ToString(), "");
                return match.ToString();
            }
            return "";
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

            // if there are any dangling layers, add them to the tree
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

        private static void ExportTree(List<Layer> tree)
        {
            for (int i = tree.Count - 1; i >= 0; i--)
            {
                ExportTreeNode(tree[i]);
            }
        }
        private static void ExportTreeNode(Layer layer)
        {
            UpdateLayerName(layer, MakeNameSafe(layer.Name));
            if (PsdUtils.IsGroupLayer(layer))
            {
                ExportGroup(layer);
            }
            else
            {
                ExportLayer(layer);
            }
        }
        //图像和text
        private static void ExportLayer(Layer layer) // 
        {
            if(!layer.IsTextLayer)
            {

            }
            else
            {
                UINode node = PsdControl.CreateUIText(layer);
                _currNode.children.Add(node);
            }
        }
        private static void ExportGroup(Layer layer)
        {
            GroupClass groupClass = PsdControl.CheckGroupClass(layer);
            UINode oldNode = _currNode;
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
                node = PsdControl.CreateTexture(layer);
            }
            else if(groupClass == GroupClass.Button)
            {
                node = PsdControl.CreateUIButton(layer);
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
                _currNode.children.Add(node);
                _currNode = node;
                ExportTree(layer.Children);
                _currNode = oldNode;
            }
        }





        private static void CreateDic(string path)
        {
            Directory.CreateDirectory(path);
        }

        private static bool ContainsIgnoreCase(this string source, string toCheck)
        {
            return source.IndexOf(toCheck, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ReplaceIgnoreCase(this string str, string oldValue, string newValue)
        {
            return null;
        }
        private static void ExportArtLayer(Layer layer)
        {
            if (!layer.IsTextLayer)
            {
                if (_layoutInScene || _createPrefab)
                {
                    CreateUIImage(layer);
                }
                else
                {
                    CreatePNG(layer);
                }
            }
            else
            {
                if (_layoutInScene || _createPrefab)
                {
                    //CreateUIText(layer);
                }
            }
        }
        private static string CreatePNG(Layer layer)
        {
            string file = string.Empty;

            if (layer.Children.Count == 0 && layer.Rect.width > 0)
            {
                //// decode the layer into a texture
                //Texture2D texture = ImageDecoder.DecodeImage(layer);
                //string writePath;
                //file = GetFilePath(layer, out writePath);
                //if(!Directory.Exists(writePath))
                //{
                //    Directory.CreateDirectory(writePath);
                //}
                Texture2D texture = DoCreateTexture(layer, out file);
                Vector2 size = layer.Rect.size;
                if (size.x >= LargeImageAlarm.x || size.y >= LargeImageAlarm.y)
                {
                    Debug.Log(Time.time + "图片=" + file + ",尺寸=" + size + "较大！考虑单拆图集！");
                }
                File.WriteAllBytes(file, texture.EncodeToPNG());
            }

            return file;
        }

        private static Texture2D DoCreateTexture(Layer layer ,out string file)
        {
            Texture2D texture = ImageDecoder.DecodeImage(layer);
            string writePath;
            file = GetFilePath(layer, out writePath);
            if(!Directory.Exists(writePath))
            {
                Directory.CreateDirectory(writePath);
            }
            return texture;
        }

        private static string GetFilePath(Layer layer, out string writePath)
        {
            string file = string.Empty;
            writePath = _currentPath;
            string layerName = TrimSpecialHead(layer.Name);

            if(layerName.Contains(PUBLIC_IMG_HEAD))
            {
                int length = writePath.Length - 1;
                if(writePath.LastIndexOf(@"/") != -1)
                    length = writePath.LastIndexOf(@"/");

                writePath = writePath.Substring(0, length);
                writePath += PUBLIC_IMG_PATH;
            }

            file = Path.Combine(writePath, layerName + ".png");
            return file;
        }

        private static string TrimSpecialHead(string str)
        {
            if (str.IndexOf(BTN_HEAD) == 0)
                return str.Replace(BTN_HEAD, "");

            return str;
        }

        public static Sprite CreateSprite(Layer layer)
        {
            return CreateSprite(layer, _psdName);
        }

        public static Sprite CreateSprite(Layer layer, string packingTag)
        {
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
                texture = DoCreateTexture(layer, out file);
                File.WriteAllBytes(file, texture.EncodeToPNG());
                string relativePathToSprite = GetRelativePath(file);
                AssetDatabase.ImportAsset(relativePathToSprite, ImportAssetOptions.ForceUpdate);
                TextureImporter textureImporter = AssetImporter.GetAtPath(relativePathToSprite) as TextureImporter;
                if(textureImporter != null)
                {
                    textureImporter.textureType = TextureImporterType.GUI;
                    textureImporter.mipmapEnabled = false;
                    textureImporter.spriteImportMode = SpriteImportMode.Single;
                    textureImporter.spritePivot = new Vector2(0.5f, 0.5f);
                    textureImporter.maxTextureSize = 2048;
                    textureImporter.spritePixelsPerUnit = PixelsToUnits;
                    //textureImporter.spritePackingTag = packingTag;
                }
                AssetDatabase.ImportAsset(relativePathToSprite, ImportAssetOptions.ForceUpdate);
            }
            return texture;

        }

        private static Sprite ImportSprite(string relativePathToSprite, string packingTag, Layer layer)
        {
            AssetDatabase.ImportAsset(relativePathToSprite, ImportAssetOptions.ForceUpdate);

            TextureImporter textureImporter = AssetImporter.GetAtPath(relativePathToSprite) as TextureImporter;
            if (textureImporter != null)
            {
                textureImporter.textureType = TextureImporterType.Sprite;
                textureImporter.mipmapEnabled = false;
                textureImporter.spriteImportMode = SpriteImportMode.Single;
                textureImporter.spritePivot = new Vector2(0.5f, 0.5f);
                textureImporter.maxTextureSize = 2048;
                textureImporter.spritePixelsPerUnit = PixelsToUnits;
                textureImporter.spritePackingTag = packingTag;
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
            UpdateRectSize(ref transform, _canvasSize.x / PixelsToUnits, _canvasSize.y / PixelsToUnits);

            scaler.dynamicPixelsPerUnit = PixelsToUnits;
            scaler.referencePixelsPerUnit = PixelsToUnits;

            GraphicRaycaster racaster = _canvasObj.GetComponent<GraphicRaycaster>();
            if (racaster == null)
                racaster = _canvasObj.AddComponent<GraphicRaycaster>();

        }

        private static Image CreateUIImage(Layer layer)
        {


            float x = layer.Rect.x;
            float y = layer.Rect.y;

            y = (_canvasSize.y) - y;

            x = x - ((_canvasSize.x / 2));
            y = y - ((_canvasSize.y / 2));

            float width = layer.Rect.width / PixelsToUnits;
            float height = layer.Rect.height / PixelsToUnits;

            GameObject gameObject = CreateObj(layer.Name);

            gameObject.transform.position = new Vector3(x + (layer.Rect.width / 2), y - (layer.Rect.height / 2), _currentDepth);

            gameObject.transform.SetParent(_currentGroupGameObject.transform, false); //.transform);

            gameObject.transform.position = new Vector3(gameObject.transform.position.x + _currentGroupGameObject.transform.position.x, gameObject.transform.position.y + _currentGroupGameObject.transform.position.y, gameObject.transform.position.z);

            _currentDepth -= _depthStep;

            Image image = gameObject.AddComponent<Image>();
            image.sprite = CreateSprite(layer);
            image.raycastTarget = false; //can not click Image by yanru 2016-06-16 19:26:55

            // 对于Image，如果当前图层指定了透明度，刷新透明度
            if (layer.ImageTransparent <= 1f)
            {
                Color imageColor = image.color;
                imageColor.a = layer.ImageTransparent;
                image.color = imageColor;
            }

            RectTransform transform = gameObject.GetComponent<RectTransform>();
            UpdateRectSize(ref transform, width, height);

            return image;
        }

        private static void UpdateRectSize(ref RectTransform transform, float width, float height)
        {
            transform.sizeDelta = new Vector2(width, height);
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

        private static void CreateUIButton(Layer layer)
        {
            Image image = CreateUIImage(layer);
        }

        private static void UpdateLayerName(Layer child, string newName)
        {
            child.Name = newName;
        }
    }

    public class UINode
    {
        public GameObject go;
        public Rect rect;
        public List<UINode> children;

        public UINode()
        {
            children = new List<UINode>();
        }
    }

}
