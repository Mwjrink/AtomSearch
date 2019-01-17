using System;
using System.IO;

namespace AtomSearch
{
    public static class SettingsHelper
    {
        #region Fields

        public static string defaultCommandPrefix = "~";
        public static string appsPrefix = "a";
        public static string settingsPrefix = ":";
        public static string commandsPrefix = "/";
        public static string fileSearchPrefix = "f";
        public static string runPrefix = ">";
        public static string superSearchPrefix = "~";
        public static string appIndexLocation = "C:/ProgramData/Microsoft/Windows/Start Menu/Programs";
        public static string DbPath = "C:/ProgramData/Microsoft/Windows/Start Menu/Programs";

        public static int animationFrameRate = 60;

        #endregion Fields

        #region Methods

        //TODO should this be in a static constructor?
        public static void LoadSettings(string fileName)
        {
            string fileContents = null;
            using (var reader = new StreamReader(fileName))
                fileContents = reader.ReadToEnd();

            foreach (string setting in fileContents.Replace("\n", String.Empty).Replace("\r", String.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var settingArr = setting.Split(new[] { '=' }, 2);
                var settingName = settingArr[0].Trim(new[] { ';', ' ', '\r', '\n', '\t', '\"' });
                var value = settingArr[1].Trim(new[] { ';', ' ', '\r', '\n', '\t', '\"' });
                switch (settingName)
                {
                    case "defaultCommandPrefix":
                        defaultCommandPrefix = value;
                        break;

                    case "appsPrefix":
                        appsPrefix = value;
                        break;

                    case "settingsPrefix":
                        settingsPrefix = value;
                        break;

                    case "commandsPrefix":
                        commandsPrefix = value;
                        break;

                    case "fileSearchPrefix":
                        fileSearchPrefix = value;
                        break;

                    case "runPrefix":
                        runPrefix = value;
                        break;

                    case "superSearchPrefix":
                        superSearchPrefix = value;
                        break;

                    case "appIndexLocation":
                        appIndexLocation = value;
                        break;

                    case "DbPath":
                        appIndexLocation = value;
                        break;

                    case "animationFrameRate":
                        if (int.TryParse(value, out var result))
                            animationFrameRate = result;
                        else
                            throw new ArgumentException("Error parsing the value for animationFrameRate.");
                        break;
                }
            }
        }

        #endregion Methods
    }
}
