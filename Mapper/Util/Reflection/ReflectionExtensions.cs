namespace Mapper.Util.Reflection;

using System;
using System.Collections.Generic;
using System.Reflection;

public static class ReflectionExtensions {
	private const BindingFlags BindingFlagsAccess = BindingFlags.Public | BindingFlags.NonPublic;

	public static string JoinNames(this Type[] args) {
		return string.Join(", ", new List<Type>(args).ConvertAll(item => item.Name));
	}

	public static MethodInfo GetCheckedMethod(this Type type, string name, BindingFlags instanceFlag, Type[] args) {
		return type.GetMethod(name, BindingFlagsAccess | instanceFlag, args) ?? throw new InvalidOperationException($"Method does not exist: {type.Name}.{name}({args.JoinNames()})");
	}

	public static PropertyInfo GetCheckedProperty(this Type type, string name, BindingFlags instanceFlag) {
		return type.GetProperty(name, BindingFlagsAccess | instanceFlag) ?? throw new InvalidOperationException($"Property does not exist: {type.Name}.{name}");
	}

	public static MethodInfo CheckedGetMethod(this PropertyInfo property) {
		return property.GetMethod ?? throw new InvalidOperationException($"Property does not have a getter: {property.DeclaringType!.Name}.{property.Name}");
	}
}
