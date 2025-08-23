namespace Mapper.Util;

using System;

public class DisposableAction(Action action) : IDisposable {
	public void Dispose() {
		action();
	}
}
