namespace Mapper.Util.Reflection;

using System;
using System.Linq.Expressions;
using System.Reflection;

public readonly struct FieldAccessor<TDeclaring, TField> {
	public readonly Func<TDeclaring, TField> GetValue;
	public readonly Action<TDeclaring, TField> SetValue;

#pragma warning disable CS8618 // nullable GetValue/SetValue, will be handled through `ReflectionAccessors.CheckErrors()`
	public FieldAccessor(string fieldName) {
		FieldInfo? field = typeof(TDeclaring).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
		if(!ReflectionAccessors.CheckMember(field, typeof(TField), $"field {ReflectionAccessors.GetUserFriendlyName(typeof(TDeclaring))}.{fieldName}"))
			return;

		ParameterExpression objParam = Expression.Parameter(typeof(TDeclaring));
		ParameterExpression valueParam = Expression.Parameter(typeof(TField));
		MemberExpression fieldAccess = Expression.Field(objParam, field!);
		this.GetValue = Expression.Lambda<Func<TDeclaring, TField>>(fieldAccess, objParam).Compile();
		this.SetValue = Expression.Lambda<Action<TDeclaring, TField>>(Expression.Assign(fieldAccess, valueParam), objParam, valueParam).Compile();
	}
#pragma warning restore CS8618
}
