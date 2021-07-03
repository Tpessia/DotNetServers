using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;

namespace DotNetServers.Shared
{
    public static class Extensions
    {
        public static string ToErrorString(this Exception ex) => ex.GetType().FullName + ": " + ex.Message +
            "\nInner: " + ex.InnerException?.Message + "\nStack: " + ex.StackTrace + "\n";

        public static void Deconstruct<T>(this IList<T> list, out T first, out IList<T> rest)
        {

            first = list.Count > 0 ? list[0] : default; // or throw
            rest = list.Skip(1).ToList();
        }

        public static void Deconstruct<T>(this IList<T> list, out T first, out T second, out IList<T> rest)
        {
            first = list.Count > 0 ? list[0] : default; // or throw
            second = list.Count > 1 ? list[1] : default; // or throw
            rest = list.Skip(2).ToList();
        }

        public static string Join(this IEnumerable<string> list, string separator) => string.Join(separator, list);

        public static string Join<T>(this IEnumerable<T> list, string separator, Func<T, string> selector) => string.Join(separator, list.Select(selector));

        public static string Repeat(this string str, uint count) => string.Concat(Enumerable.Repeat(str, (int)count));
    }
}
