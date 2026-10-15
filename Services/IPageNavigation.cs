using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace SmallBazar.Services
{
    public interface IPageNavigation
    {
        Task NavigateToPageAsync(string pageName="P1");
        INavigation NativeNavigation { get; }
    }
}
