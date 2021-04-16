using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Malie_Script_Tool
{
    class Program
    {
        static void Main(string[] args)
        {
            var script = new Script();

            script.Load(@".\data\system\exec.org.dat");

            script.ExportMessages(@".\data\system\exec.msg.txt");
            script.ImportMessages(@".\data\system\exec.msg.txt");

            script.ExportStrings(@".\data\system\exec.str.txt");
            script.ImportStrings(@".\data\system\exec.str.txt");

            script.Save(@".data\system\exec.dat");
        }
    }
}
