namespace Mapper.Util.Reflection;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;

public static class ReflectionAccessors {
	private static readonly List<string> errors = [];

	/// <returns>True when an error is found.</returns>
	public static bool CheckErrors(ILogger logger) {
		foreach(Type type in Assembly.GetCallingAssembly().GetTypes())
			RuntimeHelpers.RunClassConstructor(type.TypeHandle);

		if(ReflectionAccessors.errors.Count == 0)
			return false;

		logger.Error("Unable to initialize some reflection accessors, disabling mod:");
		foreach(string error in ReflectionAccessors.errors)
			logger.Error(error);
		ReflectionAccessors.errors.Clear();
		return true;
	}

	public static string GetUserFriendlyName(Type? type) {
		if(type == null)
			return "null";
		if(!type.IsGenericType)
			return type.Name;

		string genericName = type.GetGenericTypeDefinition().Name;
		int backtick = genericName.IndexOf('`');
		if(backtick > 0)
			genericName = genericName[..backtick];
		return $"{genericName}<{string.Join(", ", type.GetGenericArguments().Select(ReflectionAccessors.GetUserFriendlyName))}>";
	}

	internal static bool CheckMember(MemberInfo? member, Type expectedType, string typeAndFieldName) {
		static string GetCallingTypeName() => ReflectionAccessors.GetUserFriendlyName(new StackTrace(3).GetFrame(0)!.GetMethod()!.DeclaringType);

		if(member == null) {
			ReflectionAccessors.errors.Add($"Type {GetCallingTypeName()} requested {typeAndFieldName} which does not exist.");
			return false;
		}

		Type? actualType = ReflectionAccessors.GetActualType(member);
		if(expectedType != actualType) {
			ReflectionAccessors.errors.Add($"Type {GetCallingTypeName()} requested {typeAndFieldName} which does not match the expected type, actual type is {ReflectionAccessors.GetUserFriendlyName(actualType)}.");
			return false;
		}
		return true;
	}

	private static Type? GetActualType(MemberInfo member) {
		if(member is FieldInfo field)
			return field.FieldType;
		return null;
	}
}
