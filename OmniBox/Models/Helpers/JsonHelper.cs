using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;

namespace OmniBox
{
    public static class JsonHelper
    {
        #region Methods

        public static List<Command> Parse(IEnumerable<string> files)
        {
            var commands = new List<Command>();
            foreach (string filePath in files)
            {
                string fileContents = null;
                using (var reader = new StreamReader(filePath))
                    fileContents = reader.ReadToEnd();

                var obj = JsonConvert.DeserializeObject<Command>(fileContents);
                commands.Add(obj);
            }
            return commands;
        }

        #endregion Methods
    }
}
