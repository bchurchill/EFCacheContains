
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace berkeleychurchill.CacheContains
{
    public static class QueryableExtensions
    {
        public static IQueryable<T> CacheContains<T>(this IQueryable<T> q)
        {
            return new CacheContainsWrapper<T>(q);
        }

        public class DummyWrapper<T>
        {
            public T field;

            public static FieldInfo fieldInfo = typeof(DummyWrapper<T>).GetField("field", BindingFlags.Instance | BindingFlags.Public);

            public DummyWrapper(T x)
            {
                field = x;
            }
        }

        internal class CacheContainsVisitor : ExpressionVisitor
        {
            MethodInfo elementAtMethod;
            MethodInfo countMethod;
            private readonly int elementsToCache;

            internal CacheContainsVisitor(int toCache)
            {
                elementsToCache = toCache;

                elementAtMethod = typeof(Enumerable)
                        .GetMethods(BindingFlags.Static | BindingFlags.Public)
                        .Single(mi => mi.Name == "ElementAt"
                         && mi.IsGenericMethodDefinition
                         && mi.GetParameters().Length == 2);



                countMethod = typeof(Enumerable)
                        .GetMethods(BindingFlags.Static | BindingFlags.Public)
                        .Single(mi => mi.Name == "Count"
                         && mi.IsGenericMethodDefinition
                         && mi.GetParameters().Length == 1);

            }

            /** Take an object and wrap it so that EF parameterizes it. */
            private Expression Wrap(object obj)
            {
                var genericType = obj.GetType();
                var dummyWrapperType = typeof(DummyWrapper<>).MakeGenericType(new Type[] { genericType });
                var fieldInfo = dummyWrapperType.GetField("field", BindingFlags.Public | BindingFlags.Instance);
                var constructor = dummyWrapperType.GetConstructor(new Type[] { genericType });
                var dummyWrapper = constructor.Invoke(new object[] { obj });
                return Expression.Field(Expression.Constant(dummyWrapper), fieldInfo);
            }

            /** Rewrite an invocation of Contains() as something nice. */
            private Expression Rewrite(Expression target, MethodInfo method, Expression argument)
            {
                /** Specialize methods for the type at hand */
                if (target.Type.GetGenericArguments().Length == 0)  // I don't really understand this case -- improvements possible?
                    return Expression.Call(target, method, argument);
                
                Type genericArgument = target.Type.GetGenericArguments()[0];
                MethodInfo ourCount = countMethod.MakeGenericMethod(genericArgument);
                MethodInfo ourElementAt = elementAtMethod.MakeGenericMethod(genericArgument);


                /** Get the IEnumerable object */
                object enumerable = null;
                if (target.NodeType == ExpressionType.Constant)
                {
                    ConstantExpression expr = (ConstantExpression)(target);
                    enumerable = expr.Value;
                }
                else
                {
                    LambdaExpression targetLambda = Expression.Lambda(target, new ParameterExpression[] { });
                    var targetThunk = targetLambda.Compile();
                    enumerable = targetThunk.DynamicInvoke();
                }

                int count = (int)ourCount.Invoke(null, new object[] { enumerable });

                if(count == 0)
                {
                    return Expression.Constant(false);
                } else if(count == 1)
                {
                    var value = ourElementAt.Invoke(null, new object[] { enumerable, 0 });
                    return Expression.MakeBinary(ExpressionType.Equal, argument, Wrap(value));
                } else if(count < elementsToCache)
                {
                    var firstValue = ourElementAt.Invoke(null, new object[] { enumerable, 0 });
                    var firstExpr = Expression.MakeBinary(ExpressionType.Equal, argument, Wrap(firstValue));
                    var output = firstExpr;

                    for(int i = 1; i < count; ++i)
                    {
                        var nextValue = ourElementAt.Invoke(null, new object[] { enumerable, i });
                        var nextExpr = Expression.MakeBinary(ExpressionType.Equal, argument, Wrap(nextValue));
                        output = Expression.MakeBinary(ExpressionType.Or, firstExpr, nextExpr);
                    }

                    return output;
                } else
                {
                    return Expression.Call(target, method, argument);
                }

                                                                                   // target.Contains(argument)
            }

            
            /** intercept calls to Contains() and rewrite them */
            protected override Expression VisitMethodCall(MethodCallExpression e)
            {
                var method = e.Method;
                var arguments = e.Arguments;
                var target = e.Object;
                if (method.Name == "Contains" && arguments.Count() == 1 && target != null)
                {
                    var visitedTarget = Visit(target);
                    var visitedArguments = Visit(arguments);
                    return Rewrite(visitedTarget, method, visitedArguments[0]); 
                }
                else
                {
                    return base.VisitMethodCall(e);
                }
            }
        }

        internal class CacheContainsWrapper<T> : IQueryable<T>, IQueryProvider, IOrderedQueryable<T>
        {
            IQueryable<T> parent;
            static CacheContainsVisitor visitor;

            public CacheContainsWrapper(IQueryable<T> queryable, int elementsToCache = 5)
            {
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
                return new CacheContainsWrapper<U>(parent.Provider.CreateQuery<U>(transformed));
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
