using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Malie_Script_Tool
{
    static class Extensions
    {
        public static bool HasFlag(this uint value, uint flag)
        {
            return (value & flag) != 0;
        }

        public static byte[] ReadBytes(this BinaryReader @this, uint count)
        {
            return @this.ReadBytes(Convert.ToInt32(count));
        }
    }
}
