using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Malie_Script_Tool
{
    class Program
    {
        static void Main(string[] args)
        {
            Script script = new Script();
            script.Load("exec.dat");
            script.ExportStrings("exec.dat.txt");
        }
    }
}
