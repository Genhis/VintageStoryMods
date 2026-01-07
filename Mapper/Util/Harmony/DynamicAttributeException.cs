namespace Mapper.Util.Harmony;

using System;

public class DynamicAttributeException : Exception {
	public DynamicAttributeException(string message) : base(message) { }
	public DynamicAttributeException(string attributeName, string memberString, string message) : base($"Failed to process {attributeName} attribute of {memberString}: {message}") {}
}
