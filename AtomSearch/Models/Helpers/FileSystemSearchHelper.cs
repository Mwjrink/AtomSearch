using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AtomSearch.Models.Helpers
{
    public static class FileSystemSearchHelper
    {
        #region Fields

        private const string FileSystemIndexTableName = "FileSystemIndex";
        private static readonly HashSet<string> IndexedLocations = new HashSet<string>();

        #endregion Fields

        #region Methods

        private static string NormalizePath(this string path)
        {
            return Path.GetFullPath(new Uri(path).LocalPath)
                       .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       .ToUpperInvariant();
        }

        public static async Task Index(string path)
        {
            IndexedLocations.Add(path.NormalizePath());
        }

        #endregion Methods
    }
}
