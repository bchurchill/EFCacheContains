using NUnit.Framework;
using berkeleychurchill.CacheContains;
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

        [Test]
        public void ContainsWorks()
        {
            IEnumerable<int> myList = new List<int>() { 2, 4, 8 };
            IQueryable<int> myStore = (new List<int>() { 1, 2, 3, 4, 5, 6, 7 }).AsQueryable();

            var result = from i in myStore
                         where myList.Contains(i)
                         select i;

            var expected = new List<int>() { 2, 4 };

            Assert.AreEqual(expected, result.ToList());
        }
    }
}