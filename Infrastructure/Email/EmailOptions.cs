using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Email
{
    public class EmailOptions
    {
        public const string SectionName = "Email";

        public string Host { get; set; } = null!;

        public int Port { get; set; } = 587;

        public bool UseStartTls { get; set; } = true;

        public string? UserName { get; set; }

        public string? Password { get; set; }

        public string FromEmail { get; set; } = null!;

        public string FromName { get; set; } = "WebShop";
    }
}
