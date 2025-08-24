namespace Mapper.Util.Harmony;

using HarmonyLib;
using Mapper.Util;
using Mapper.Util.Reflection;
using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;

internal class DynamicPatchResolver(Harmony harmony, ILogger logger) {
	// [HarmonyID -> [AssemblyName -> [PatchedMethods]]]
	private static readonly Dictionary<string, Dictionary<string, HashSet<MethodInfo>>> patchedAssemblies = [];

	private readonly Harmony harmony = harmony;
	private readonly ILogger logger = logger;
	private readonly Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
	private readonly HashSet<string> skippedAssemblies = [];

	public void Patch(Assembly callingAssembly) {
		foreach(Type type in callingAssembly.GetTypes()) {
			DynamicHarmonyPatchAttribute? attribute = type.GetCustomAttribute<DynamicHarmonyPatchAttribute>();
			if(attribute == null)
				continue;
			if(attribute.AssemblyName == null && attribute.TypeName != null)
				throw new DynamicAttributeException("DynamicHarmonyPatch", type.Name, "AssemblyName cannot be null when TypeName is specified on a class");
			if(attribute.MethodName != null)
				throw new DynamicAttributeException("DynamicHarmonyPatch", type.Name, "MethodName cannot be applied to a class");

			Assembly? targetClassAssembly = this.GetAssembly(attribute.AssemblyName);
			if(!this.TryGetTargetType(targetClassAssembly, attribute.TypeName, out Type? targetClassType))
				continue;

			foreach(MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
				this.CheckPatchMethod(targetClassAssembly, targetClassType, method);
		}
	}

	private void CheckPatchMethod(Assembly? targetClassAssembly, Type? targetClassType, MethodInfo method) {
		DynamicHarmonyPatchAttribute? attribute = method.GetCustomAttribute<DynamicHarmonyPatchAttribute>();
		if(attribute == null)
			return;
		if(attribute.MethodName == null)
			throw new DynamicAttributeException("DynamicHarmonyPatch", method.GetParentAndName(), "MethodName cannot be null");

		Assembly? targetAssembly = this.GetAssembly(attribute.AssemblyName) ?? targetClassAssembly;
		if(targetAssembly == null || this.skippedAssemblies.Contains(targetAssembly.GetName().Name!))
			return;

		try {
			Type? targetType = DynamicPatchResolver.GetType(targetAssembly, attribute.TypeName) ?? targetClassType;
			if(targetType == null)
				throw new DynamicAttributeException("DynamicHarmonyPatch", method.GetParentAndName(), "TypeName cannot be null");
			MethodInfo targetMethod = targetType.GetCheckedMethod(attribute.MethodName, BindingFlags.Static | BindingFlags.Instance, null);
			this.PatchTarget(targetAssembly, method, targetMethod);
		}
		catch(DynamicAttributeException) {
			throw;
		}
		catch(Exception ex) {
			this.UnpatchAndSkipAssembly(targetAssembly, ex);
		}
	}

	private void PatchTarget(Assembly assembly, MethodInfo patchMethod, MethodInfo targetMethod) {
		HarmonyMethod? prefix = patchMethod.GetCustomAttribute<HarmonyPrefix>() == null ? null : new HarmonyMethod(patchMethod);
		HarmonyMethod? postfix = patchMethod.GetCustomAttribute<HarmonyPostfix>() == null ? null : new HarmonyMethod(patchMethod);
		HarmonyMethod? transpiler = patchMethod.GetCustomAttribute<HarmonyTranspiler>() == null ? null : new HarmonyMethod(patchMethod);
		HarmonyMethod? finalizer = patchMethod.GetCustomAttribute<HarmonyFinalizer>() == null ? null : new HarmonyMethod(patchMethod);
		if(prefix == null && postfix == null && transpiler == null && finalizer == null)
			throw new DynamicAttributeException($"Method {patchMethod.GetParentAndName()} lacks a Harmony annotation which could determine its patch type");

		this.harmony.Patch(targetMethod, prefix, postfix, transpiler, finalizer);
		DynamicPatchResolver.patchedAssemblies.GetOrCreate(this.harmony.Id).GetOrCreate(assembly.GetName().Name!).Add(patchMethod);
	}

	private void UnpatchAndSkipAssembly(Assembly assembly, Exception reason) {
		string assemblyName = assembly.GetName().Name!;
		this.logger.Error($"An error occured, disabling patches for {assemblyName} assembly:\n{reason}");
		DynamicPatchResolver.Unpatch(this.harmony, assemblyName);
		this.skippedAssemblies.Add(assemblyName);
	}

	private bool TryGetTargetType(Assembly? targetAssembly, string? name, out Type? targetType) {
		try {
			targetType = targetAssembly == null ? null : DynamicPatchResolver.GetType(targetAssembly, name);
			return true;
		}
		catch(Exception ex) {
			this.UnpatchAndSkipAssembly(targetAssembly!, ex);
			targetType = null;
			return false;
		}
	}

	private Assembly? GetAssembly(string? name) {
		if(name != null)
			foreach(Assembly assembly in this.assemblies)
				if(assembly.GetName().Name == name)
					return assembly;
		return null;
	}

	public static void Unpatch(Harmony harmony, string assemblyName) {
		if(!DynamicPatchResolver.patchedAssemblies.TryGetValue(harmony.Id, out Dictionary<string, HashSet<MethodInfo>>? patchedAssemblies))
			return;
		if(!patchedAssemblies.TryGetValue(assemblyName, out HashSet<MethodInfo>? patchedMethods))
			return;

		foreach(MethodInfo targetMethod in patchedMethods)
			harmony.Unpatch(targetMethod, HarmonyPatchType.All, harmony.Id);
		patchedAssemblies.Remove(assemblyName);
		if(patchedAssemblies.Count == 0)
			DynamicPatchResolver.patchedAssemblies.Remove(harmony.Id);
	}

	private static Type? GetType(Assembly assembly, string? name) {
		return name == null ? null : assembly.GetCheckedType(name);
	}
}
