using System;
using UnityEditor;
using UnityEngine;

namespace PsdLayoutTool
{

    [CustomEditor(typeof(TextureImporter))]
    public class PsdInspector : Editor
    {
        private Editor _nativeEditor;

        private GUIStyle _guiStyle;

        public void OnEnable()
        {
            Type type = Type.GetType("UnityEditor.TextureImporterInspector, UnityEditor");

            if (target == null)
            {
                Debug.Log(Time.time + "target is null");
            }
            if (type == null)
            {
                Debug.Log(Time.time + "type is null");
            }

            _nativeEditor = CreateEditor(target, type);

            _guiStyle = new GUIStyle();
            _guiStyle.richText = true;
            _guiStyle.fontSize = 14;
            _guiStyle.normal.textColor = Color.black;

            if (Application.HasProLicense())
            {
                _guiStyle.normal.textColor = Color.white;
            }

            /*
            TextureImporter import = (TextureImporter)target;
            if (import.textureType != TextureImporterType.Sprite)
            {
                import.textureType = TextureImporterType.Sprite;
                import.mipmapEnabled = false;
                import.filterMode = FilterMode.Bilinear;
                import.SaveAndReimport();
                //import.textureFormat = TextureImporterFormat.AutomaticCompressed;
                import.textureCompression = TextureImporterCompression.Compressed;
            }
            */
        }
        public override void OnInspectorGUI()
        {
            if (_nativeEditor != null)
            {
                // check if it is a PSD file selected
                string assetPath = ((TextureImporter)target).assetPath;

                if (assetPath.EndsWith(PsdImporter.PSD_TAIL))
                {
                    GUILayout.Label("<b>PSD Layout Tool</b>", _guiStyle, GUILayout.Height(23));

                    //set ui width and height;       
                    GUIContent screenSize = new GUIContent("屏幕分辨率", "UI 宽*高");
                    PsdImporter.ScreenResolution = EditorGUILayout.Vector2Field(screenSize, PsdImporter.ScreenResolution);

                    GUIContent imageSizeLimit = new GUIContent("小图限制(超过该尺寸 建议拆分图集)", "小图尺寸限制(超过该尺寸 建议拆出图集)");
                    PsdImporter.LargeImageAlarm = EditorGUILayout.Vector2Field(imageSizeLimit, PsdImporter.LargeImageAlarm);

                    //set textFont
                    GUIContent fontName = new GUIContent("字体名称", "字体名称");
                    PsdImporter.textFont = EditorGUILayout.TextField(fontName, PsdImporter.textFont);

                    if (GUILayout.Button("Layout in Current Scene"))
                    {
                        PsdImporter.LayoutInCurrentScene(assetPath);
                    }

                    if (GUILayout.Button("Generate Prefab"))
                    {
                        PsdImporter.GeneratePrefab(assetPath);
                    }


                    GUILayout.Space(3);

                    GUILayout.Box(string.Empty, GUILayout.Height(1), GUILayout.MaxWidth(Screen.width - 30));

                    GUILayout.Space(3);

                    GUILayout.Label("<b>Unity Texture Importer Settings</b>", _guiStyle, GUILayout.Height(23));

                    // draw the default Inspector for the PSD
                    _nativeEditor.OnInspectorGUI();
                }
                else
                {
                    // It is a "normal" Texture, not a PSD
                    _nativeEditor.OnInspectorGUI();
                }
            }
        }
    }
}