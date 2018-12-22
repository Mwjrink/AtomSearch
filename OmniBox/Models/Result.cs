using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Media;

namespace OmniBox
{
    public struct Result
    {
        #region Properties

        public object IconSource { get; }

        public string ResultText { get; }

        public string Descriptor { get; }

        public string path { get; }

        #endregion Properties

        #region Constructors

        public Result(string resultText, string iconPath, string path = null, string descriptor = null)
        {
            ResultText = resultText;
            this.path = path;

            IconSource = Path.Combine(
#if DEBUG
                    "..", "..",
#endif
                    "Resources", iconPath);
            
            this.Descriptor = descriptor;
        }

        public Result(string resultText, Icon icon, string path = null, string descriptor = null)
        {
            ResultText = resultText;
            this.path = path;

            IconSource = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        System.Windows.Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            
            this.Descriptor = descriptor;
        }

        #endregion Constructors
    }
}
