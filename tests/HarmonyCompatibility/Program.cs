using Mono.Cecil;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length < 2 || args.Length > 3)
{
    Console.Error.WriteLine("Usage: HarmonyCompatCheck <consumer.dll> <runtime/0Harmony.dll> [report.json]");
    return 2;
}

// Metadata-only: neither the consumer nor Harmony is executed or reflection-loaded.
using var consumer = AssemblyDefinition.ReadAssembly(Path.GetFullPath(args[0]));
using var runtime = AssemblyDefinition.ReadAssembly(Path.GetFullPath(args[1]));
var runtimeName = runtime.Name.Name;
var definitions = AllTypes(runtime.MainModule.Types).ToDictionary(t => t.FullName);
var memberRefs = consumer.MainModule.GetMemberReferences().Where(m => IsRuntimeType(m.DeclaringType)).ToArray();
var typeRefs = consumer.MainModule.GetTypeReferences().Where(IsRuntimeType).ToArray();
var uses = new Dictionary<string, HashSet<string>>();
AddAttributes(consumer, "assembly " + consumer.Name.Name);
AddAttributes(consumer.MainModule, "module " + consumer.MainModule.Name);
foreach (var type in AllTypes(consumer.MainModule.Types))
{
    AddAttributes(type, type.FullName);
    foreach (var field in type.Fields) AddAttributes(field, field.FullName);
    foreach (var property in type.Properties) AddAttributes(property, property.FullName);
    foreach (var @event in type.Events) AddAttributes(@event, @event.FullName);
    foreach (var method in type.Methods)
    {
        AddAttributes(method, method.FullName);
        AddAttributes(method.MethodReturnType, method.FullName + " return");
        foreach (var parameter in method.Parameters) AddAttributes(parameter, method.FullName + " parameter " + parameter.Name);
        if (!method.HasBody) continue;
        foreach (var instruction in method.Body.Instructions)
            if (instruction.Operand is MemberReference member && IsRuntimeType(member.DeclaringType))
                AddUse(member, method.FullName);
    }
}

var findings = new List<Finding>();
foreach (var type in typeRefs.GroupBy(t => t.GetElementType().FullName).Select(g => g.First()))
{
    if (!definitions.ContainsKey(type.GetElementType().FullName))
        findings.Add(new("type", type.FullName, [], []));
}

foreach (var member in memberRefs)
{
    if (!definitions.TryGetValue(member.DeclaringType.GetElementType().FullName, out var declaring))
        continue; // Already reported as a missing type.
    var candidates = member switch
    {
        MethodReference method => declaring.Methods.Where(m => m.Name == method.Name)
            .Select(m => (MemberReference)m).ToArray(),
        FieldReference field => declaring.Fields.Where(f => f.Name == field.Name)
            .Select(f => (MemberReference)f).ToArray(),
        _ => [],
    };
    if (candidates.Any(c => Matches(member, c))) continue;
    var callsites = uses.GetValueOrDefault(MemberKey(member))?.Order().ToArray() ?? [];
    findings.Add(new(member is MethodReference ? "method" : "field", member.FullName,
        candidates.Select(c => c.FullName).ToArray(), callsites));
}

var methods = memberRefs.OfType<MethodReference>().ToArray();
var fields = memberRefs.OfType<FieldReference>().ToArray();
var report = new
{
    Consumer = Path.GetFullPath(args[0]), ConsumerAssembly = consumer.Name.FullName,
    ConsumerSHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))),
    Runtime = Path.GetFullPath(args[1]), RuntimeAssembly = runtime.Name.FullName,
    RuntimeSHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[1]))),
    DeclaredReference = consumer.MainModule.AssemblyReferences.FirstOrDefault(a => a.Name == runtimeName)?.FullName,
    CheckedTypes = typeRefs.Length, CheckedMethods = methods.Length, CheckedFields = fields.Length,
    MissingCount = findings.Count, Findings = findings,
    CheckedTypeReferences = typeRefs.Select(t => t.FullName).ToArray(),
    CheckedMethodReferences = methods.Select(m => new
    {
        Signature = m.FullName,
        Callers = uses.GetValueOrDefault(MemberKey(m))?.Order().ToArray() ?? [],
    }).ToArray(),
};
if (args.Length == 3)
    File.WriteAllText(Path.GetFullPath(args[2]), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Consumer: {consumer.Name.FullName}");
Console.WriteLine($"Runtime: {runtime.Name.FullName}");
Console.WriteLine($"Checked {typeRefs.Length} Harmony types, {methods.Length} method references, {fields.Length} field references.");
foreach (var finding in findings)
{
    Console.WriteLine($"MISSING {finding.Kind}: {finding.Reference}");
    foreach (var candidate in finding.AvailableOverloads) Console.WriteLine($"  available: {candidate}");
    foreach (var caller in finding.Callers) Console.WriteLine($"  caller: {caller}");
}
Console.WriteLine(findings.Count == 0 ? "PASS: all referenced Harmony metadata signatures exist." : $"FAIL: {findings.Count} incompatible Harmony references.");
return findings.Count == 0 ? 0 : 1;

bool IsRuntimeType(TypeReference? type)
{
    if (type == null) return false;
    type = type.GetElementType();
    while (type.DeclaringType != null) type = type.DeclaringType.GetElementType();
    return type.Scope is AssemblyNameReference scope && scope.Name == runtimeName;
}

void AddUse(MemberReference member, string location)
{
    var key = MemberKey(member);
    if (!uses.TryGetValue(key, out var callers)) uses[key] = callers = [];
    callers.Add(location);
}

void AddAttributes(ICustomAttributeProvider provider, string location)
{
    foreach (var attribute in provider.CustomAttributes)
        if (IsRuntimeType(attribute.AttributeType))
            AddUse(attribute.Constructor, location + " [attribute]");
}

static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> roots)
{
    foreach (var root in roots)
    {
        yield return root;
        foreach (var nested in AllTypes(root.NestedTypes)) yield return nested;
    }
}

static string MemberKey(MemberReference member) => member is GenericInstanceMethod method
    ? method.ElementMethod.FullName : member.FullName;

static bool Matches(MemberReference expected, MemberReference actual)
{
    // Both signatures are instantiated in the referenced declaring-type context.
    // A FieldRef<T,F>.Invoke reference uses !0/!1 in its member signature even
    // though its declaring type is FieldRef<Spellbook,int[]>; normalize both sides.
    var typeArguments = (expected.DeclaringType as GenericInstanceType)?.GenericArguments.ToArray() ?? [];
    if (expected is FieldReference ef && actual is FieldReference af)
        return ef.Name == af.Name && TypeKey(ef.FieldType, typeArguments, []) == TypeKey(af.FieldType, typeArguments, []);
    if (expected is not MethodReference em || actual is not MethodReference am) return false;
    var methodArguments = (em as GenericInstanceMethod)?.GenericArguments.ToArray() ?? [];
    em = em.GetElementMethod();
    am = am.GetElementMethod();
    return em.Name == am.Name && em.HasThis == am.HasThis && em.ExplicitThis == am.ExplicitThis
        && em.CallingConvention == am.CallingConvention
        && em.GenericParameters.Count == am.GenericParameters.Count
        && em.Parameters.Count == am.Parameters.Count
        && TypeKey(em.ReturnType, typeArguments, methodArguments) == TypeKey(am.ReturnType, typeArguments, methodArguments)
        && em.Parameters.Select(p => TypeKey(p.ParameterType, typeArguments, methodArguments))
            .SequenceEqual(am.Parameters.Select(p => TypeKey(p.ParameterType, typeArguments, methodArguments)));
}

static string TypeKey(TypeReference type, TypeReference[] typeArgs, TypeReference[] methodArgs)
{
    if (type is GenericParameter generic)
    {
        var substitution = generic.Type == GenericParameterType.Type ? typeArgs : methodArgs;
        if (generic.Position < substitution.Length)
            return TypeKey(substitution[generic.Position], [], []);
        return (generic.Type == GenericParameterType.Type ? "!" : "!!") + generic.Position;
    }
    return type switch
    {
        GenericInstanceType instance => TypeKey(instance.ElementType, [], []) + "<" +
            string.Join(",", instance.GenericArguments.Select(t => TypeKey(t, typeArgs, methodArgs))) + ">",
        ByReferenceType byref => TypeKey(byref.ElementType, typeArgs, methodArgs) + "&",
        PointerType pointer => TypeKey(pointer.ElementType, typeArgs, methodArgs) + "*",
        ArrayType array => TypeKey(array.ElementType, typeArgs, methodArgs) +
            (array.IsVector ? "[]" : "[" + string.Join(",", array.Dimensions.Select(d => d.ToString())) + "]"),
        RequiredModifierType required => "modreq(" + TypeKey(required.ModifierType, typeArgs, methodArgs) + ")" + TypeKey(required.ElementType, typeArgs, methodArgs),
        OptionalModifierType optional => "modopt(" + TypeKey(optional.ModifierType, typeArgs, methodArgs) + ")" + TypeKey(optional.ElementType, typeArgs, methodArgs),
        PinnedType pinned => TypeKey(pinned.ElementType, typeArgs, methodArgs) + " pinned",
        SentinelType sentinel => "sentinel " + TypeKey(sentinel.ElementType, typeArgs, methodArgs),
        FunctionPointerType function => $"function[{function.CallingConvention},{function.HasThis},{function.ExplicitThis}] " +
            TypeKey(function.ReturnType, typeArgs, methodArgs) + "(" +
            string.Join(",", function.Parameters.Select(p => TypeKey(p.ParameterType, typeArgs, methodArgs))) + ")",
        _ => type.FullName,
    };
}

record Finding(string Kind, string Reference, string[] AvailableOverloads, string[] Callers);
