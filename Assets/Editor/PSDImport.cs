using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using UnityEngine;
using PhotoshopFile;
namespace PsdLayoutTool
{
    public static class PSDImport
    {
       // private Vector2 _canvasSize;

        public static int pixelsToUnits;
        static PSDImport()
        {
            pixelsToUnits = 100;
        }

        private static void Import(string asset)
        {
            string fullPath = Path.Combine(PsdUtils.GetFullProjectPath(), asset.Replace('\\', '/'));
            PsdFile psd = new PsdFile(fullPath);

        }
    }
}
