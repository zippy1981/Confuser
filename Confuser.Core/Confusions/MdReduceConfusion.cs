﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Confuser.Core.Confusions
{
    public class MdReduceConfusion : StructurePhase, IConfusion
    {
        public string Name
        {
            get { return "Reduce Metadata Confusion"; }
        }
        public string Description
        {
            get
            {
                return @"This confusion reduce the metadata carried by the assembly by removing unnecessary metadata.
***If your application relys on Reflection, you should not apply this confusion***";
            }
        }
        public string ID
        {
            get { return "reduce md"; }
        }
        public bool StandardCompatible
        {
            get { return true; }
        }
        public Target Target
        {
            get { return Target.Events | Target.Properties | Target.Types; }
        }
        public Preset Preset
        {
            get { return Preset.Maximum; }
        }
        public Phase[] Phases
        {
            get { return new Phase[] { this }; }
        }

        public override Priority Priority
        {
            get { return Priority.TypeLevel; }
        }
        public override IConfusion Confusion
        {
            get { return this; }
        }
        public override int PhaseID
        {
            get { return 1; }
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
            IMemberDefinition def = parameter.Target as IMemberDefinition;

            if (def is TypeDefinition && (def as TypeDefinition).IsEnum)
            {
                TypeDefinition t = def as TypeDefinition;
                int idx = 0;
                while (t.Fields.Count != 1)
                    if (t.Fields[idx].Name != "value__")
                        t.Fields.RemoveAt(idx);
                    else
                        idx++;
            }
            else if (def is EventDefinition)
            {
                def.DeclaringType.Events.Remove(def as EventDefinition);
            }
            else if (def is PropertyDefinition)
            {
                def.DeclaringType.Properties.Remove(def as PropertyDefinition);
            }
        }
    }
}