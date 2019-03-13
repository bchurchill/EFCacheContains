# EFCacheContains

[![Build status](https://ci.appveyor.com/api/projects/status/d2aj00i8de4thr0e/branch/master?svg=true)](https://ci.appveyor.com/project/bchurchill/efcachecontains/branch/master)

A library to help cache query plans using the 'Contains' method for Entity Framework

# About

Entity Framework translates queries written in Linq to SQL, and this is an expensive compilation.  To help with performance, Entity Framework caches its "query plans" after compiling them (not to be confused with the SQL database's query plan).  However, there are several cases where EF does not perform caching.  One is the case where the `IEnumerable.Contains()` method is invoked.  This library solves the problem by intercepting the expression trees before they get to EF and rewriting the contains method so that it is cached -- at least for small lists.  By default it will rewrite `xs.Contains(x)` as `xs[0] == x || xs[1] == x || ... || xs[n] == x`.  However, it only performs this for lists up to a configurable length, by default 5.  In principle, EF should create one query plan for each different array length.  When no rewriting is done (i.e. for long lists), no query plan is cached.

# Installation

This is distributed as a NuGet package on nuget.org:

```
Install-Package EFCacheContains -Version 1.0.31-Release
```

# Usage

```
using berkeleychurchill.CacheContains;

...

var myQuery = from r in myContext.Records.CacheContains()
              where myList.Contains(r.Id)
              select r;
```

Typically, `myQuery` will reference an expression tree that has a call to `Contains` in it.  But, so long as `myList` contains 5 elements or fewer, the call to `CacheContains()` will rewrite the expression tree as a cascade of boolean checks.  To change the cutoff size from 5 to another value, pass it as a parameter to `CacheContains`:

```

var myQuery = from r in myContext.Records.CacheContains(10)
              where myList.Contains(r.Id)
              select r;
```

One can also change the default maximum list size to rewrite the expression tree:

```
CacheContains.QueryableExtensions.DefaultMaxSize = 20;
```

# Release Notes

**1.0.31** 
  * Important bug fix for lists containing more than 2 items.
  * Performance improvements
  * Additional testing.

**1.0.26** Initial release.

