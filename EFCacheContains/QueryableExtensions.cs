
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace berkeleychurchill.CacheContains
{
    public static class QueryableExtensions
    {

        public static IQueryable<T> CacheContains<T>(this IQueryable<T> q, int maxSize = 5)
        {
            return new CacheContainsWrapper<T>(q, maxSize);
        }
        
        internal class CacheContainsWrapper<T> : IQueryable<T>, IQueryProvider, IOrderedQueryable<T>
        {
            IQueryable<T> parent;
            static CacheContainsVisitor visitor;
            private readonly int elementsToCache_;

            public CacheContainsWrapper(IQueryable<T> queryable, int elementsToCache)
            {
                if (elementsToCache < 0)
                    throw new ArgumentOutOfRangeException("elementsToCache", "The value of 'elementsToCache' must be non-negative.");
                elementsToCache_ = elementsToCache;
                visitor = new CacheContainsVisitor(elementsToCache);
                parent = queryable;
            }

            public IQueryable CreateQuery(Expression expression)
            {
                return parent.Provider.CreateQuery(expression);
            }

            public object Execute(Expression expression)
            {
                return parent.Provider.Execute(expression);
            }

            public IQueryable<U> CreateQuery<U>(Expression e)
            {
                var transformed = visitor.Visit(e);
                return new CacheContainsWrapper<U>(parent.Provider.CreateQuery<U>(transformed),
                                                    elementsToCache_);
            }

            public U Execute<U>(Expression e)
            {
                return parent.Provider.Execute<U>(e);
            }

            IEnumerator<T> IEnumerable<T>.GetEnumerator()
            {
                return parent.GetEnumerator();
            }

            public Type ElementType
            {
                get { return parent.ElementType;  }
            }
            public Expression Expression
            {
                get { return parent.Expression; }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return parent.GetEnumerator();
            }

            public IQueryProvider Provider
            {
                get { return this; }
            }
        }
    }
}
