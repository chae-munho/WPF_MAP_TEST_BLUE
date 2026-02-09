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
        [ObservableProperty] private string valueText = "100";
        [ObservableProperty] private double barHeight = 97; // Image Height 바인딩
    }
}
