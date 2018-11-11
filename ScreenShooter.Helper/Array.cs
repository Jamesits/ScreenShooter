using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScreenShooter.Helper
{
    public static class Array
    {
        /// <summary>
        /// Determines whether a System.Collections.Generic.List<T> object is a subset of the specified collection.
        /// http://geekswithblogs.net/mnf/archive/2011/05/13/issubsetof-list-extension.aspx
        /// http://stackoverflow.com/questions/332973/linq-check-whether-an-array-is-a-subset-of-another
        /// </summary>
        /// <param name="expectedBiggerList"></param>
        /// <param name="expectedSmallerList"></param>
        /// <returns></returns>
        public static bool IsSubsetOf<T>(this IEnumerable<T> expectedSmallerList, IEnumerable<T> expectedBiggerList)
        {
            var isSubset = !expectedSmallerList.Except(expectedBiggerList).Any();
            return isSubset;
        }
    }
}
