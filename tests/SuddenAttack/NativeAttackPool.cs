using System.Reflection.Emit;
using Kingmaker.RuleSystem.Rules;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Executes the installed game's complete static AddExtraAttackForHand IL body,
// with only its three integer fields redirected to AttackPool and Math.Max
// redirected to the host runtime. Unknown IL or API changes fail closed.
internal static class NativeAttackPool
{
    public static Action<AttackPool, int, bool, bool> Merge;
    public static void Load(AssemblyDefinition game)
    {
        var rule = game.MainModule.Types.Single(t => t.FullName == "Kingmaker.RuleSystem.Rules.RuleCalculateAttacksCount");
        var method = rule.Methods.Single(m => m.Name == "AddExtraAttackForHand");
        if (!method.IsStatic || method.Parameters.Count != 4 || method.Body.Variables.Count != 0 || method.Body.ExceptionHandlers.Count != 0)
            throw new NotSupportedException("Native AddExtraAttackForHand shape changed.");
        var fields = typeof(AttackPool).GetFields().ToDictionary(f => f.Name);
        var ops = typeof(System.Reflection.Emit.OpCodes).GetFields()
            .Where(f => f.FieldType == typeof(System.Reflection.Emit.OpCode))
            .Select(f => (System.Reflection.Emit.OpCode)f.GetValue(null)).ToDictionary(o => o.Value);
        var dynamic = new DynamicMethod("InstalledGame_AddExtraAttackForHand", typeof(void),
            new[] { typeof(AttackPool), typeof(int), typeof(bool), typeof(bool) });
        var il = dynamic.GetILGenerator();
        var labels = method.Body.Instructions.ToDictionary(i => i, i => il.DefineLabel());
        foreach (var instruction in method.Body.Instructions)
        {
            il.MarkLabel(labels[instruction]);
            var op = ops[instruction.OpCode.Value];
            switch (instruction.Operand)
            {
                case null:
                    if (instruction.OpCode.Code is not (Code.Ldarg_0 or Code.Ldarg_1 or Code.Ldarg_2 or Code.Ldarg_3 or Code.Dup or Code.Add or Code.Ret or Code.Nop))
                        throw new NotSupportedException("Unexpected native opcode " + instruction);
                    il.Emit(op); break;
                case Instruction target when instruction.OpCode.Code is Code.Brfalse or Code.Brfalse_S:
                    il.Emit(op, labels[target]); break;
                case FieldReference field when field.DeclaringType.FullName == "Kingmaker.RuleSystem.Rules.RuleCalculateAttacksCount/AttacksCount"
                    && field.FieldType.FullName == "System.Int32" && fields.ContainsKey(field.Name)
                    && instruction.OpCode.Code is Code.Ldfld or Code.Stfld:
                    il.Emit(op, fields[field.Name]); break;
                case MethodReference called when called.FullName == "System.Int32 System.Math::Max(System.Int32,System.Int32)" && instruction.OpCode.Code == Code.Call:
                    il.Emit(op, typeof(Math).GetMethod(nameof(Math.Max), new[] { typeof(int), typeof(int) })); break;
                default: throw new NotSupportedException("Unexpected native IL " + instruction);
            }
        }
        Merge = dynamic.CreateDelegate<Action<AttackPool, int, bool, bool>>();
    }
}
