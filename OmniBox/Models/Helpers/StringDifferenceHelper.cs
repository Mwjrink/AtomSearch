using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniBox
{
    public static class StringDifferenceHelper
    {
        #region Methods

        public static int LevenshteinDistance(string sourceA, string sourceB)
        {
            var A = sourceA.ToLowerInvariant();
            var B = sourceB.ToLowerInvariant();

            int n = A.Length;
            int m = B.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0)
                return m;

            if (m == 0)
                return n;

            for (int i = 0; i <= n; d[i, 0] = i++)
                ;

            for (int j = 0; j <= m; d[0, j] = j++)
                ;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (A[i - 1] == B[j - 1]) ? 0 : 1;

                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }

        #endregion Methods
    }
}
