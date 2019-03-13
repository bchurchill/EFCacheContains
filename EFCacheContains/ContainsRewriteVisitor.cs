using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace berkeleychurchill.CacheContains
{
    
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
            if (target.Type.GetGenericArguments().Length == 0)
            {
                // I don't really understand this case -- improvements possible?
#if DEBUG
                Console.WriteLine("[CacheContains - Rewrite] GetGenericArguments().Length == 0");
#endif
                return null;
            }

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
            else if (target.NodeType == ExpressionType.MemberAccess)
            {
                MemberExpression expr = (MemberExpression)(target);
                if (expr.Expression.NodeType == ExpressionType.Constant)
                {
                    var valueForInvocation = ((ConstantExpression)expr.Expression).Value;
                    if (expr.Member.MemberType == MemberTypes.Property)
                    {
                        var propertyInfo = (PropertyInfo)expr.Member;
                        enumerable = propertyInfo.GetValue(valueForInvocation);
                    }
                    else if (expr.Member.MemberType == MemberTypes.Field)
                    {
                        var fieldInfo = (FieldInfo)expr.Member;
                        enumerable = fieldInfo.GetValue(valueForInvocation);
                    }
                }
            }

            if (enumerable == null)
            {
                LambdaExpression targetLambda = Expression.Lambda(target, new ParameterExpression[] { });
                var targetThunk = targetLambda.Compile();
                enumerable = targetThunk.DynamicInvoke();
#if DEBUG
                Console.WriteLine($"[CacheContains - Rewrite] Dynamically invoking lambda.  target.NodeType = {target.NodeType.ToString()} target={target}");
                CacheContainsStatistics.DynamicLambdaCount++;
#endif
            }

            int count = (int)ourCount.Invoke(null, new object[] { enumerable });

            if (count == 0)
            {
                return Expression.Constant(false);
            }
            else if (count == 1 && 1 <= elementsToCache)
            {
                var value = ourElementAt.Invoke(null, new object[] { enumerable, 0 });
                return Expression.MakeBinary(ExpressionType.Equal, argument, Wrap(value));
            }
            else if (count <= elementsToCache)
            {
                var firstValue = ourElementAt.Invoke(null, new object[] { enumerable, 0 });
                var firstExpr = Expression.MakeBinary(ExpressionType.Equal, argument, Wrap(firstValue));
                var output = firstExpr;

                for (int i = 1; i < count; ++i)
                {
                    var nextValue = ourElementAt.Invoke(null, new object[] { enumerable, i });
                    var nextExpr = Expression.MakeBinary(ExpressionType.Equal, argument, Wrap(nextValue));
                    output = Expression.MakeBinary(ExpressionType.Or, output, nextExpr);
                }

                return output;
            }
            else
            {
                return null;
            }

        }


        /** intercept calls to Contains() and rewrite them */
        protected override Expression VisitMethodCall(MethodCallExpression e)
        {
            var method = e.Method;
            var arguments = e.Arguments;
            var target = e.Object;
            Expression rewrite = null;
            if (method.Name == "Contains" && arguments.Count() == 1 && target != null)
            {
                var visitedTarget = Visit(target);
                var visitedArguments = Visit(arguments);
                rewrite = Rewrite(visitedTarget, method, visitedArguments[0]);
#if DEBUG
                Console.WriteLine("[CacheContains - VisitMethodCall] Found Contains() call");
                CacheContainsStatistics.ContainsCount++;
#endif
            }
            else if (method.Name == "Contains" && arguments.Count() == 2 && target == null)
            {
                var visitedArguments = Visit(arguments);
                rewrite = Rewrite(visitedArguments[0], method, visitedArguments[1]);
#if DEBUG
                Console.WriteLine("[CacheContains - VisitMethodCall] Found Contains() extension method");
                CacheContainsStatistics.ContainsCount++;
#endif
            }
#if DEBUG
            else if (method.Name == "Contains")
            {
                Console.WriteLine("[CacheContains - VisitMethodCall] 'Contains' method found, but wrong number of arguments.");
                CacheContainsStatistics.ContainsCount++;
            }
#endif

            if (rewrite != null)
            {
#if DEBUG
                CacheContainsStatistics.RewriteCount++;
                Console.WriteLine("[CacheContains - VisitMethodCall] Rewrite succeeded.");
#endif
                return rewrite;
            }
            else
            {
                return base.VisitMethodCall(e);
            }
        }
    }
}
