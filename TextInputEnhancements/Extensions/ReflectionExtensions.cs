namespace TextInputEnhancements.Extensions;

using System;
using System.Collections.Generic;
using System.Reflection;

public static class ReflectionExtensions {
	public static string JoinNames(this Type[] args) {
		return string.Join(", ", new List<Type>(args).ConvertAll(item => item.Name));
	}

	public static ConstructorInfo GetCheckedConstructor(this Type t, Type[] args) {
		return t.GetConstructor(args) ?? throw new InvalidOperationException($"Constructor does not exist: {t.Name}({args.JoinNames()})");
	}

	public static FieldInfo GetNonPublicField(this Type t, string name) {
		return t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
	}

	public static PropertyInfo GetCheckedProperty(this Type t, string name) {
		return t.GetProperty(name) ?? throw new InvalidOperationException($"Property does not exist: {t.Name}.{name}");
	}

	public static MethodInfo GetCheckedMethod(this Type t, string name, Type[] args) {
		return t.GetMethod(name, args) ?? throw new InvalidOperationException($"Method does not exist: {t.Name}.{name}({args.JoinNames()})");
	}

	public static MethodInfo CheckedGetMethod(this PropertyInfo property) {
		return property.GetMethod ?? throw new InvalidOperationException($"Property does not have a getter: {property.DeclaringType.Name}.{property.Name}");
	}
}
