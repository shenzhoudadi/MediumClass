# Harmony binary signature audit

This executable reads metadata with Mono.Cecil. It does not execute or load the
game, consumer mod, or Harmony code. It checks all external Harmony type, method,
and field references in the complete merged mod assembly (including BlueprintCore).
Method matching includes return type, parameter list, instance/static calling
convention, generic arity, nested types, and normalized generic parameters.
Constructed declaring-type generic arguments are applied to both signatures;
this is important for `AccessTools.FieldRef<T,F>.Invoke` and its by-ref return.

Run with `dotnet run --project <this-directory> -- <merged-mod.dll> <runtime-0Harmony.dll> [report.json]`.
Exit code 0 means all referenced signatures exist. Exit code 1 lists missing
references and any available overloads; exit code 2 indicates incorrect usage.

The check compares binary signatures, not Harmony behavior, Unity execution,
runtime binding policy, or dynamically generated reflection calls. The JSON
report contains the exact paths, assembly versions, checked references, and IL
callers. A reference with no IL caller may be metadata such as an attribute.
