using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AtomSearch
{
    public static class HttpHelper
    {
        private static readonly HttpClient client = new HttpClient();

        public static async Task<string> RequestStringAsync(string url) 
            => await client.GetStringAsync(url).ConfigureAwait(false);

        public static string RequestString(string url)
            => client.GetStringAsync(url).GetAwaiter().GetResult();
    }
}
