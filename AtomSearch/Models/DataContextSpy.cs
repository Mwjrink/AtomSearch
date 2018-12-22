using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace AtomSearch
{
    public class DataContextSpy : Freezable
    {
        #region Fields

        // Borrow the DataContext dependency property from FrameworkElement.
        public static readonly DependencyProperty DataContextProperty = FrameworkElement
            .DataContextProperty.AddOwner(typeof(DataContextSpy));

        #endregion Fields

        #region Properties

        public object Context
        {
            get { return GetValue(DataContextProperty); }
            set { SetValue(DataContextProperty, value); }
        }

        #endregion Properties

        #region Constructors

        public DataContextSpy()
        {
            // This binding allows the spy to inherit a DataContext.
            BindingOperations.SetBinding(this, DataContextProperty, new Binding());
        }

        #endregion Constructors

        #region Methods

        private void Current_Exit(object sender, ExitEventArgs e)
                    => throw new NotImplementedException();

        // We are required to override this abstract method.
        protected override Freezable CreateInstanceCore()
            => throw new NotImplementedException();

        #endregion Methods
    }
}
