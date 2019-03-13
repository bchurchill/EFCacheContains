using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace berkeleychurchill.CacheContains
{
    
    internal class ContainsRewriteVisitor : ExpressionVisitor
    {
        MethodInfo elementAtMethod;
        MethodInfo countMethod;
        private readonly int elementsToCache;

        internal ContainsRewriteVisitor(int toCache)
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

        /** This is meant to be a faster way of evaluating an expression than compiling a new lambda
         * every single time.  The reality is that we only need to evaluate really small expresison trees,
         * and most of them are very simple. */
        private object EvaluateExpression(Expression e)
        {
            switch(e.NodeType)
            {
                case ExpressionType.Constant:
                    return ((ConstantExpression)e).Value;

                case ExpressionType.Convert:
                    var target = ((UnaryExpression)e).Operand;
                    return EvaluateExpression(target);

                case ExpressionType.MemberAccess:
                    var memberExpr = (MemberExpression)e;
                    var valueForInvocation = EvaluateExpression(memberExpr.Expression);
                    if (memberExpr.Member.MemberType == MemberTypes.Property)
                    {
                        var propertyInfo = (PropertyInfo)memberExpr.Member;
                        return propertyInfo.GetValue(valueForInvocation);
                    }
                    else if (memberExpr.Member.MemberType == MemberTypes.Field)
                    {
                        var fieldInfo = (FieldInfo)memberExpr.Member;
                        return fieldInfo.GetValue(valueForInvocation);
                    } else
                    {
                        return EvaluateExpressionSlow(e);
                    }

                default:
                    return EvaluateExpressionSlow(e);
            }

        }

        private object EvaluateExpressionSlow(Expression e)
        {
#if DEBUG
            Console.WriteLine($"[CacheContains - Rewrite] Dynamically invoking lambda.  NodeType = {e.NodeType.ToString()} node={e}");
            CacheContainsStatistics.DynamicLambdaCount++;
#endif
            LambdaExpression targetLambda = Expression.Lambda(e, new ParameterExpression[] { });
            var targetThunk = targetLambda.Compile();
            return targetThunk.DynamicInvoke();
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

            /** Get the IEnumerable object and its methods. */
            Type genericArgument = target.Type.GetGenericArguments()[0];
            MethodInfo ourCount = countMethod.MakeGenericMethod(genericArgument);
            MethodInfo ourElementAt = elementAtMethod.MakeGenericMethod(genericArgument);
            object enumerable = EvaluateExpression(target);

            /** Count the number of items. */
            int count = (int)ourCount.Invoke(null, new object[] { enumerable });

            if (count == 0)
            {
                /** In this case, the method call will always return false. */
                return Expression.Constant(false);
            }
            else if (count == 1 && 1 <= elementsToCache)
            {
                /** If there's only one item in the list, we replace the call with an equality check. */
                var value = ourElementAt.Invoke(null, new object[] { enumerable, 0 });
                return Expression.MakeBinary(ExpressionType.Equal, argument, Wrap(value));
            }
            else if (count <= elementsToCache)
            {
                /** If there are multiple itmes in the list, we chain logical-ORs. */
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
