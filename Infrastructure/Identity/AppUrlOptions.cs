using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Identity
{
    public class AppUrlOptions
    {
        public const string SectionName = "AppUrls";

        /// <summary>Domain frontend — dùng để dựng link trong email và cấu hình CORS.</summary>
        public string Frontend { get; set; } = "http://localhost:3000";
    }
}
