using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseMonitor.Api.Utils
{
    public static class UrlHelper
    {
        public static string ToPublicUrl(string baseUrl, string relativePath)
        {
            return $"{baseUrl.TrimEnd('/')}/{relativePath.Replace("\\", "/")}";
        }
    }

}