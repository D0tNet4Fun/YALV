using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace YALV.Common
{
    public class FileComparer : Comparer<string>
    {
        private readonly CultureInfo _culture;

        public FileComparer(CultureInfo culture)
        {
            _culture = culture;
        }

        public override int Compare(string x, string y)
        {
            if (_culture.CompareInfo.Compare(x, y) == 0)
            {
                return 0;
            }

            try
            {
                var xDirectory = Path.GetDirectoryName(x);
                var yDirectory = Path.GetDirectoryName(y);

                var isSameDirectory = _culture.CompareInfo.Compare(xDirectory, yDirectory) == 0;
                if (isSameDirectory)
                {
                    var isSameFileName = _culture.CompareInfo.Compare(Path.GetFileNameWithoutExtension(x), Path.GetFileNameWithoutExtension(y)) == 0;
                    if (isSameFileName)
                    {
                        var xExtension = GetExtension(x);
                        var yExtension = GetExtension(y);
                        int xEntensionAsInt, yExtensionAsInt;
                        if (int.TryParse(xExtension, out xEntensionAsInt) && int.TryParse(yExtension, out yExtensionAsInt))
                        {
                            return xEntensionAsInt.CompareTo(yExtensionAsInt);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print("Error comparing {0} and {1}, will use default comparer: {2}", x, y, ex);
            }
            return _culture.CompareInfo.Compare(x, y);
        }

        private static string GetExtension(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            if (!string.IsNullOrEmpty(filePath))
            {
                extension = extension.Replace(".", string.Empty);
            }
            return extension;
        }
    }
}