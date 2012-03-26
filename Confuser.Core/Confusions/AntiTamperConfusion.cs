﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;
using Mono.Cecil;
using Mono.Cecil.Metadata;
using Mono.Cecil.Rocks;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using Mono.Cecil.Cil;
using Mono.Cecil.PE;

namespace Confuser.Core.Confusions
{
    public class AntiTamperConfusion : IConfusion
    {
        class Phase1 : StructurePhase
        {
            AntiTamperConfusion cion;
            public Phase1(AntiTamperConfusion cion) { this.cion = cion; }
            public override Priority Priority
            {
                get { return Priority.AssemblyLevel; }
            }
            public override IConfusion Confusion
            {
                get { return cion; }
            }
            public override int PhaseID
            {
                get { return 1; }
            }
            public override bool WholeRun
            {
                get { return true; }
            }

            public override void Initialize(ModuleDefinition mod)
            {
                this.mod = mod;
                cion.txts[mod] = new _Context();
                Random rand = new Random();
                byte[] dat = new byte[25];
                rand.NextBytes(dat);
                var txt = cion.txts[mod];
                txt.key0 = BitConverter.ToInt32(dat, 0);
                txt.key1 = BitConverter.ToInt64(dat, 4);
                txt.key2 = BitConverter.ToInt32(dat, 12);
                txt.key3 = BitConverter.ToInt32(dat, 16);
                txt.key4 = dat[24];
                txt.sectName = Convert.ToBase64String(MD5.Create().ComputeHash(dat)).Substring(0, 8);
                txt.fieldLayout = new byte[5];
                for (int i = 1; i <= 5; i++)
                {
                    int idx = rand.Next(0, 5);
                    while (txt.fieldLayout[idx] != 0) idx = rand.Next(0, 5);
                    txt.fieldLayout[idx] = (byte)i;
                }
                txt.bodies = new Dictionary<int, MethodBody>();
                txt.strings = new ByteBuffer();
                txt.finalDat = new ByteBuffer();
            }
            public override void DeInitialize()
            {
                //
            }

            ModuleDefinition mod;
            public override void Process(ConfusionParameter parameter)
            {
                var txt = cion.txts[mod];
                AssemblyDefinition i = AssemblyDefinition.ReadAssembly(typeof(Iid).Assembly.Location);
                txt.root = CecilHelper.Inject(mod, i.MainModule.GetType("AntiTamper"));
                mod.Types.Add(txt.root);
                MethodDefinition cctor = mod.GetType("<Module>").GetStaticConstructor();
                cctor.Body.GetILProcessor().InsertBefore(0, Instruction.Create(OpCodes.Call, txt.root.Methods.FirstOrDefault(mtd => mtd.Name == "Initialize")));

                MethodDefinition init = txt.root.Methods.FirstOrDefault(mtd => mtd.Name == "Initialize");
                foreach (Instruction inst in init.Body.Instructions)
                {
                    if (inst.Operand is int)
                    {
                        switch ((int)inst.Operand)
                        {
                            case 0x11111111:
                                inst.Operand = txt.key0; break;
                            case 0x33333333:
                                inst.Operand = txt.key2; break;
                            case 0x44444444:
                                inst.Operand = txt.key3; break;
                            case 0x55555555:
                                inst.Operand = (int)txt.sectName.ToCharArray().Sum(_ => (int)_); break;
                        }
                    }
                    else if (inst.Operand is long && (long)inst.Operand == 0x2222222222222222)
                        inst.Operand = txt.key1;
                }
                MethodDefinition dec = txt.root.Methods.FirstOrDefault(mtd => mtd.Name == "Decrypt");
                foreach (Instruction inst in dec.Body.Instructions)
                    if (inst.Operand is int && (int)inst.Operand == 0x11111111)
                        inst.Operand = (int)txt.key4;

                txt.root.Name = ObfuscationHelper.GetNewName("AntiTamperModule" + Guid.NewGuid().ToString());
                txt.root.Namespace = "";
                AddHelper(txt.root, HelperAttribute.NoInjection);
                foreach (MethodDefinition mtdDef in txt.root.Methods)
                {
                    if (mtdDef.IsConstructor) continue;
                    mtdDef.Name = ObfuscationHelper.GetNewName(mtdDef.Name + Guid.NewGuid().ToString());
                    AddHelper(mtdDef, HelperAttribute.NoInjection);
                }
                foreach (FieldDefinition fldDef in txt.root.Fields)
                {
                    fldDef.Name = ObfuscationHelper.GetNewName(fldDef.Name + Guid.NewGuid().ToString());
                    AddHelper(fldDef, HelperAttribute.NoInjection);
                }
                foreach (TypeDefinition nested in txt.root.NestedTypes)
                {
                    if (nested.Name == "MethodData")
                    {
                        FieldDefinition[] fields = nested.Fields.ToArray();
                        byte[] layout = txt.fieldLayout.Clone() as byte[];
                        Array.Sort(layout, fields);
                        for (byte j = 1; j <= 5; j++) layout[j - 1] = j;
                        Array.Sort(txt.fieldLayout, layout);
                        txt.fieldLayout = layout;
                        nested.Fields.Clear();
                        foreach (var f in fields)
                            nested.Fields.Add(f);
                    }

                    nested.Name = ObfuscationHelper.GetNewName(nested.Name + Guid.NewGuid().ToString());
                    AddHelper(nested, HelperAttribute.NoInjection);
                    foreach (MethodDefinition mtdDef in nested.Methods)
                    {
                        if (mtdDef.IsConstructor || mtdDef.IsRuntime) continue;
                        if (mtdDef.Name == "Obj2Ptr")
                        {
                            mtdDef.Body.Instructions.Clear();
                            mtdDef.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                            mtdDef.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                        }
                        mtdDef.Name = ObfuscationHelper.GetNewName(mtdDef.Name + Guid.NewGuid().ToString());
                        AddHelper(mtdDef, HelperAttribute.NoInjection);
                    }
                    foreach (FieldDefinition fldDef in nested.Fields)
                    {
                        fldDef.Name = ObfuscationHelper.GetNewName(fldDef.Name + Guid.NewGuid().ToString());
                        AddHelper(fldDef, HelperAttribute.NoInjection);
                    }
                }
            }
        }
        class Phase2 : StructurePhase, IProgressProvider
        {
            AntiTamperConfusion cion;
            public Phase2(AntiTamperConfusion cion) { this.cion = cion; }
            public override Priority Priority
            {
                get { return Priority.AssemblyLevel; }
            }
            public override IConfusion Confusion
            {
                get { return cion; }
            }
            public override int PhaseID
            {
                get { return 2; }
            }
            public override bool WholeRun
            {
                get { return false; }
            }

            public override void Initialize(ModuleDefinition mod)
            {
                this.mod = mod;

                var txt = cion.txts[mod];
                Queue<TypeDefinition> q = new Queue<TypeDefinition>();
                txt.excludes = new List<int>();
                txt.excludes.Add((int)mod.GetType("<Module>").GetStaticConstructor().MetadataToken.RID - 1);
                q.Enqueue(txt.root);
                while (q.Count != 0)
                {
                    TypeDefinition typeDef = q.Dequeue();
                    foreach (MethodDefinition mtd in typeDef.Methods)
                        txt.excludes.Add((int)mtd.MetadataToken.RID - 1);
                    foreach (TypeDefinition t in typeDef.NestedTypes)
                        q.Enqueue(t);
                }
            }
            public override void DeInitialize()
            {
                //
            }

            ModuleDefinition mod;
            public override void Process(ConfusionParameter parameter)
            {
                var txt = cion.txts[mod];
                List<MethodDefinition> methods = new List<MethodDefinition>();
                Queue<TypeDefinition> q = new Queue<TypeDefinition>();
                foreach (var i in mod.Types)
                    q.Enqueue(i);
                while (q.Count != 0)
                {
                    TypeDefinition typeDef = q.Dequeue();
                    foreach (var i in typeDef.NestedTypes)
                        q.Enqueue(i);
                    foreach (var i in typeDef.Methods)
                        if (!txt.excludes.Contains((int)i.MetadataToken.RID - 1))
                            methods.Add(i);
                }

                int total = methods.Count;
                int interval = 1;
                if (total > 1000)
                    interval = (int)total / 100;
                IEnumerator<MethodDefinition> etor = methods.GetEnumerator();
                etor.MoveNext();
                for (int i = 0; i < methods.Count; i++)
                {
                    txt.bodies.Add((int)methods[i].MetadataToken.RID - 1, methods[i].Body);

                    etor.MoveNext();
                    if (i % interval == 0 || i == methods.Count - 1)
                        progresser.SetProgress(i + 1, total);
                }
            }

            IProgresser progresser;
            public void SetProgresser(IProgresser progresser) { this.progresser = progresser; }
        }
        class Phase3 : MetadataPhase
        {
            AntiTamperConfusion cion;
            public Phase3(AntiTamperConfusion cion) { this.cion = cion; }
            public override Priority Priority
            {
                get { return Priority.MetadataLevel; }
            }
            public override IConfusion Confusion
            {
                get { return cion; }
            }
            public override int PhaseID
            {
                get { return 2; }
            }

            static void Crypt(byte[] buff, uint key)
            {
                uint k = key;
                for (uint i = 0; i < buff.Length; i++)
                {
                    byte o = buff[i];
                    buff[i] ^= (byte)(k&0xff);
                    k = (k * o + key) % 0xff;
                }
            }

            class MethodData
            {
                public int BufferOffset;
                public int Index;

                public uint MaxStack;
                public uint LocalVars;
                public uint Options;
                public byte[] ILCodes;
                public MethodEH[] EHs;
                public struct MethodEH
                {
                    public ExceptionHandlerType Flags;
                    public uint TryOffset;
                    public uint TryLength;
                    public uint HandlerOffset;
                    public uint HandlerLength;
                    public uint ClassTokenOrFilterOffset;
                    public void Serialize(BinaryWriter wtr)
                    {
                        wtr.Write((uint)Flags);
                        wtr.Write(TryOffset);
                        wtr.Write(TryLength);
                        wtr.Write(HandlerOffset);
                        wtr.Write(HandlerLength);
                        wtr.Write(ClassTokenOrFilterOffset);
                    }
                }

                public byte[] Serialize(byte[] fieldLayout)
                {
                    MemoryStream str = new MemoryStream();
                    BinaryWriter wtr = new BinaryWriter(str);
                    bool datLayout = false;
                    foreach (var i in fieldLayout)
                        switch (i)
                        {
                            case 1: wtr.Write((uint)ILCodes.Length); break;
                            case 2: wtr.Write((uint)MaxStack); break;
                            case 3: wtr.Write((uint)EHs.Length); break;
                            case 4: wtr.Write((uint)LocalVars); break;
                            case 5:
                                wtr.Write((uint)Options);
                                datLayout = (Options >> 8) == 0;
                                break;
                        }
                    if (datLayout)
                    {
                        wtr.Write(ILCodes);
                        foreach (var i in EHs)
                            i.Serialize(wtr);
                    }
                    else
                    {
                        foreach (var i in EHs)
                            i.Serialize(wtr);
                        wtr.Write(ILCodes);
                    }
                    return str.ToArray();
                }
            }
            class StringData
            {
                public int BufferOffset;
                public int Index;

                public int Offset;
                public string String;

                public byte[] Serialize()
                {
                    using (MemoryStream str = new MemoryStream())
                    {
                        BinaryWriter wtr = new BinaryWriter(str);
                        wtr.Write((uint)String.Length * 2 + 1);

                        byte special = 0;

                        for (int i = 0; i < String.Length; i++)
                        {
                            var @char = String[i];
                            wtr.Write((ushort)@char);

                            if (special == 1)
                                continue;

                            if (@char < 0x20 || @char > 0x7e)
                            {
                                if (@char > 0x7e
                                    || (@char >= 0x01 && @char <= 0x08)
                                    || (@char >= 0x0e && @char <= 0x1f)
                                    || @char == 0x27
                                    || @char == 0x2d)
                                {

                                    special = 1;
                                }
                            }
                        }

                        wtr.Write(special);
                        return str.ToArray();
                    }
                }
            }
            static Random rand = new Random();
            static void ExtractRefs(MethodBody body, int idx, List<object> objs)
            {
                foreach (var i in body.Instructions)
                {
                    if (i.OpCode.Code == Code.Ldstr)
                    {
                        objs.Add(new StringData()
                        {
                            Index = idx,
                            Offset = i.Offset + 1,
                            String = (string)i.Operand
                        });
                    }
                }
            }
            static MethodData Transform(MethodBody body, int idx, byte[] codes, Range range)
            {
                MethodData ret = new MethodData();
                ret.Index = idx;
                ret.EHs = Mono.Empty<MethodData.MethodEH>.Array;
                if ((codes[range.Start] & 0x3) == 0x2)
                {
                    ret.ILCodes = new byte[codes[range.Start] >> 2];
                    Buffer.BlockCopy(codes, (int)range.Start + 1, ret.ILCodes, 0, ret.ILCodes.Length);
                    ret.LocalVars = 0;
                    ret.MaxStack = (uint)8;
                    ret.Options = (uint)rand.Next(0, 2) << 8;
                }
                else
                {
                    ushort flags = BitConverter.ToUInt16(codes, (int)range.Start);
                    ret.ILCodes = new byte[BitConverter.ToInt32(codes, (int)range.Start + 4)];
                    Buffer.BlockCopy(codes, (int)range.Start + 12, ret.ILCodes, 0, ret.ILCodes.Length);
                    ret.LocalVars = BitConverter.ToUInt32(codes, (int)range.Start + 8);
                    ret.MaxStack = BitConverter.ToUInt16(codes, (int)range.Start + 2);
                    ret.Options = (flags & 0x10) != 0 ? 0x10 : 0U;
                    ret.Options |= (uint)rand.Next(0, 2) << 8;

                    if ((flags & 0x8) != 0)
                    {
                        int ptr = (int)range.Start + 12 + ret.ILCodes.Length;
                        var ehs = new List<MethodData.MethodEH>();
                        byte f;
                        do
                        {
                            ptr = (ptr + 3) & ~3;
                            f = codes[ptr];
                            uint count;
                            bool isSmall = (f & 0x40) == 0;
                            if (isSmall)
                                count = codes[ptr + 1] / 12u;
                            else
                                count = (BitConverter.ToUInt32(codes, ptr) >> 8) / 24;
                            ptr += 4;

                            for (int i = 0; i < count; i++)
                            {
                                var clause = new MethodData.MethodEH();
                                clause.Flags = (ExceptionHandlerType)(codes[ptr] & 0x7);
                                ptr += isSmall ? 2 : 4;

                                clause.TryOffset = isSmall ? BitConverter.ToUInt16(codes, ptr) : BitConverter.ToUInt32(codes, ptr);
                                ptr += isSmall ? 2 : 4;
                                clause.TryLength = isSmall ? codes[ptr] : BitConverter.ToUInt32(codes, ptr);
                                ptr += isSmall ? 1 : 4;

                                clause.HandlerOffset = isSmall ? BitConverter.ToUInt16(codes, ptr) : BitConverter.ToUInt32(codes, ptr);
                                ptr += isSmall ? 2 : 4;
                                clause.HandlerLength = isSmall ? codes[ptr] : BitConverter.ToUInt32(codes, ptr);
                                ptr += isSmall ? 1 : 4;

                                clause.ClassTokenOrFilterOffset = BitConverter.ToUInt32(codes, ptr);
                                ptr += 4;

                                if ((clause.ClassTokenOrFilterOffset & 0xff000000) == 0x1b000000)
                                    ret.Options |= 0x80;

                                ehs.Add(clause);
                            }
                        }
                        while ((f & 0x80) != 0);
                        ret.EHs = ehs.ToArray();
                    }
                }

                return ret;
            }
            public override void Process(NameValueCollection parameters, MetadataProcessor.MetadataAccessor accessor)
            {
                var txt = cion.txts[accessor.Module];
                MethodTable tbl = accessor.TableHeap.GetTable<MethodTable>(Table.Method);

                accessor.Codes.Position = 0;
                txt.codes = accessor.Codes.ReadBytes(accessor.Codes.Length);
                accessor.Codes.Reset(null);
                accessor.Codes.Position = 0;

                Random rand = new Random();
                uint bas = accessor.Codebase;
                List<object> o = new List<object>();
                for (int i = 0; i < tbl.Length; i++)
                {
                    if (tbl[i].Col1 == 0) continue;
                    if (txt.excludes.Contains(i))
                    {
                        tbl[i].Col1 = (uint)accessor.Codes.Position + bas;

                        Range range = accessor.BodyRanges[new MetadataToken(TokenType.Method, i + 1)];
                        byte[] buff = new byte[range.Length];
                        Buffer.BlockCopy(txt.codes, (int)(range.Start - bas), buff, 0, buff.Length);
                        accessor.Codes.WriteBytes(buff);
                        accessor.Codes.WriteBytes(((accessor.Codes.Position + 3) & ~3) - accessor.Codes.Position);
                    }
                    else
                    {
                        tbl[i].Col2 |= MethodImplAttributes.NoInlining;

                        Range range = accessor.BodyRanges[new MetadataToken(TokenType.Method, i + 1)];
                        range = new Range(range.Start - bas, range.Length);
                        ExtractRefs(txt.bodies[i], i, o);
                        var dat = Transform(txt.bodies[i], i, txt.codes, range);
                        o.Add(dat);
                    }
                }

                int[] randArray = new int[o.Count];
                for (int i = 0; i < o.Count; i++) randArray[i] = rand.Next();
                object[] objs = o.ToArray();
                Array.Sort(randArray, objs);

                var mtdDats = new Dictionary<int, MethodData>();
                foreach (var i in objs)
                {
                    if (i is MethodData)
                    {
                        MethodData mtdDat = i as MethodData;
                        mtdDats[mtdDat.Index] = mtdDat;
                        mtdDat.BufferOffset = txt.finalDat.Position;
                        txt.finalDat.WriteBytes(mtdDat.Serialize(txt.fieldLayout));
                    }
                    else
                    {
                        StringData strDat = i as StringData;
                        strDat.BufferOffset = txt.finalDat.Position;
                        txt.finalDat.WriteBytes(strDat.Serialize());
                    }
                }

                foreach (var i in objs)
                {
                    if (i is StringData)
                    {
                        StringData dat = i as StringData;
                        uint token = 0x70800000;
                        token |= (uint)dat.BufferOffset;
                        Buffer.BlockCopy(BitConverter.GetBytes(token), 0, mtdDats[dat.Index].ILCodes, dat.Offset, 4);
                    }
                }

                byte[] randBuff = new byte[4];
                foreach (var i in mtdDats)
                {
                    uint ptr = (uint)i.Value.BufferOffset;

                    rand.NextBytes(randBuff);
                    uint key = BitConverter.ToUInt32(randBuff, 0);
                    tbl[i.Key].Col1 = (uint)accessor.Codes.Position + bas;
                    byte[] buff = i.Value.Serialize(txt.fieldLayout);
                    Crypt(buff, key);
                    txt.finalDat.Position = i.Value.BufferOffset;
                    txt.finalDat.WriteBytes(buff);

                    accessor.Codes.WriteByte(0x46); //flags
                    accessor.Codes.WriteByte(0x21); //ldc.i8
                    accessor.Codes.WriteUInt64(((ulong)key << 32) | (ptr ^ key));
                    accessor.Codes.WriteByte(0x20); //ldc.i4
                    accessor.Codes.WriteUInt32(~(uint)buff.Length ^ key);
                    accessor.Codes.WriteByte(0x26);

                    accessor.BlobHeap.Position = (int)tbl[i.Key].Col5;
                    accessor.BlobHeap.ReadCompressedUInt32();
                    byte flags = accessor.BlobHeap.ReadByte();
                    if ((flags & 0x10) != 0) accessor.BlobHeap.ReadCompressedUInt32();
                    accessor.BlobHeap.ReadCompressedUInt32();
                    bool hasRet = false;
                    do
                    {
                        byte t = accessor.BlobHeap.ReadByte();
                        if (t == 0x1f || t == 0x20) continue;
                        hasRet = t != 0x01;
                    } while (false);

                    accessor.Codes.WriteByte(hasRet ? (byte)0x00 : (byte)0x26);
                    accessor.Codes.WriteByte(0x2a); //ret
                    accessor.Codes.WriteBytes(((accessor.Codes.Position + 3) & ~3) - accessor.Codes.Position);
                }

                accessor.USHeap.Reset(new byte[0]);
            }
        }
        class Phase4 : ImagePhase
        {
            AntiTamperConfusion cion;
            public Phase4(AntiTamperConfusion cion) { this.cion = cion; }
            public override Priority Priority
            {
                get { return Priority.MetadataLevel; }
            }
            public override IConfusion Confusion
            {
                get { return cion; }
            }
            public override int PhaseID
            {
                get { return 4; }
            }

            public override void Process(NameValueCollection parameters, MetadataProcessor.ImageAccessor accessor)
            {
                var txt = cion.txts[accessor.Module];
                uint size = (((uint)txt.finalDat.Length + 2 + 0x7f) & ~0x7fu) + 0x28;
                Section prev = accessor.Sections[accessor.Sections.Count - 1];
                Section sect = accessor.CreateSection(txt.sectName, size, 0x40000040, prev);
                accessor.Sections.Add(sect);
            }
        }
        class Phase5 : PePhase
        {
            AntiTamperConfusion cion;
            public Phase5(AntiTamperConfusion cion) { this.cion = cion; }
            public override Priority Priority
            {
                get { return Priority.PELevel; }
            }
            public override IConfusion Confusion
            {
                get { return cion; }
            }
            public override int PhaseID
            {
                get { return 5; }
            }

            void ExtractOffsets(_Context txt, Stream stream, out uint csOffset, out uint sn, out uint snLen)
            {
                BinaryReader rdr = new BinaryReader(stream);
                stream.Seek(0x3c, SeekOrigin.Begin);
                uint offset = rdr.ReadUInt32();
                stream.Seek(offset, SeekOrigin.Begin);
                stream.Seek(0x6, SeekOrigin.Current);
                uint sections = rdr.ReadUInt16();
                stream.Seek(offset = offset + 0x18, SeekOrigin.Begin);  //Optional hdr
                bool pe32 = (rdr.ReadUInt16() == 0x010b);
                csOffset = offset + 0x40;
                stream.Seek(offset = offset + (pe32 ? 0xE0U : 0xF0U), SeekOrigin.Begin);   //sections
                uint[] vAdrs = new uint[sections];
                uint[] vSizes = new uint[sections];
                uint[] dAdrs = new uint[sections];
                for (int i = 0; i < sections; i++)
                {
                    string name = Encoding.ASCII.GetString(rdr.ReadBytes(8)).Trim('\0');
                    uint vSize = vSizes[i] = rdr.ReadUInt32();
                    uint vAdr = vAdrs[i] = rdr.ReadUInt32();
                    uint dSize = rdr.ReadUInt32();
                    uint dAdr = dAdrs[i] = rdr.ReadUInt32();
                    stream.Seek(0x10, SeekOrigin.Current);
                }

                stream.Seek(offset - 16, SeekOrigin.Begin);
                uint mdDir = rdr.ReadUInt32();
                for (int i = 0; i < sections; i++)
                    if (mdDir > vAdrs[i] && mdDir < vAdrs[i] + vSizes[i])
                    {
                        mdDir = mdDir - vAdrs[i] + dAdrs[i];
                        break;
                    }
                stream.Seek(mdDir + 0x20, SeekOrigin.Begin);
                sn = rdr.ReadUInt32();
                for (int i = 0; i < sections; i++)
                    if (sn > vAdrs[i] && sn < vAdrs[i] + vSizes[i])
                    {
                        sn = sn - vAdrs[i] + dAdrs[i];
                        break;
                    }
                snLen = rdr.ReadUInt32();
            }
            static byte[] Encrypt(byte[] buff, byte[] dat, out byte[] iv, byte key)
            {
                dat = (byte[])dat.Clone();
                SHA512 sha = SHA512.Create();
                byte[] c = sha.ComputeHash(buff);
                for (int i = 0; i < dat.Length; i += 64)
                {
                    byte[] o = new byte[64];
                    int len = dat.Length <= i + 64 ? dat.Length : i + 64;
                    Buffer.BlockCopy(dat, i, o, 0, len - i);
                    for (int j = i; j < len; j++)
                        dat[j] ^= (byte)(c[j - i] ^ key);
                    c = sha.ComputeHash(o);
                }

                Rijndael ri = Rijndael.Create();
                ri.GenerateIV(); iv = ri.IV;
                MemoryStream ret = new MemoryStream();
                using (CryptoStream cStr = new CryptoStream(ret, ri.CreateEncryptor(SHA256.Create().ComputeHash(buff), iv), CryptoStreamMode.Write))
                    cStr.Write(dat, 0, dat.Length);
                return ret.ToArray();
            }
            static byte[] Decrypt(byte[] buff, byte[] iv, byte[] dat, byte key)
            {
                Rijndael ri = Rijndael.Create();
                byte[] ret = new byte[dat.Length];
                MemoryStream ms = new MemoryStream(dat);
                using (CryptoStream cStr = new CryptoStream(ms, ri.CreateDecryptor(SHA256.Create().ComputeHash(buff), iv), CryptoStreamMode.Read))
                { cStr.Read(ret, 0, dat.Length); }

                SHA512 sha = SHA512.Create();
                byte[] c = sha.ComputeHash(buff);
                for (int i = 0; i < ret.Length; i += 64)
                {
                    int len = ret.Length <= i + 64 ? ret.Length : i + 64;
                    for (int j = i; j < len; j++)
                        ret[j] ^= (byte)(c[j - i] ^ key);
                    c = sha.ComputeHash(ret, i, len - i);
                }
                return ret;
            }
            public override void Process(NameValueCollection parameters, Stream stream, ModuleDefinition mod)
            {
                var txt = cion.txts[mod];
                stream.Seek(0, SeekOrigin.Begin);
                uint csOffset;
                uint sn;
                uint snLen;
                ExtractOffsets(txt, stream, out csOffset, out sn, out snLen);
                stream.Position = 0;
                Image img = ImageReader.ReadImageFrom(stream);

                MemoryStream ms = new MemoryStream();
                ms.WriteByte(0xd6);
                ms.WriteByte(0x6f);
                BinaryWriter wtr = new BinaryWriter(ms);
                wtr.Write(txt.finalDat.GetBuffer(), 0, txt.finalDat.Length);

                byte[] buff;
                BinaryReader sReader = new BinaryReader(stream);
                using (MemoryStream str = new MemoryStream())
                {
                    stream.Position = img.ResolveVirtualAddress(img.Metadata.VirtualAddress) + 12;
                    stream.Position += sReader.ReadUInt32() + 4;
                    stream.Position += 2;

                    ushort streams = sReader.ReadUInt16();

                    for (int i = 0; i < streams; i++)
                    {
                        uint offset = img.ResolveVirtualAddress(img.Metadata.VirtualAddress + sReader.ReadUInt32());
                        uint size = sReader.ReadUInt32();

                        int c = 0;
                        while (sReader.ReadByte() != 0) c++;
                        long ori = stream.Position += (((c + 1) + 3) & ~3) - (c + 1);

                        stream.Position = offset;
                        str.Write(sReader.ReadBytes((int)size), 0, (int)size);
                        stream.Position = ori;
                    }

                    buff = str.ToArray();
                }

                byte[] iv;
                byte[] dat = Encrypt(buff, ms.ToArray(), out iv, txt.key4);

                byte[] md5 = MD5.Create().ComputeHash(buff);
                long checkSum = BitConverter.ToInt64(md5, 0) ^ BitConverter.ToInt64(md5, 8);
                wtr = new BinaryWriter(stream);
                stream.Seek(csOffset, SeekOrigin.Begin);
                wtr.Write(img.Metadata.VirtualAddress ^ (uint)txt.key0);
                stream.Seek(img.GetSection(txt.sectName).PointerToRawData, SeekOrigin.Begin);
                wtr.Write(checkSum ^ txt.key1);
                wtr.Write(sn);
                wtr.Write(snLen);
                wtr.Write(iv.Length ^ txt.key2);
                wtr.Write(iv);
                wtr.Write(dat.Length ^ txt.key3);
                wtr.Write(dat);
            }
        }

        class _Context
        {
            public TypeDefinition root;
            public int key0;
            public long key1;
            public int key2;
            public int key3;
            public byte key4;
            public byte[] fieldLayout;
            public string sectName;
            public List<int> excludes;

            public byte[] codes;
            public ByteBuffer strings;
            public Dictionary<int, MethodBody> bodies;
            public ByteBuffer finalDat;
        }
        Dictionary<ModuleDefinition, _Context> txts = new Dictionary<ModuleDefinition, _Context>();

        public string Name
        {
            get { return "Anti Tampering Confusion"; }
        }
        public string Description
        {
            get { return "This confusion provides a better protection than strong name for maintain integration."; }
        }
        public string ID
        {
            get { return "anti tamper"; }
        }
        public bool StandardCompatible
        {
            get { return false; }
        }
        public Target Target
        {
            get { return Target.Module; }
        }
        public Preset Preset
        {
            get { return Preset.Maximum; }
        }
        public bool SupportLateAddition
        {
            get { return true; }
        }
        public Behaviour Behaviour
        {
            get { return Behaviour.Inject | Behaviour.AlterCode | Behaviour.Encrypt; }
        }

        Phase[] phases;
        public Phase[] Phases
        {
            get { if (phases == null)phases = new Phase[] { new Phase1(this), new Phase2(this), new Phase3(this), new Phase4(this), new Phase5(this) }; return phases; }
        }

        public void Init() { txts.Clear(); }
        public void Deinit() { txts.Clear(); }
    }
}