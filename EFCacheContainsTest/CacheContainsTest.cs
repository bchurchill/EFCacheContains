using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace berkeleychurchill.CacheContains.Test
{
    public class Tests
    {
        List<int> storeList;

        [SetUp]
        public void Setup()
        {
            storeList = new List<int>() { 1, 2, 3, 4, 5, 6, 7 };
            QueryableExtensions.DefaultMaxSize = 5;
            CacheContainsStatistics.Reset();
        }

        /** TODO: It would be nice to test that this actually causes EF
         * to cache things properly, but I think actually making a test 
         * to do this reliably would be hard, since EF doesn't expose its
         * internals visibly. */

        void InsersectStoreWithListTest<T> (
            T list, 
            int maxSize) 
         where T:IEnumerable<int>
        {
            var queryable = from i in storeList.AsQueryable().CacheContains(maxSize)
                         where list.Contains(i)
                         select i;

            var result = queryable.ToList();
            var expected = storeList.Intersect(list);
            var expectedRewrites = maxSize < list.Count() ? 0 : 1;

            Assert.AreEqual(expected, result);

#if DEBUG
            Assert.AreEqual(1, CacheContainsStatistics.ContainsCount);
            Assert.AreEqual(0, CacheContainsStatistics.DynamicLambdaCount);
            Assert.AreEqual(expectedRewrites, CacheContainsStatistics.RewriteCount);
#endif
        }

        [Test]
        public void ListContainsWorksShortList([Range(0, 6)] int maxSize)
        {
            /** This test is different than the others, because it calls List.Contains(),
             * not the IQueryable extension method (which is called regardless of the type parameters
             * to IntersectStoreWithListTest) */
            var list = new List<int>() { 3, 5, 7 };
            var queryable = from i in storeList.AsQueryable().CacheContains(maxSize)
                            where list.Contains(i)
                            select i;

            var result = queryable.ToList();
            var expected = storeList.Intersect(list);
            var expectedRewrites = maxSize < list.Count() ? 0 : 1;

            Assert.AreEqual(expected, result);

#if DEBUG
            Assert.AreEqual(1, CacheContainsStatistics.ContainsCount);
            Assert.AreEqual(0, CacheContainsStatistics.DynamicLambdaCount);
            Assert.AreEqual(expectedRewrites, CacheContainsStatistics.RewriteCount);
#endif
        }

        [Test]
        public void IEnumerableContainsWorksShortList([Range(0,6)] int maxSize)
        {
            IEnumerable<int> myList = new List<int>() { 2, 4, 8 };
            InsersectStoreWithListTest(myList, maxSize);
        }

        [Test]
        public void ContainsWorksOneValue([Range(-2,10)] int value, 
                                          [Range(0, 6)] int maxSize)
        {
            IEnumerable<int> myList = new List<int>() { value };
            InsersectStoreWithListTest(myList, maxSize);
        }

        [Test]
        public void ContainsWorksEmptyList([Range(0, 6)] int maxSize)
        {
            IEnumerable<int> myList = new List<int>() { };
            InsersectStoreWithListTest(myList, maxSize);
        }

        [Test]
        public void DefaultMaxSizeWorksWhenLarge()
        {
            QueryableExtensions.DefaultMaxSize = 15;
            var list = new List<int>() { };
            for (int i = 0; i < 20; i+=2)
                list.Add(i);

            var queryable = from i in storeList.AsQueryable().CacheContains()
                            where list.Contains(i)
                            select i;

            var result = queryable.ToList();
            var expected = storeList.Intersect(list);

            Assert.AreEqual(expected, result);

#if DEBUG
            Assert.AreEqual(1, CacheContainsStatistics.ContainsCount);
            Assert.AreEqual(0, CacheContainsStatistics.DynamicLambdaCount);
            Assert.AreEqual(1, CacheContainsStatistics.RewriteCount);
#endif
        }

        [Test]
        public void DefaultMaxSizeWorksWhenSmall()
        {
            QueryableExtensions.DefaultMaxSize = 2;
            var list = new List<int>() { 1, 3, 5, 7 };

            var queryable = from i in storeList.AsQueryable().CacheContains()
                            where list.Contains(i)
                            select i;

            var result = queryable.ToList();
            var expected = storeList.Intersect(list);

            Assert.AreEqual(expected, result);

#if DEBUG
            Assert.AreEqual(1, CacheContainsStatistics.ContainsCount);
            Assert.AreEqual(0, CacheContainsStatistics.DynamicLambdaCount);
            Assert.AreEqual(0, CacheContainsStatistics.RewriteCount);
#endif
        }
    }
}