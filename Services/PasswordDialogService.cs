using Map.Popups;
using Map.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Map.Services
{
    public sealed class PasswordDialogService : IPasswordDialogService
    {
        private readonly Window _owner;
        public PasswordDialogService(Window owner) => _owner = owner;

        public bool ShowPassword()
        {
            var popup = new PasswordPopup { Owner = _owner };
            return popup.ShowDialog() == true;
        }
    }
}
