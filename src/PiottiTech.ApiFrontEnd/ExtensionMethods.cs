using System.Collections.Generic;

namespace PiottiTech.ApiFrontEnd
{
    internal static class ExtensionMethods
    {
        internal static IEnumerable<string> ToCleanWhiteList(this string whitelist)
        {
            return whitelist.ToLower().Replace(" ", "").Replace("\r", "").Replace("\n", "").Replace("\t", "").TrimEnd(',').Split(',');
        }
    }
}