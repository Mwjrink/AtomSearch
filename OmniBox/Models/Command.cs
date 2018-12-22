using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OmniBox
{
    public struct Command
    {
        #region Fields
        
        public string command;
        public string filePath;
        public string mode;
        public string image;
        public string commandFormat;
        public string description;
        public string nonAlphaNumericCharacterEncodingFormat;

        public bool requiresSelectedResult;

        public Func<string, IEnumerable<Result>> _CustomResultsAction;
        
        public string resultsHTTPRequestFormat;
        public string resultsProcessInvokeFileName;

        public int resultsArrayIndex;

        public Flag[] flags;

        #endregion Fields
    }

    public struct Flag
    {
        #region Fields

        public string commandFlag;
        public string image;
        public string commandFormat;
        public string argument;
        public string description;

        #endregion Fields
    }

    public struct ResultSet
    {
        public string query;
        public string[] resultNames;
    }
}
