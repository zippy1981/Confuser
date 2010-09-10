﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Confuser.Core.Poly;
using Confuser.Core.Poly.Visitors;

namespace Confuser.Core.Confusions
{
    public class DisConstConfusion : StructurePhase, IConfusion, IProgressProvider
    {
        public string Name
        {
            get { return "Constant Disintegration Confusion"; }
        }
        public string Description
        {
            get
            {
                return @"This confusion disintegrate the constants in the code into expression.
***This confusion could affect the performance if your application uses constant frequently***";
            }
        }
        public string ID
        {
            get { return "disintegrate const"; }
        }
        public bool StandardCompatible
        {
            get { return true; }
        }
        public Target Target
        {
            get { return Target.Methods; }
        }
        public Preset Preset
        {
            get { return Preset.Aggressive; }
        }
        public Phase[] Phases
        {
            get { return new Phase[] { this }; }
        }

        public override Priority Priority
        {
            get { return Priority.CodeLevel; }
        }
        public override IConfusion Confusion
        {
            get { return this; }
        }
        public override int PhaseID
        {
            get { return 3; }
        }
        public override bool WholeRun
        {
            get { return false; }
        }
        public override void Initialize(AssemblyDefinition asm)
        {
            //
        }
        public override void DeInitialize()
        {
            //
        }

        public override void Process(ConfusionParameter parameter)
        {

            List<Context> txts = new List<Context>();
            List<Object> targets = parameter.Target as List<Object>;
            for (int i = 0; i < targets.Count; i++)
            {
                MethodDefinition mtd = targets[i] as MethodDefinition;
                if (!mtd.HasBody) continue;
                mtd.Body.SimplifyMacros();
                int lv = 5;
                if (Array.IndexOf(parameter.Parameters.AllKeys, mtd.GetHashCode().ToString("X8") + "_level") != -1)
                {
                    if (!int.TryParse(parameter.Parameters[mtd.GetHashCode().ToString("X8") + "_level"], out lv) && (lv <= 0 || lv > 10))
                    {
                        Log("Invaild level, 5 will be used.");
                        lv = 5;
                    }
                }
                foreach (Instruction inst in mtd.Body.Instructions)
                {
                    if (inst.OpCode.Name == "ldc.i4" ||
                        inst.OpCode.Name == "ldc.i8" ||
                        inst.OpCode.Name == "ldc.r4" ||
                        inst.OpCode.Name == "ldc.r8")
                        txts.Add(new Context() { psr = mtd.Body.GetILProcessor(), inst = inst, lv = lv });
                }
                progresser.SetProgress((i + 1) / (double)targets.Count);
            }

            for (int i = 0; i < txts.Count; i++)
            {
                Context txt = txts[i];
                double val = Convert.ToDouble(txt.inst.Operand);
                int seed;

                Expression exp;
                double eval = 0;
                double tmp = 0;
                do
                {
                    exp = ExpressionGenerator.Generate(txt.lv, out seed);
                    eval = (double)new ReflectionVisitor(exp, false, true).Eval(val);
                    try
                    {
                        tmp = (double)new ReflectionVisitor(exp, true, true).Eval(eval);
                    }
                    catch { continue; }
                } while (tmp != val);

                Instruction[] expInsts = new CecilVisitor(exp, true, new Instruction[] { Instruction.Create(OpCodes.Ldc_R8, eval) }, true).GetInstructions();
                if (expInsts.Length == 0) continue;
                string op = txt.inst.OpCode.Name;
                txt.psr.Replace(txt.inst, expInsts[0]);
                for (int ii = 1; ii < expInsts.Length; ii++)
                {
                    txt.psr.InsertAfter(expInsts[ii - 1], expInsts[ii]);
                }
                switch (op)
                {
                    case "ldc.i4":
                        txt.psr.InsertAfter(expInsts[expInsts.Length - 1], Instruction.Create(OpCodes.Conv_I4)); break;
                    case "ldc.i8":
                        txt.psr.InsertAfter(expInsts[expInsts.Length - 1], Instruction.Create(OpCodes.Conv_I8)); break;
                    case "ldc.r4":
                        txt.psr.InsertAfter(expInsts[expInsts.Length - 1], Instruction.Create(OpCodes.Conv_R4)); break;
                    case "ldc.r8":
                        txt.psr.InsertAfter(expInsts[expInsts.Length - 1], Instruction.Create(OpCodes.Conv_R8)); break;
                }

                progresser.SetProgress((i + 1) / (double)txts.Count);
            }
        }

        IProgresser progresser;
        void IProgressProvider.SetProgresser(IProgresser progresser)
        {
            this.progresser = progresser;
        }

        private class Context { public ILProcessor psr; public Instruction inst; public int lv;}
    }
}