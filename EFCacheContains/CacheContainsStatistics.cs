using System;
using System.Collections.Generic;
using System.Text;

namespace berkeleychurchill.CacheContains
{
    public static class CacheContainsStatistics
    {
        public static int ContainsCount { get; internal set; }
        public static int RewriteCount { get; internal set; }
        public static int DynamicLambdaCount { get; internal set; }

        public static void Reset()
        {
            ContainsCount = 0;
            RewriteCount = 0;
            DynamicLambdaCount = 0;
        }
    }
}
