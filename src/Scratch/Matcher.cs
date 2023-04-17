using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Demo;

partial class Matcher
{
    [GeneratedRegex(@"[a-z0-9]+@[a-z0-9]+\.[a-z]{2,}$")]
    public static partial Regex CreateEmailRegex();
}
