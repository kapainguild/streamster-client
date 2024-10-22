using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models.Chats
{
    public class BaseAuthenticateModel
    {
        public Action Login { get; set; }

        public Action Logout { get; set; }

        public bool ShowPopupDialog { get; set; }
    }
}
