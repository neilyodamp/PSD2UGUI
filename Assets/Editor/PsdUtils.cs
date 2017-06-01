using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using PhotoshopFile;
namespace PsdLayoutTool
{
    public class PsdUtils
    {
        public static string GetFullProjectPath()
        {
            string projectDirectory = Application.dataPath;

            if (projectDirectory.EndsWith("Assets"))
            {
                projectDirectory = projectDirectory.Remove(projectDirectory.Length - "Assets".Length);
            }

            return projectDirectory;
        }

        public static bool IsGroupLayer(Layer layer)
        {
            if (layer.Children.Count > 0 || layer.Rect.width == 0)
            {
                return true;
            }
            return false;
        }

        #region Test Tools
        public static void DisplayUINodeTree(UINode root)
        {
            foreach(UINode node in root.children)
            {
                DisplayUINodeTree(node);
            }
        }
        #endregion

        public static void CreateUIHierarchy(UINode root)
        {
            for(int childIndex = 0;childIndex < root.children.Count;childIndex++)
            {
                root.children[childIndex].Go.transform.SetParent(root.Go.transform);
                CreateUIHierarchy(root.children[childIndex]);
            }
        }
        //设定一下位置和尺寸
        public static Rect GetUINodeRectTransform(UINode node)
        {
            if(node.rect.width != 0 || node.children.Count == 0)
            {
                return node.rect;
            }

            float minX = PsdImporter.ScreenResolution.x;
            float minY = PsdImporter.ScreenResolution.y;
            float maxX = 0;
            float maxY = 0;

            
            foreach(var childNode in node.children)
            {
                Rect childRect = GetUINodeRectTransform(childNode);

                minX = Mathf.Min(minX, childRect.x);
                maxX = Mathf.Max(maxX, childRect.x+childRect.width);
                minY = Mathf.Min(minY, childRect.y);
                maxY = Mathf.Max(maxY, childRect.y+childRect.height);
            }

            node.rect.x = minX;
            node.rect.y = minY;
            node.rect.width = maxX - minX;
            node.rect.height = maxY - minY;

            return node.rect;
        }

        public static void UpdateAllUINodeRectTransform(UINode root)
        {

            if(PsdImporter.CanvasObj == null)
            {
                Debug.LogError("No Canvas");
                return;
            }

            RectTransform canvasRectTransform = PsdImporter.CanvasObj.transform as RectTransform;
            Vector3 canvasWorldPosition = canvasRectTransform.position;
            Rect rect = GetUINodeRectTransform(root);
            Vector3 nodeWorldPosition = RectToPostion(rect, PsdImporter.ScreenResolution.x, PsdImporter.ScreenResolution.y);                          

            //宽 和 高
            float width = rect.width;
            float height = rect.height;

            Vector3 canvasPosition = PsdImporter.GetCanvasPosition();

            RectTransform rectTransform = root.Go.transform as RectTransform;

            if (rectTransform.anchorMax.x == rectTransform.anchorMin.x && 
                rectTransform.anchorMax.y == rectTransform.anchorMin.y)
            {
                rectTransform.sizeDelta = new Vector2(width, height);
            }

            //root.Go.transform.position = new Vector3(x + (rect.width) - (0.5f - root.pivot.x) * rect.width
            //        , y - (0.5f - root.pivot.y) * rect.height, 0);

            root.Go.transform.position = new Vector3(nodeWorldPosition.x + canvasWorldPosition.x - (0.5f - root.pivot.x) * rect.width,
                nodeWorldPosition.y+canvasWorldPosition.y - (0.5f - root.pivot.y) * rect.height, 0);

            foreach (var node in root.children)
            {
                UpdateAllUINodeRectTransform(node);
            }

        }

        public static Vector3 RectToPostion(Rect rect,float width,float height)
        {
            float y = height / 2 - rect.y - rect.height/2;
            float x = rect.x - (width / 2) + rect.width/2;

            return new Vector3(x, y, 0);
        }

        public static string TrimSliceReg(string layerName)
        {
            if(layerName.Contains("@"))
            {
                int length = layerName.Length - 1;
                if(layerName.LastIndexOf("@") != -1)
                    length = layerName.LastIndexOf("@");
                layerName = layerName.Substring(0, length);
                return layerName;
            }
            return layerName;
        }

    }
}
