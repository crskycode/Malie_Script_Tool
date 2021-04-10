using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Malie_Script_Tool
{
    class Script
    {
        public Script()
        {
        }

        public void Load(string filePath)
        {
            using (Stream stream = File.OpenRead(filePath))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                Read(reader);
                //Dump();
                Parse();
            }
        }

        List<Symbol> _symbols = new List<Symbol>();
        List<Function> _functions = new List<Function>();
        List<Label> _labels = new List<Label>();
        byte[] _seg_constant;
        byte[] _seg_code;
        List<MsgDefine> _msg_defines = new List<MsgDefine>();
        byte[] _seg_message;
        List<Tuple<uint, string>> _message_strings = new List<Tuple<uint, string>>();

        void Read(BinaryReader reader)
        {
            // Clear up before reading
            _symbols.Clear();
            _functions.Clear();
            _labels.Clear();
            _seg_constant = null;
            _seg_code = null;
            _msg_defines.Clear();
            _seg_message = null;

            void ReadSymbols()
            {
                int count = reader.ReadInt32();

                for (int i = 0; i < count; i++)
                {
                    var entry = new Symbol();

                    entry.Name = ReadString(reader);

                    sub_77C350();

                    entry.Type = reader.ReadInt32(); // type 0:func?  1:?  2:?  3:var  4:func
                    entry.field_8 = reader.ReadInt32();
                    entry.field_14 = reader.ReadInt32(); // offset or order
                    entry.field_C = reader.ReadInt32();

                    _symbols.Add(entry);
                }

                var unk = reader.ReadInt32();
            }

            void sub_77C350()
            {
                uint id = reader.ReadUInt32();
                if (id == 0)
                    return;
                reader.ReadUInt32();
                sub_77C350();
            }

            void ReadFunctions()
            {
                int count = reader.ReadInt32();

                for (int i = 0; i < count; i++)
                {
                    var entry = new Function();

                    entry.Name = ReadString(reader);
                    entry.Index = reader.ReadInt32();
                    entry.field_4 = reader.ReadInt32();
                    entry.Offset = reader.ReadInt32();

                    _functions.Add(entry);
                }
            }

            void ReadLabels()
            {
                int count = reader.ReadInt32();

                for (int i = 0; i < count; i++)
                {
                    var entry = new Label();

                    entry.Name = ReadString(reader);
                    entry.Offset = reader.ReadInt32();

                    _labels.Add(entry);
                }
            }

            void ReadConstantSegment()
            {
                int size = reader.ReadInt32();
                _seg_constant = reader.ReadBytes(size);
            }

            void ReadCodeSegment()
            {
                int size = reader.ReadInt32();
                _seg_code = reader.ReadBytes(size);
            }

            void ReadMsgDefines()
            {
                int count = reader.ReadInt32();

                for (int i = 0; i < count; i++)
                {
                    var entry = new MsgDefine();

                    entry.Offset = reader.ReadInt32();
                    entry.Length = reader.ReadInt32();

                    _msg_defines.Add(entry);
                }
            }

            void ReadMsgSegment()
            {
                int size = reader.ReadInt32();
                _seg_message = reader.ReadBytes(size);
            }

            ReadSymbols();

            ReadFunctions();

            ReadLabels();

            ReadConstantSegment();

            ReadCodeSegment();

            ReadMsgDefines();

            ReadMsgSegment();


            Debug.Assert(reader.BaseStream.Position == reader.BaseStream.Length);
        }

        void Dump()
        {
            foreach (var item in _symbols)
            {
                Console.WriteLine($"{"SYMBOL",-12} {item.Name,-32} {item.Type:X8} {item.field_8:X8} {item.field_14:X8} {item.field_C:X8}");
            }

            foreach (var item in _functions)
            {
                Console.WriteLine($"{"FUNCTION",-12} {item.Name,-32} {item.Index:X8} {item.field_4:X8} {item.Offset:X8}");
            }
        }

        void Parse()
        {
            // Clear up before parsing
            _message_strings.Clear();

            using (MemoryStream stream = new MemoryStream(_seg_code))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                // Hack
                uint last_push = 0;
                string last_string = string.Empty;

                while (stream.Position < stream.Length)
                {
                    long addr = stream.Position;
                    byte code = reader.ReadByte();

                    switch (code)
                    {
                        // JUMP
                        case 0x00:
                        {
                            var offset = reader.ReadUInt32();

                            var label = _labels.Find(a => a.Offset == offset);

                            if (label != null)
                                Console.WriteLine($"{addr:X8}| JUMP {label.Name}");
                            else if (offset < _seg_code.Length)
                                Console.WriteLine($"{addr:X8}| JUMP {offset:X8}h");
                            else
                                Console.WriteLine($"{addr:X8}| JUMP {offset:X8}h ; WTF");

                            break;
                        }
                        // TJUMP
                        case 0x01:
                        {
                            uint offset = reader.ReadUInt32();

                            var label = _labels.Find(a => a.Offset == offset);

                            if (label != null)
                                Console.WriteLine($"{addr:X8}| TJUMP {label.Name}");
                            else if (offset < _seg_code.Length)
                                Console.WriteLine($"{addr:X8}| TJUMP {offset:X8}h");
                            else
                                throw new Exception();

                            break;
                        }
                        // FJUMP
                        case 0x02:
                        {
                            uint offset = reader.ReadUInt32();

                            var label = _labels.Find(a => a.Offset == offset);

                            if (label != null)
                                Console.WriteLine($"{addr:X8}| FJUMP {label.Name}");
                            else if (offset < _seg_code.Length)
                                Console.WriteLine($"{addr:X8}| FJUMP {offset:X8}h");
                            else
                                throw new Exception();

                            break;
                        }
                        // CALL_C
                        case 0x03:
                        {
                            int index = reader.ReadInt32();
                            uint argc = reader.ReadByte();

                            if (index >= 0 && index < _functions.Count)
                                Console.WriteLine($"{addr:X8}| CALL_C {_functions[index].Name} ({argc})");
                            else
                                throw new Exception();

                            break;
                        }
                        // CALL_CB
                        case 0x04:
                        {
                            int index = reader.ReadByte();
                            uint argc = reader.ReadByte();

                            if (index >= 0 && index < _functions.Count)
                                Console.WriteLine($"{addr:X8}| CALL_CB {_functions[index].Name} ({argc})");
                            else
                                throw new Exception();

                            break;
                        }
                        // STOP
                        case 0x05:
                        {
                            Console.WriteLine($"{addr:X8}| STOP");
                            break;
                        }
                        // LOAD
                        case 0x06:
                        {
                            Console.WriteLine($"{addr:X8}| LOAD");
                            break;
                        }
                        // STORE
                        case 0x07:
                        {
                            Console.WriteLine($"{addr:X8}| STORE");
                            break;
                        }
                        // LOADCONST
                        case 0x08:
                        {
                            uint value = reader.ReadUInt32();

                            Console.WriteLine($"{addr:X8}| LOADCONST {value:X8}h ({value})");
                            break;
                        }
                        // LOADSTRING1
                        case 0x09:
                        {
                            int offset = reader.ReadByte();

                            if (offset < _seg_constant.Length)
                            {
                                var str = StringFromBuffer(_seg_constant, offset);
                                last_string = str;
                                str = StringFormatDisplay(str);

                                Console.WriteLine($"{addr:X8}| LOADSTRING1 \"{str}\"");
                            }
                            else
                            {
                                throw new Exception();
                            }

                            break;
                        }
                        // LOADSTRING2
                        case 0x0A:
                        {
                            int offset = reader.ReadUInt16();

                            if (offset < _seg_constant.Length)
                            {
                                var str = StringFromBuffer(_seg_constant, offset);
                                last_string = str;
                                str = StringFormatDisplay(str);

                                Console.WriteLine($"{addr:X8}| LOADSTRING2 \"{str}\"");
                            }
                            else
                            {
                                throw new Exception();
                            }

                            break;
                        }
                        // LOADSTRING4
                        case 0x0C:
                        {
                            uint offset = reader.ReadUInt32();

                            if (offset < _seg_constant.Length)
                            {
                                var str = StringFromBuffer(_seg_constant, (int)offset);
                                last_string = str;
                                str = StringFormatDisplay(str);

                                Console.WriteLine($"{addr:X8}| LOADSTRING4 \"{str}\"");
                            }
                            else
                            {
                                throw new Exception();
                            }

                            break;
                        }
                        // PUSH
                        case 0x0D:
                        {
                            uint value = reader.ReadUInt32();
                            last_push = value;

                            Console.WriteLine($"{addr:X8}| PUSH {value:X8}h ({value})");
                            break;
                        }
                        // POP
                        case 0x0E:
                        {
                            Console.WriteLine($"{addr:X8}| POP");
                            break;
                        }
                        // PUSHZ
                        case 0x0F:
                        {
                            last_push = 0;

                            Console.WriteLine($"{addr:X8}| PUSHZ");
                            break;
                        }
                        // PUSHB
                        case 0x11:
                        {
                            uint value = reader.ReadByte();
                            last_push = value;

                            Console.WriteLine($"{addr:X8}| PUSHB {value:X2}h ({value})");
                            break;
                        }
                        // PUSHC
                        case 0x12:
                        {
                            Console.WriteLine($"{addr:X8}| PUSHC");
                            break;
                        }
                        // INVSIGN
                        case 0x13:
                        {
                            Console.WriteLine($"{addr:X8}| INVSIGN");
                            break;
                        }
                        // ADD
                        case 0x14:
                        {
                            Console.WriteLine($"{addr:X8}| ADD");
                            break;
                        }
                        // SUB
                        case 0x15:
                        {
                            Console.WriteLine($"{addr:X8}| SUB");
                            break;
                        }
                        // MUL
                        case 0x16:
                        {
                            Console.WriteLine($"{addr:X8}| MUL");
                            break;
                        }
                        // DIV
                        case 0x17:
                        {
                            Console.WriteLine($"{addr:X8}| DIV");
                            break;
                        }
                        // MOD
                        case 0x18:
                        {
                            Console.WriteLine($"{addr:X8}| MOD");
                            break;
                        }
                        // AND
                        case 0x19:
                        {
                            Console.WriteLine($"{addr:X8}| AND");
                            break;
                        }
                        // OR
                        case 0x1A:
                        {
                            Console.WriteLine($"{addr:X8}| OR");
                            break;
                        }
                        // XOR
                        case 0x1B:
                        {
                            Console.WriteLine($"{addr:X8}| XOR");
                            break;
                        }
                        // NOT
                        case 0x1C:
                        {
                            Console.WriteLine($"{addr:X8}| NOT");
                            break;
                        }
                        // BOOL
                        case 0x1D:
                        {
                            Console.WriteLine($"{addr:X8}| BOOL");
                            break;
                        }
                        // LAND
                        case 0x1E:
                        {
                            Console.WriteLine($"{addr:X8}| LAND");
                            break;
                        }
                        // LOR
                        case 0x1F:
                        {
                            Console.WriteLine($"{addr:X8}| LOR");
                            break;
                        }
                        // LNOT
                        case 0x20:
                        {
                            Console.WriteLine($"{addr:X8}| LNOT");
                            break;
                        }
                        // LT
                        case 0x21:
                        {
                            Console.WriteLine($"{addr:X8}| LT");
                            break;
                        }
                        // LE
                        case 0x22:
                        {
                            Console.WriteLine($"{addr:X8}| LE");
                            break;
                        }
                        // GT
                        case 0x23:
                        {
                            Console.WriteLine($"{addr:X8}| GT");
                            break;
                        }
                        // GE
                        case 0x24:
                        {
                            Console.WriteLine($"{addr:X8}| GE");
                            break;
                        }
                        // EQ
                        case 0x25:
                        {
                            Console.WriteLine($"{addr:X8}| EQ");
                            break;
                        }
                        // NE
                        case 0x26:
                        {
                            Console.WriteLine($"{addr:X8}| NE");
                            break;
                        }
                        // LSHIFT
                        case 0x27:
                        {
                            Console.WriteLine($"{addr:X8}| LSHIFT");
                            break;
                        }
                        // RSHIFT
                        case 0x28:
                        {
                            Console.WriteLine($"{addr:X8}| RSHIFT");
                            break;
                        }
                        // INC
                        case 0x29:
                        {
                            Console.WriteLine($"{addr:X8}| INC");
                            break;
                        }
                        // DEC
                        case 0x2A:
                        {
                            Console.WriteLine($"{addr:X8}| DEC");
                            break;
                        }
                        // ADDRESS
                        case 0x2B:
                        {
                            Console.WriteLine($"{addr:X8}| ADDRESS");
                            break;
                        }
                        // DUMP
                        case 0x2C:
                        {
                            Console.WriteLine($"{addr:X8}| DUMP");
                            break;
                        }
                        // CALL
                        case 0x2D:
                        {
                            int index = reader.ReadInt32();

                            if (index >= 0 && index < _functions.Count)
                            {
                                var func = _functions[index];

                                // If function has __cdecl call style, the next byte is parameter size ( in stack )
                                var symb = _symbols.Find(a => a.Name == func.Name);
                                // __cdecl
                                if (symb.field_C == 1)
                                {
                                    reader.ReadByte();
                                }

                                Console.WriteLine($"{addr:X8}| CALL {func.Name}");

                                // Output Message
                                if (func.Name == "_ms_message")
                                {
                                    var str = GetMessage(last_push);
                                    _message_strings.Add(new Tuple<uint, string>(last_push, str));
                                }
                                // Output Character Name
                                if (func.Name == "MALIE_NAME")
                                {
                                    _message_strings.Add(new Tuple<uint, string>(0xAAAAAAAA, last_string));
                                }
                            }
                            else
                            {
                                throw new Exception();
                            }

                            break;
                        }
                        // LOAD_FP
                        case 0x2E:
                        {
                            Console.WriteLine($"{addr:X8}| LOAD_FP");
                            break;
                        }
                        // STORE_FP
                        case 0x2F:
                        {
                            Console.WriteLine($"{addr:X8}| STORE_FP");
                            break;
                        }
                        // ADDRESS_FP
                        case 0x30:
                        {
                            Console.WriteLine($"{addr:X8}| ADDRESS_FP");
                            break;
                        }
                        // ENTER_FUNC
                        case 0x31:
                        {
                            uint local_size = reader.ReadUInt32();

                            Console.WriteLine($"{addr:X8}| ENTER_FUNC {local_size:X8}h");
                            break;
                        }
                        // LEAVE_FUNC
                        case 0x32:
                        {
                            Console.WriteLine($"{addr:X8}| LEAVE_FUNC");
                            break;
                        }
                        // LEAVE_FUNC_STD
                        case 0x33:
                        {
                            uint local_size = reader.ReadByte();

                            Console.WriteLine($"{addr:X8}| LEAVE_FUNC_STD {local_size:X8}h");
                            break;
                        }
                        // Unexplored
                        default:
                        {
                            Console.WriteLine($"{addr:X8}| {code:X2} ; unexplored");
                            break;
                        }
                    }
                }
            }
        }

        string GetMessage(uint index)
        {
            var entry = _msg_defines[Convert.ToInt32(index)];
            return Encoding.Unicode.GetString(_seg_message, entry.Offset, entry.Length);
        }

        public void ExportStrings(string filePath)
        {
            using (StreamWriter writer = File.CreateText(filePath))
            {
                foreach (var item in _message_strings)
                {
                    var idx = item.Item1;
                    var str = EscapeString(item.Item2);

                    writer.WriteLine($"◇{idx:X8}◇{str}");
                    writer.WriteLine($"◆{idx:X8}◆{str}");
                    writer.WriteLine();
                }
            }
        }


        static readonly Encoding _encoding = Encoding.GetEncoding("shift_jis");

        static string ReadString(BinaryReader reader)
        {
            uint length = reader.ReadUInt32();
            bool flag = false;

            if (length.HasFlag(0x80000000))
            {
                length &= 0x7FFFFFFF;
                flag = true;
            }

            var buffer = reader.ReadBytes(length);

            if (flag)
                return Encoding.Unicode.GetString(buffer, 0, (int)length - 2);
            else
                return _encoding.GetString(buffer, 0, (int)length - 1);
        }


        static string StringFromBuffer(byte[] buffer, int index)
        {
            int i = index;

            while (i + 2 < buffer.Length)
            {
                int c = buffer[i++];
                c |= buffer[i++] << 8;
                if (c == 0)
                    break;
            }

            int length = i - 2 - index;

            if (length <= 0)
                return string.Empty;

            return Encoding.Unicode.GetString(buffer, index, length);
        }

        static string StringFormatDisplay(string input)
        {
            input = input.Replace("\t", "\\t");
            input = input.Replace("\r", "\\r");
            input = input.Replace("\n", "\\n");

            return input;
        }

        static string EscapeString(string input)
        {
            // Voice
            input = Regex.Replace(input, @"\u0007\u0008(.+?)\u0000", "{$1}");
            // Ruby
            input = Regex.Replace(input, @"\u0007\u0001(.+?)\u000A(.+?)\u0000", "[$1]($2)");
            // \x07\x04
            input = Regex.Replace(input, @"\u0007\u0004", "[c]");
            // \x07\x06
            input = Regex.Replace(input, @"\u0007\u0006", "[z]");
            // \x07\x09
            input = Regex.Replace(input, @"\u0007\u0009", "[s]");
            // \x0A
            input = Regex.Replace(input, @"\u000A", "[n]");
            // \x0D
            input = Regex.Replace(input, @"\u000D", "[r]");

            return input;
        }

        class Symbol
        {
            public string Name;
            public int Type;
            public int field_8;
            public int field_14;
            public int field_C; // 1=='cdecl' 2=='stdcall'
        }

        class Function
        {
            public string Name;
            public int Index;
            public int field_4;
            public int Offset;
        }

        class Label
        {
            public string Name;
            public int Offset;
        }

        class MsgDefine
        {
            public int Offset;
            public int Length;
        }
    }

    class MessageParser
    {
        public MessageParser()
        {
        }

        public void Parse(string message)
        {
        }
    }
}
