using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Map.ViewModels
{
    public partial class GraphPointViewModel : ObservableObject
    {
        [ObservableProperty] private string valueText = "0";
        [ObservableProperty] private double barWidth = 0; //  fillGraph Width 바인딩
    }
}
