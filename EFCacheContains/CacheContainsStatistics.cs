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
    }
}
