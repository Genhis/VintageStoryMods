namespace Mapper.Util.Harmony;

using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Delegate)]
public class DynamicHarmonyPatchAttribute(string? assemblyName, string? typeName, string? methodName) : Attribute {
	public string? AssemblyName = assemblyName;
	public string? TypeName = typeName;
	public string? MethodName = methodName;

	public DynamicHarmonyPatchAttribute() : this(null, null, null) {}
	public DynamicHarmonyPatchAttribute(string methodName) : this(null, null, methodName) {}
	public DynamicHarmonyPatchAttribute(string typeName, string methodName) : this(null, typeName, methodName) {}
}
