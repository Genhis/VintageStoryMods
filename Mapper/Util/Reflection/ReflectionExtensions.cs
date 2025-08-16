namespace Mapper.Util.Reflection;

using System;
using System.Collections.Generic;
using System.Reflection;

public static class ReflectionExtensions {
	private const BindingFlags BindingFlagsAccess = BindingFlags.Public | BindingFlags.NonPublic;

	public static string JoinNames(this Type[] args) {
		return string.Join(", ", new List<Type>(args).ConvertAll(item => item.Name));
	}

	public static ConstructorInfo GetCheckedConstructor(this Type type, Type[] args) {
		return type.GetConstructor(BindingFlagsAccess | BindingFlags.Instance, args) ?? throw new InvalidOperationException($"Constructor does not exist: {type.Name}({args.JoinNames()})");
	}

	public static EventInfo GetCheckedEvent(this Type type, string name, BindingFlags instanceFlag) {
		return type.GetEvent(name, BindingFlagsAccess | instanceFlag) ?? throw new InvalidOperationException($"Event does not exist: {type.Name}.{name}");
	}

	public static FieldInfo GetCheckedField(this Type type, string name, BindingFlags instanceFlag) {
		return type.GetField(name, BindingFlagsAccess | instanceFlag) ?? throw new InvalidOperationException($"Field does not exist: {type.Name}.{name}");
	}

	public static MethodInfo GetCheckedMethod(this Type type, string name, BindingFlags instanceFlag, Type[] args) {
		return type.GetMethod(name, BindingFlagsAccess | instanceFlag, args) ?? throw new InvalidOperationException($"Method does not exist: {type.Name}.{name}({args.JoinNames()})");
	}

	public static PropertyInfo GetCheckedProperty(this Type type, string name, BindingFlags instanceFlag) {
		return type.GetProperty(name, BindingFlagsAccess | instanceFlag) ?? throw new InvalidOperationException($"Property does not exist: {type.Name}.{name}");
	}

	public static MethodInfo CheckedAddMethod(this EventInfo evt) {
		return evt.AddMethod ?? throw new InvalidOperationException($"Event does not have an add method: {evt.DeclaringType!.Name}.{evt.Name}");
	}

	public static MethodInfo CheckedGetMethod(this PropertyInfo property) {
		return property.GetMethod ?? throw new InvalidOperationException($"Property does not have a getter: {property.DeclaringType!.Name}.{property.Name}");
	}
}
