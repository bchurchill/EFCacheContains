using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace berkeleychurchill.CacheContains
{
    internal class DummyWrapper<T>
    {
        public T field;

        public static FieldInfo fieldInfo = 
            typeof(DummyWrapper<T>).GetField("field", BindingFlags.Instance | BindingFlags.Public);

        public DummyWrapper(T x)
        {
            field = x;
        }
    }
}
