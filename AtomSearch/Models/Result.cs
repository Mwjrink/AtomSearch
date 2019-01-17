using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Media;

namespace AtomSearch
{
    public class Result
    {
        #region Properties

        public object IconSource { get; }

        public string DisplayText { get; }

        public string Descriptor { get; }

        public string ExecutionText { get; }

        public double MatchRank => MatchRankStringDifference + MatchRankReOcurrance;

        public double MatchRankStringDifference { get; set; }

        public double MatchRankReOcurrance { get; set; }

        //5000, 4000 is string difference, 1000 is number of occurences

        #endregion Properties

        #region Constructors

        public Result(string resultText, string iconPath, string executionText = null, string descriptor = null, double matchRank = double.MaxValue, double normalization = 4000)
        {
            DisplayText = resultText;
            this.ExecutionText = executionText;

            IconSource = Path.Combine(
#if DEBUG
                    "..", "..",
#endif
                    "Resources", iconPath);
            
            this.Descriptor = descriptor;
            this.MatchRankStringDifference = (double)4000 - matchRank / normalization * (double)4000;
        }

        public Result(string resultText, Icon icon, string executionText = null, string descriptor = null, double matchRank = double.MaxValue, double normalization = 4000)
        {
            DisplayText = resultText;
            this.ExecutionText = executionText;

            IconSource = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        System.Windows.Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            
            this.Descriptor = descriptor;
            this.MatchRankStringDifference = (double)4000 - matchRank / normalization * (double)4000;
        }

        #endregion Constructors
    }
}
