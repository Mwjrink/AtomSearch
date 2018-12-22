using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniBox.Models.Helpers
{
    public static class DbHelper
    {
        // DB
        /*
         * CommandText
         *    Compared against with string difference matcher, PRIMARY KEY TEXT
         * DependentUpon?
         *     Plugin this depends on
         * UseCount (better name?)
         *     self explanatory
         */

        private static readonly string updateCommand =
        @"INSERT OR REPLACE INTO main (CommandText, Uses) VALUES
          (
              'OmniBoxeheh',
              IFNULL((SELECT Uses + 1 FROM main WHERE CommandText = 'OmniBoxeheh'),1)
          );";
    }
}
