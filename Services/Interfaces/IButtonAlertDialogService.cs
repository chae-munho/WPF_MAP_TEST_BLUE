using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Map.Services.Interfaces
{
    public interface IButtonAlertDialogService
    {
        bool ShowMessage(string message);
    }
}
