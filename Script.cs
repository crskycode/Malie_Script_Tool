using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Malie_Script_Tool
{
    using TDep = Tuple<int, int>;
    using TStr = Tuple<uint, string>;
    using TStrRefDict = Dictionary<uint, uint>;
    using TStrDict = Dictionary<uint, string>;

    class Script
    {
        public Script()
        {
        }

        public void Load(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            using (var reader = new BinaryReader(stream))
            {
                Read(reader);
                //Dump();
                Parse();
            }
        }


        readonly List<Symbol> _symbols = new List<Symbol>();
        readonly List<Function> _functions = new List<Function>();
        readonly List<Label> _labels = new List<Label>();

        byte[] _seg_string;
        byte[] _seg_code;

        readonly List<MsgEntry> _msg_entries = new List<MsgEntry>();

        byte[] _seg_message;
        int _unk_dword;

        readonly TStrRefDict _string_refs = new TStrRefDict();
        // Scenario Ordered Messages
        readonly List<TStr> _messages = new List<TStr>();


        void Clear()
        {
            _symbols.Clear();
            _functions.Clear();
            _labels.Clear();
            _seg_string = null;
            _seg_code = null;
            _msg_entries.Clear();
            _seg_message = null;
            _unk_dword = 0;
        }

        void Read(BinaryReader reader)
        {
            Clear();

            void ReadSymbols()
            {
                int count = reader.ReadInt32();

                for (int i = 0; i < count; i++)
                {
                    var entry = new Symbol();

                    entry.Name = ReadString(reader);

                    entry.Dep = new List<TDep>();
                    while (true)
                    {
                        var v1 = reader.ReadInt32();
                        if (v1 == 0)
                            break;
                        var v2 = reader.ReadInt32();

                        entry.Dep.Add(new TDep(v1, v2));
                    }

                    entry.Type = reader.ReadInt32(); // type 0:func?  1:?  2:?  3:var  4:func
                    entry.field_8 = reader.ReadInt32();
                    entry.field_14 = reader.ReadInt32(); // offset or order
                    entry.field_C = reader.ReadInt32();

                    _symbols.Add(entry);
                }

                _unk_dword = reader.ReadInt32();
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

            void ReadStringSegment()
            {
                int size = reader.ReadInt32();
                _seg_string = reader.ReadBytes(size);
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
                    var entry = new MsgEntry();

                    entry.Offset = reader.ReadInt32();
                    entry.Length = reader.ReadInt32();

                    _msg_entries.Add(entry);
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

            ReadStringSegment();

            ReadCodeSegment();

            ReadMsgDefines();

            ReadMsgSegment();


            Debug.Assert(reader.BaseStream.Position == reader.BaseStream.Length);
        }

        void Dump()
        {
            foreach (var item in _symbols)
            {
                Debug.WriteLine($"{"SYMBOL",-12} {item.Name,-32} {item.Type:X8} {item.field_8:X8} {item.field_14:X8} {item.field_C:X8}");
            }

            foreach (var item in _functions)
            {
                Debug.WriteLine($"{"FUNCTION",-12} {item.Name,-32} {item.Index:X8} {item.field_4:X8} {item.Offset:X8}");
            }
        }

        void Parse()
        {
            // Clear up before parsing
            _string_refs.Clear();
            _messages.Clear();

            using (var stream = new MemoryStream(_seg_code))
            using (var reader = new BinaryReader(stream))
            {
                void OutputDiasm(string output)
                {
                }

                // Hack
                uint last_push = 0;
                string last_string = string.Empty;

                while (stream.Position < stream.Length)
                {
                    uint addr = Convert.ToUInt32(stream.Position);
                    byte code = reader.ReadByte();

                    switch (code)
                    {
                        // JUMP
                        case 0x00:
                        {
                            var offset = reader.ReadUInt32();

                            var label = _labels.Find(a => a.Offset == offset);

                            if (label != null)
                                OutputDiasm($"{addr:X8}| JUMP {label.Name}");
                            else if (offset < _seg_code.Length)
                                OutputDiasm($"{addr:X8}| JUMP {offset:X8}h");
                            else
                                OutputDiasm($"{addr:X8}| JUMP {offset:X8}h ; WTF");

                            break;
                        }
                        // TJUMP
                        case 0x01:
                        {
                            uint offset = reader.ReadUInt32();

                            var label = _labels.Find(a => a.Offset == offset);

                            if (label != null)
                                OutputDiasm($"{addr:X8}| TJUMP {label.Name}");
                            else if (offset < _seg_code.Length)
                                OutputDiasm($"{addr:X8}| TJUMP {offset:X8}h");
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
                                OutputDiasm($"{addr:X8}| FJUMP {label.Name}");
                            else if (offset < _seg_code.Length)
                                OutputDiasm($"{addr:X8}| FJUMP {offset:X8}h");
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
                                OutputDiasm($"{addr:X8}| CALL_C {_functions[index].Name} ({argc})");
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
                                OutputDiasm($"{addr:X8}| CALL_CB {_functions[index].Name} ({argc})");
                            else
                                throw new Exception();

                            break;
                        }
                        // STOP
                        case 0x05:
                        {
                            OutputDiasm($"{addr:X8}| STOP");
                            break;
                        }
                        // LOAD
                        case 0x06:
                        {
                            OutputDiasm($"{addr:X8}| LOAD");
                            break;
                        }
                        // STORE
                        case 0x07:
                        {
                            OutputDiasm($"{addr:X8}| STORE");
                            break;
                        }
                        // LOADCONST
                        case 0x08:
                        {
                            uint value = reader.ReadUInt32();

                            OutputDiasm($"{addr:X8}| LOADCONST {value:X8}h ({value})");
                            break;
                        }
                        // LOADSTRING1
                        case 0x09:
                        {
                            uint offset = reader.ReadByte();

                            if (offset < _seg_string.Length)
                            {
                                _string_refs[addr] = offset;

                                var str = StringFromBuffer(_seg_string, offset);
                                last_string = str;
                                str = EscapeString(str);

                                OutputDiasm($"{addr:X8}| LOADSTRING1 \"{str}\"");
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
                            uint offset = reader.ReadUInt16();

                            if (offset < _seg_string.Length)
                            {
                                _string_refs[addr] = offset;

                                var str = StringFromBuffer(_seg_string, offset);
                                last_string = str;
                                str = EscapeString(str);

                                OutputDiasm($"{addr:X8}| LOADSTRING2 \"{str}\"");
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

                            if (offset < _seg_string.Length)
                            {
                                _string_refs[addr] = offset;

                                var str = StringFromBuffer(_seg_string, offset);
                                last_string = str;
                                str = EscapeString(str);

                                OutputDiasm($"{addr:X8}| LOADSTRING4 \"{str}\"");
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

                            OutputDiasm($"{addr:X8}| PUSH {value:X8}h ({value})");
                            break;
                        }
                        // POP
                        case 0x0E:
                        {
                            OutputDiasm($"{addr:X8}| POP");
                            break;
                        }
                        // PUSHZ
                        case 0x0F:
                        {
                            last_push = 0;

                            OutputDiasm($"{addr:X8}| PUSHZ");
                            break;
                        }
                        // PUSHB
                        case 0x11:
                        {
                            uint value = reader.ReadByte();
                            last_push = value;

                            OutputDiasm($"{addr:X8}| PUSHB {value:X2}h ({value})");
                            break;
                        }
                        // PUSHC
                        case 0x12:
                        {
                            OutputDiasm($"{addr:X8}| PUSHC");
                            break;
                        }
                        // INVSIGN
                        case 0x13:
                        {
                            OutputDiasm($"{addr:X8}| INVSIGN");
                            break;
                        }
                        // ADD
                        case 0x14:
                        {
                            OutputDiasm($"{addr:X8}| ADD");
                            break;
                        }
                        // SUB
                        case 0x15:
                        {
                            OutputDiasm($"{addr:X8}| SUB");
                            break;
                        }
                        // MUL
                        case 0x16:
                        {
                            OutputDiasm($"{addr:X8}| MUL");
                            break;
                        }
                        // DIV
                        case 0x17:
                        {
                            OutputDiasm($"{addr:X8}| DIV");
                            break;
                        }
                        // MOD
                        case 0x18:
                        {
                            OutputDiasm($"{addr:X8}| MOD");
                            break;
                        }
                        // AND
                        case 0x19:
                        {
                            OutputDiasm($"{addr:X8}| AND");
                            break;
                        }
                        // OR
                        case 0x1A:
                        {
                            OutputDiasm($"{addr:X8}| OR");
                            break;
                        }
                        // XOR
                        case 0x1B:
                        {
                            OutputDiasm($"{addr:X8}| XOR");
                            break;
                        }
                        // NOT
                        case 0x1C:
                        {
                            OutputDiasm($"{addr:X8}| NOT");
                            break;
                        }
                        // BOOL
                        case 0x1D:
                        {
                            OutputDiasm($"{addr:X8}| BOOL");
                            break;
                        }
                        // LAND
                        case 0x1E:
                        {
                            OutputDiasm($"{addr:X8}| LAND");
                            break;
                        }
                        // LOR
                        case 0x1F:
                        {
                            OutputDiasm($"{addr:X8}| LOR");
                            break;
                        }
                        // LNOT
                        case 0x20:
                        {
                            OutputDiasm($"{addr:X8}| LNOT");
                            break;
                        }
                        // LT
                        case 0x21:
                        {
                            OutputDiasm($"{addr:X8}| LT");
                            break;
                        }
                        // LE
                        case 0x22:
                        {
                            OutputDiasm($"{addr:X8}| LE");
                            break;
                        }
                        // GT
                        case 0x23:
                        {
                            OutputDiasm($"{addr:X8}| GT");
                            break;
                        }
                        // GE
                        case 0x24:
                        {
                            OutputDiasm($"{addr:X8}| GE");
                            break;
                        }
                        // EQ
                        case 0x25:
                        {
                            OutputDiasm($"{addr:X8}| EQ");
                            break;
                        }
                        // NE
                        case 0x26:
                        {
                            OutputDiasm($"{addr:X8}| NE");
                            break;
                        }
                        // LSHIFT
                        case 0x27:
                        {
                            OutputDiasm($"{addr:X8}| LSHIFT");
                            break;
                        }
                        // RSHIFT
                        case 0x28:
                        {
                            OutputDiasm($"{addr:X8}| RSHIFT");
                            break;
                        }
                        // INC
                        case 0x29:
                        {
                            OutputDiasm($"{addr:X8}| INC");
                            break;
                        }
                        // DEC
                        case 0x2A:
                        {
                            OutputDiasm($"{addr:X8}| DEC");
                            break;
                        }
                        // ADDRESS
                        case 0x2B:
                        {
                            OutputDiasm($"{addr:X8}| ADDRESS");
                            break;
                        }
                        // DUMP
                        case 0x2C:
                        {
                            OutputDiasm($"{addr:X8}| DUMP");
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

                                OutputDiasm($"{addr:X8}| CALL {func.Name}");

                                // Output Message
                                if (func.Name == "_ms_message")
                                {
                                    var str = GetMessage(last_push);
                                    _messages.Add(new TStr(last_push, str));
                                }
                                // Output Character Name
                                if (func.Name == "MALIE_NAME")
                                {
                                    _messages.Add(new TStr(0xAAAAAAAA, last_string));
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
                            OutputDiasm($"{addr:X8}| LOAD_FP");
                            break;
                        }
                        // STORE_FP
                        case 0x2F:
                        {
                            OutputDiasm($"{addr:X8}| STORE_FP");
                            break;
                        }
                        // ADDRESS_FP
                        case 0x30:
                        {
                            OutputDiasm($"{addr:X8}| ADDRESS_FP");
                            break;
                        }
                        // ENTER_FUNC
                        case 0x31:
                        {
                            uint local_size = reader.ReadUInt32();

                            OutputDiasm($"{addr:X8}| ENTER_FUNC {local_size:X8}h");
                            break;
                        }
                        // LEAVE_FUNC
                        case 0x32:
                        {
                            OutputDiasm($"{addr:X8}| LEAVE_FUNC");
                            break;
                        }
                        // LEAVE_FUNC_STD
                        case 0x33:
                        {
                            uint local_size = reader.ReadByte();

                            OutputDiasm($"{addr:X8}| LEAVE_FUNC_STD {local_size:X8}h");
                            break;
                        }
                        // Unexplored
                        default:
                        {
                            OutputDiasm($"{addr:X8}| {code:X2} ; unexplored");
                            break;
                        }
                    }
                }
            }
        }

        public void Save(string filePath)
        {
            using (var stream = File.Create(filePath))
            using (var writer = new BinaryWriter(stream))
            {
                void WriteSymbols()
                {
                    writer.Write(_symbols.Count);

                    foreach (var item in _symbols)
                    {
                        WriteString(writer, item.Name);

                        foreach (var d in item.Dep)
                        {
                            writer.Write(d.Item1);
                            writer.Write(d.Item2);
                        }
                        writer.Write(0);

                        writer.Write(item.Type);
                        writer.Write(item.field_8);
                        writer.Write(item.field_14);
                        writer.Write(item.field_C);
                    }

                    writer.Write(_unk_dword);
                }

                void WriteFunctions()
                {
                    writer.Write(_functions.Count);

                    foreach (var item in _functions)
                    {
                        WriteString(writer, item.Name);
                        writer.Write(item.Index);
                        writer.Write(item.field_4);
                        writer.Write(item.Offset);
                    }
                }

                void WriteLabels()
                {
                    writer.Write(_labels.Count);

                    foreach (var item in _labels)
                    {
                        WriteString(writer, item.Name);
                        writer.Write(item.Offset);
                    }
                }

                void WriteStringSegment()
                {
                    writer.Write(_seg_string.Length);
                    writer.Write(_seg_string);
                }

                void WriteCodeSegment()
                {
                    writer.Write(_seg_code.Length);
                    writer.Write(_seg_code);
                }

                void WriteMsgDefines()
                {
                    writer.Write(_msg_entries.Count);

                    foreach (var item in _msg_entries)
                    {
                        writer.Write(item.Offset);
                        writer.Write(item.Length);
                    }
                }
                
                void WriteMsgSegment()
                {
                    writer.Write(_seg_message.Length);
                    writer.Write(_seg_message);
                }

                WriteSymbols();

                WriteFunctions();

                WriteLabels();

                WriteStringSegment();

                WriteCodeSegment();

                WriteMsgDefines();

                WriteMsgSegment();

                writer.Flush();
            }
        }

        string GetMessage(uint index)
        {
            var entry = _msg_entries[Convert.ToInt32(index)];
            return Encoding.Unicode.GetString(_seg_message, entry.Offset, entry.Length);
        }

        public void ExportMessages(string filePath)
        {
            using (var writer = File.CreateText(filePath))
            {
                foreach (var item in _messages)
                {
                    var idx = item.Item1;
                    var str = EscapeMessage(item.Item2);

                    writer.WriteLine($"◇{idx:X8}◇{str}");
                    writer.WriteLine($"◆{idx:X8}◆{str}");
                    writer.WriteLine();
                }
            }
        }

        public void ImportMessages(string filePath)
        {
            var msgMap = new string[_msg_entries.Count];

            // Read translated messages
            using (var reader = File.OpenText(filePath))
            {
                int lineNo = 0;

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var ln = lineNo++;

                    // Ignore empty line
                    if (line.Length == 0 || line[0] != '◆')
                        continue;

                    // Parse line
                    var m = Regex.Match(line, @"◆(\w+)◆(.*$)");

                    // Check match
                    if (!m.Success || m.Groups.Count != 3)
                        throw new Exception($"Bad format at line: {ln}");

                    // Parse index
                    var index = uint.Parse(m.Groups[1].Value, NumberStyles.HexNumber);

                    // Ignore character name...
                    if (index == 0xAAAAAAAA)
                        continue;

                    // Check index
                    if (index >= msgMap.Length)
                        throw new Exception($"Index {index:X8} not contained in script. line: {ln}");

                    // Parse message
                    var msg = UnescapeMessage(m.Groups[2].Value);

                    // Update message
                    msgMap[index] = msg;
                }
            }

            // Build message segment

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                for (int i = 0; i < _msg_entries.Count; i++)
                {
                    var entry = _msg_entries[i];

                    var bytes = Encoding.Unicode.GetBytes(msgMap[i]);

                    // Update message define
                    entry.Offset = Convert.ToInt32(stream.Position);
                    entry.Length = bytes.Length;

                    // Write message
                    writer.Write(bytes);
                }

                writer.Flush();

                _seg_message = stream.ToArray();
            }
        }

        TStrDict GetStrings()
        {
            // Find all string offset

            var set = new HashSet<uint>();
            
            foreach (var item in _string_refs)
                set.Add(item.Value);

            // Create string map
            
            var map = new TStrDict();
            
            foreach (var item in set)
            {
                if (!map.ContainsKey(item))
                {
                    var str = StringFromBuffer(_seg_string, item);
                    map.Add(item, str);
                }
            }

            return map;
        }

        public void ExportStrings(string filePath)
        {
            var strings = GetStrings();

            using (var writer = File.CreateText(filePath))
            {
                foreach (var item in strings)
                {
                    writer.WriteLine($"◇{item.Key:X8}◇{EscapeString(item.Value)}");
                }
            }
        }

        public void ImportStrings(string filePath)
        {
            var strings = GetStrings();

            // Read translated strings
            using (var reader = File.OpenText(filePath))
            {
                int lineNo = 0;

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var ln = lineNo++;

                    // Ignore empty line
                    if (line.Length == 0 || line[0] != '◇')
                        continue;

                    // Parse line
                    var m = Regex.Match(line, @"◇(\w+)◇(.*$)");

                    // Check match
                    if (!m.Success || m.Groups.Count < 2)
                        throw new Exception($"Bad format at line: {ln}");

                    // Parse offset
                    var offset = uint.Parse(m.Groups[1].Value, NumberStyles.HexNumber);

                    // Check offset
                    if (!strings.ContainsKey(offset))
                        throw new Exception($"String offset {offset:X8} not contained in script. line: {ln}");

                    // Parse string
                    var str = string.Empty;
                    // Maybe a empty string...
                    if (m.Groups.Count >= 3)
                        str = UnescapeString(m.Groups[2].Value);

                    // Update string
                    strings[offset] = str;
                }
            }

            // Group by offset range
            var offset_ranges = new uint[] { byte.MaxValue, ushort.MaxValue, uint.MaxValue };
            var grouping = strings.GroupBy(a => offset_ranges.First(m => m >= a.Key));

            // Build string segment

            var offset_map = new Dictionary<uint, uint>(); // old: new

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                foreach (var g in grouping)
                {
                    var q = g.OrderBy(a => a.Value.Length);

                    foreach (var i in q)
                    {
                        // Store new offset
                        offset_map[i.Key] = Convert.ToUInt32(stream.Position);

                        if (!string.IsNullOrEmpty(i.Value))
                        {
                            // Write string
                            var bytes = Encoding.Unicode.GetBytes(i.Value);
                            writer.Write(bytes);
                        }

                        // Null terminated
                        writer.Write((short)0);
                    }
                }

                writer.Flush();

                _seg_string = stream.ToArray();
            }

            // Update code segment

            foreach (var item in _string_refs)
            {
                var old_offset = item.Value;
                var new_offset = offset_map[old_offset];
                var size = GetOffsetSize(old_offset);

                // Check offset size
                if (GetOffsetSize(new_offset) > size)
                    throw new Exception("New string offset too big.");

                // Get offset bytes

                byte[] bytes;

                if (size == 1)
                    bytes = new byte[] { (byte)new_offset };
                else if (size == 2)
                    bytes = BitConverter.GetBytes((ushort)new_offset);
                else
                    bytes = BitConverter.GetBytes(new_offset);

                // Write offset
                Buffer.BlockCopy(bytes, 0, _seg_code, (int)(item.Key + 1), bytes.Length);
            }
        }

        static int GetOffsetSize(uint offset)
        {
            if (offset <= byte.MaxValue)
                return 1;
            if (offset <= ushort.MaxValue)
                return 2;
            return 4;
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

        static void WriteString(BinaryWriter writer, string value)
        {
            var bytes = Encoding.Unicode.GetBytes(value);
            var length = (uint)(bytes.Length + 2);
            length |= 0x80000000; // Unicode
            writer.Write(length);
            writer.Write(bytes);
            writer.Write((short)0); // Null terminated
        }


        static string StringFromBuffer(byte[] buffer, uint index)
        {
            int i = Convert.ToInt32(index);
            int j = i;

            while (j + 2 < buffer.Length)
            {
                int c = buffer[j++];
                c |= buffer[j++] << 8;
                if (c == 0)
                    break;
            }

            int length = j - 2 - i;

            if (length <= 0)
                return string.Empty;

            return Encoding.Unicode.GetString(buffer, i, length);
        }

        static string EscapeString(string input)
        {
            input = input.Replace("\t", "\\t");
            input = input.Replace("\r", "\\r");
            input = input.Replace("\n", "\\n");

            return input;
        }

        static string UnescapeString(string input)
        {
            input = input.Replace("\\t", "\t");
            input = input.Replace("\\r", "\r");
            input = input.Replace("\\n", "\n");

            return input;
        }

        static string EscapeMessage(string input)
        {
            // Voice
            input = Regex.Replace(input, @"\u0007\u0008([^\u0007\u0008]+?)\u0000", "{$1}");
            // Ruby
            input = Regex.Replace(input, @"\u0007\u0001([^\u0007\u0001]+?)\u000A([^\u000A]+?)\u0000", "[$1]($2)");
            // \x07\x04
            input = input.Replace("\u0007\u0004", "[c]");
            // \x07\x06
            input = input.Replace("\u0007\u0006", "[z]");
            // \x07\x09
            input = input.Replace("\u0007\u0009", "[s]");
            // \x0A
            input = input.Replace("\u000A", "[n]");
            // \x0D
            input = input.Replace("\u000D", "[r]");

            return input;
        }

        static string UnescapeMessage(string input)
        {
            // Voice
            input = Regex.Replace(input, @"\{([^\{\}]+?)\}", "\u0007\u0008$1\u0000");
            // Ruby
            input = Regex.Replace(input, @"\[([^\[\]]+?)\]\(([^\(\)]+?)\)", "\u0007\u0001$1\u000A$2\u0000");
            // \x07\x04
            input = input.Replace("[c]", "\u0007\u0004");
            // \x07\x06
            input = input.Replace("[z]", "\u0007\u0006");
            // \x07\x09
            input = input.Replace("[s]", "\u0007\u0009");
            // \x0A
            input = input.Replace("[n]", "\u000A");
            // \x0D
            input = input.Replace("[r]", "\u000D");

            return input;
        }

        class Symbol
        {
            public string Name;
            public int Type;
            public int field_8;
            public int field_14;
            public int field_C; // 1=='cdecl' 2=='stdcall'
            public List<TDep> Dep;
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

        class MsgEntry
        {
            public int Offset;
            public int Length;
        }
    }
}
