using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Exceptions
{
    public class ForbiddenAccessException(string message = "Bạn không có quyền thực hiện thao tác này.")
     : Exception(message);
}
