
using System;

namespace Streamster.ClientCore.Models.Chats
{
    public class UserPasswordAuthenticateModel : BaseAuthenticateModel
    {
        public string Title { get; set; }

        public string Description { get; set; }

        public string User { get; set; }

        public string Password { get; set; }

        public bool CanSave { get; set; }
    }
}
