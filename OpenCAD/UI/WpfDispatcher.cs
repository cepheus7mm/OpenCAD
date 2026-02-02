using OpenCAD.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI
{
    public sealed class WpfDispatcher : IDispatcher
    {
        public void Invoke(Action action)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(action);
        }
    }
}
