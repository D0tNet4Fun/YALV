﻿using System;

namespace YALV.Core
{
    public static class Utils
    {
        public static bool Contains(this string source, string toCheck, StringComparison comparison)
        {
            return source.IndexOf(toCheck, comparison) >= 0;
        }
    }
}
