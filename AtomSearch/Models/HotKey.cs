using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Interop;

namespace AtomSearch
{
    public sealed class HotKey : IDisposable
    {
        #region Fields

        private static readonly Dictionary<int, Action> _hotKeyCallbackDict = new Dictionary<int, Action>();

        private bool _disposed = false;

        public const int WmHotKey = 0x0312;

        #endregion Fields

        #region Properties

        public Key Key { get; private set; }

        public KeyModifier KeyModifiers { get; private set; }

        public Action Action { get; private set; }

        public int Id { get; private set; }

        #endregion Properties

        #region Constructors

        public HotKey(Key k, KeyModifier keyModifiers, Action action, bool register = true)
        {
            Key = k;
            KeyModifiers = keyModifiers;
            Action = action;
            if (register)
                Register();
        }

        #endregion Constructors

        #region Methods

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, UInt32 fsModifiers, UInt32 vlc);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private void ComponentDispatcherThreadFilterMessage(ref MSG msg, ref bool handled)
        {
            if (!handled && msg.message == WmHotKey)
            {
                if (_hotKeyCallbackDict.TryGetValue((int)msg.wParam, out var action))
                    if (action != null)
                        App.Current.Dispatcher.BeginInvoke(action);
                handled = true;
            }
        }

        // Might need to be in a dispatch thread
        public bool Register()
        {
            int virtualKeyCode = KeyInterop.VirtualKeyFromKey(Key);
            Id = virtualKeyCode + ((int)KeyModifiers * 0x10000);
            bool result = RegisterHotKey(IntPtr.Zero, Id, (UInt32)KeyModifiers, (UInt32)virtualKeyCode);

            ComponentDispatcher.ThreadFilterMessage += new ThreadMessageEventHandler(ComponentDispatcherThreadFilterMessage);

            _hotKeyCallbackDict.Add(Id, Action);

            Debug.Print(string.Join(", ", result, Id, virtualKeyCode));
            return result;
        }

        public void Unregister()
            => _hotKeyCallbackDict.Remove(Id);

        public void Dispose()
        {
            if (!_disposed)
            {
                Unregister();
                _disposed = true;
            }
        }

        #endregion Methods
    }

    [Flags]
    public enum KeyModifier
    {
        None = 0x0000,
        Alt = 0x0001,
        Ctrl = 0x0002,
        NoRepeat = 0x4000,
        Shift = 0x0004,
        Win = 0x0008
    }
}
