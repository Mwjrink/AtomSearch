using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AtomSearch
{
    //https://@URL/favicon.ico
    //https://www.reddit.com/favicon.ico
    public static class CommandHelper
    {
        private static Dictionary<string, Dictionary<string, Flag>> flagsDict;

        private const string ARGUMENTS_PARAMETER_NAME = "@arguments";
        private const string COMMAND_PARAMETER_NAME = "@command";
        private const string UNICODE_PARAMETER_NAME = "@unicode";

        #region Methods

        private static void EnsureFlagsDict(Command command)
        {
            if (flagsDict == null)
                flagsDict = new Dictionary<string, Dictionary<string, Flag>>();
            
            if (!flagsDict.ContainsKey(command.command))
            {
                flagsDict[command.command] = new Dictionary<string, Flag>();
                foreach (var flag in command.flags ?? Enumerable.Empty<Flag>())
                    flagsDict[command.command].Add(flag.commandFlag, flag);
            }
        }

        // if * is the json command it will be matched if none of the other flags do, error if more than one * is detected
        // validateCommandJson "@path" to validate your JSon command, (no double asterisks, no double commands ("i " specifiec twice) etc.)
        public static (string filePath, string arguments) GetCommand(this Command command, Result? selected, string provided)
        {
            EnsureFlagsDict(command);

            string constructed = command.commandFormat;
            string arguments = String.Empty;

            if (provided.StartsWith(command.command))
                provided = provided.Remove(0, command.command.Length).TrimStart();

            while (flagsDict[command.command].TryGetValue(provided.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries)[0], out Flag flag))
                flag.GetCommand(ref provided, ref constructed, ref arguments);

            constructed = constructed.Replace(ARGUMENTS_PARAMETER_NAME, arguments);
            constructed = constructed.Replace(COMMAND_PARAMETER_NAME, selected.HasValue ? selected.Value.path : provided);

            return (command.filePath, constructed);
        }

        public static string GetDescription(this Command command, string provided)
        {
            EnsureFlagsDict(command);

            var descriptions = new List<string>();

            if (provided.StartsWith(command.command))
                provided = provided.Remove(0, command.command.Length).TrimStart();

            while (flagsDict[command.command].TryGetValue(provided.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries)[0], out Flag flag))
                flag.GetDescription(ref provided, descriptions);

            return String.Join(", ", descriptions);
        }

        public static string GetImage(this Command command, string provided)
        {
            EnsureFlagsDict(command);

            if (provided.StartsWith(command.command))
                provided = provided.Remove(0, command.command.Length).TrimStart();

            string imagePath = command.image;

            while (flagsDict[command.command].TryGetValue(provided.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries)[0], out var flag))
                flag.GetImage(ref provided, ref imagePath);

            return imagePath;
        }

        public static IEnumerable<Result> GetResults(this Command command, string provided)
        {
            //TODO allow customization of the result set and changing useCustomResult request by subcommand/flag
            //EnsureFlagsDict();

            if (provided.StartsWith(command.command))
                provided = provided.Remove(0, command.command.Length).TrimStart();

            if (command._CustomResultsAction != null)
                return command._CustomResultsAction?.Invoke(provided);

            var results = new List<Result>();

            if (command.resultsHTTPRequestFormat != null)
            {
                foreach (var non in provided.Where(c => !(char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '-')).ToArray())
                    provided = provided.Replace("" + non, 
                        command.nonAlphaNumericCharacterEncodingFormat.Replace(UNICODE_PARAMETER_NAME, ((int)non).ToString("X")));

                var httpResult = HttpHelper.RequestString(command.resultsHTTPRequestFormat.Replace(COMMAND_PARAMETER_NAME, provided));

                var obj = JsonConvert.DeserializeObject<object[]>(httpResult);

                var resultsArray = (JArray)obj[command.resultsArrayIndex];

                results.AddRange(resultsArray.ToObject<string[]>().Select(x => new Result(x, command.image)));
            }
            else if (command.resultsProcessInvokeFileName != null)
            {
                foreach (var non in provided.Where(c => !(char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '-')).ToArray())
                    provided = provided.Replace("" + non,
                        command.nonAlphaNumericCharacterEncodingFormat.Replace(UNICODE_PARAMETER_NAME, ((int)non).ToString("X")));

                var resultsProcessInvokeFileName = command.resultsProcessInvokeFileName;
                var processResult = Process.Start(new ProcessStartInfo()
                {
                    FileName = resultsProcessInvokeFileName,
                    Arguments = provided,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                }).StandardOutput.ReadToEnd();

                var obj = JsonConvert.DeserializeObject<object[]>(processResult);

                var resultsArray = (JArray)obj[command.resultsArrayIndex];

                results.AddRange(resultsArray.ToObject<string[]>().Select(x => new Result(x, command.image)));
            }

            return results;
        }

        #endregion Methods

        // FLAGS
        #region Methods

        private static void GetCommand(this Flag flag, ref string provided, ref string constructed, ref string arguments)
        {
            if (provided.StartsWith(flag.commandFlag))
            {
                provided = provided.Remove(0, flag.commandFlag.Length).TrimStart();

                if (flag.argument != null)
                    arguments += " " + flag.argument;

                if (flag.commandFormat != null)
                    constructed = flag.commandFormat;
            }
        }

        private static void GetDescription(this Flag flag, ref string provided, List<string> descriptions)
        {
            if (provided.StartsWith(flag.commandFlag))
            {
                provided = provided.Remove(0, flag.commandFlag.Length).TrimStart();

                if (!String.IsNullOrWhiteSpace(flag.description))
                    descriptions.Add(flag.description);
            }
        }

        private static void GetImage(this Flag flag, ref string provided, ref string imagePath)
        {
            if (provided.StartsWith(flag.commandFlag))
            {
                provided = provided.Remove(0, flag.commandFlag.Length).TrimStart();

                if (!String.IsNullOrWhiteSpace(flag.image))
                    imagePath = flag.image;
            }
        }

        #endregion Methods
    }
}
