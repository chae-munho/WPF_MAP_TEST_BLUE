using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Map.ViewModels
{
    public partial class AlertItemViewModel : ObservableObject
    {
        [ObservableProperty] private string msg = "";
        [ObservableProperty] private string time = "";
    }
}
