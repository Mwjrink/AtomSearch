using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace OmniBox
{
    public static class Extensions
    {
        #region Methods

        public static Task BeginAsync(this Storyboard storyBoard, FrameworkElement container = null, HandoffBehavior handoffBehavior = HandoffBehavior.SnapshotAndReplace)
        {
            var tcs = new TaskCompletionSource<bool>();

            if (storyBoard == null)
                tcs.SetException(new ArgumentNullException(nameof(storyBoard)));
            else
            {
                EventHandler onComplete = null;
                onComplete = (s, e) =>
                {
                    storyBoard.Completed -= onComplete;
                    tcs.SetResult(true);
                };

                storyBoard.Completed += onComplete;

                if (container != null)
                    storyBoard.Begin(container, handoffBehavior);
                else
                    storyBoard.Begin();
            }
            return tcs.Task;
        }

        #endregion Methods
    }
}
