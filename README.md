# EFCacheContains

[![Build status](https://ci.appveyor.com/api/projects/status/d2aj00i8de4thr0e/branch/master?svg=true)](https://ci.appveyor.com/project/bchurchill/efcachecontains/branch/master)

A library to help cache query plans using the 'Contains' method for Entity Framework

# About

Entity Framework translates queries written in Linq to SQL, and this is an expensive compilation.  To help with performance, Entity Framework caches its "query plans" after compiling them (not to be confused with the SQL database's query plan).  However, there are several cases where EF does not perform caching.  One is the case where the `IEnumerable.Contains()` method is invoked.  This library solves the problem by intercepting the expression trees before they get to EF and rewriting the contains method so that it is cached -- at least for small lists.  By default it will rewrite `xs.Contains(x)` as `xs[0] == x || xs[1] == x || ... || xs[n] == x`.  However, it only performs this for lists up to a configurable length, by default 5.  In principle, EF should create one query plan for each different array length.  When no rewriting is done (i.e. for long lists), no query plan is cached.

# How to Use

```
using berkeleychurchill.CacheContains.QueryableExtensions;

...

var myQuery = from r in myContext.Records.CacheContains()
              where myList.Contains(r.Id)
              select r;
```





