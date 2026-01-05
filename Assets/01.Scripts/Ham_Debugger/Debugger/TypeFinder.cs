using System;
using System.Collections.Generic;
using System.Linq;

public static class TypeFinder
{
    // Warning about performance degradation due to reflection usage
    public static List<Type> GetTypesDerivedFrom<T>()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                Type[] types = null;
                try { types = a.GetTypes(); } catch { return new Type[0]; }
                return types;
            })
            .Where(t => typeof(T).IsAssignableFrom(t) && t != typeof(T) && !t.IsAbstract)
            .ToList();
    }
}