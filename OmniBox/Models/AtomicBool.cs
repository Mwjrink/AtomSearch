using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OmniBox
{
    public struct AtomicBool
    {
        #region Fields

        private const int TRUE_VALUE = 1;
        private const int FALSE_VALUE = 0;
        private int _value;

        #endregion Fields

        #region Properties

        public bool Value
        {
            get => _value == TRUE_VALUE;
            set => Interlocked.Exchange(ref _value, value ? TRUE_VALUE : FALSE_VALUE);
        }

        #endregion Properties

        #region Methods

        public static implicit operator bool(AtomicBool other)
            => other._value == TRUE_VALUE;

        public bool TrueToFalse()
                    => Interlocked.CompareExchange(ref _value, FALSE_VALUE, TRUE_VALUE) == TRUE_VALUE;

        public bool FalseToTrue()
            => Interlocked.CompareExchange(ref _value, TRUE_VALUE, FALSE_VALUE) == FALSE_VALUE;

        #endregion Methods
    }
}
