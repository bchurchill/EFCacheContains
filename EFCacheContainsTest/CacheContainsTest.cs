using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace berkeleychurchill.CacheContains.Test
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        /** TODO: It would be nice to test that this actually causes EF
         * to cache things properly, but I think actually making a test 
         * to do this reliably would be hard, since EF doesn't expose its
         * internals visibly. */

        [Test]
        public void ContainsWorksShortList([Range(0,6)] int maxSize)
        {
            IEnumerable<int> myList = new List<int>() { 2, 4, 8 };
            IQueryable<int> myStore = (new List<int>() { 1, 2, 3, 4, 5, 6, 7 }).AsQueryable();

            var result = from i in myStore.CacheContains(maxSize)
                         where myList.Contains(i)
                         select i;

            var expected = new List<int>() { 2, 4 };

            Assert.AreEqual(expected, result.ToList());
        }

        [Test]
        public void ContainsWorksOneValue([Range(-10,10)] int value, 
                                          [Range(0, 6)] int maxSize)
        {
            IEnumerable<int> myList = new List<int>() { value };
            var storeList = new List<int>() { 1, 2, 3, 4, 5, 6, 7 };
            IQueryable<int> myStore = storeList.AsQueryable();

            var result = from i in myStore.CacheContains(maxSize)
                         where myList.Contains(i)
                         select i;
            
            var expected = new List<int>();
            if (storeList.Contains(value))
                expected.Add(value);

            Assert.AreEqual(expected, result.ToList());
        }

        [Test]
        public void ContainsWorksEmptyList([Range(0, 6)] int maxSize)
        {
            IEnumerable<int> myList = new List<int>() { };
            var storeList = new List<int>() { 1, 2, 3, 4, 5, 6, 7 };
            IQueryable<int> myStore = storeList.AsQueryable();

            var result = from i in myStore.CacheContains(maxSize)
                         where myList.Contains(i)
                         select i;

            var expected = new List<int>();

            Assert.AreEqual(expected, result.ToList());
        }
    }
}