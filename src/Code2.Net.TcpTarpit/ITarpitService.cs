using System;
using System.Collections.Generic;

namespace Code2.Net.TcpTarpit
{
	public interface ITarpitService
	{
		TarpitServiceOptions Options { get; }
		int ConnectionsCount { get; }
		int ListenersCount { get; }

		event EventHandler<ConnectionCreatedEventArgs>? ConnectionCreated;
		event EventHandler<ConnectionsUpdatedEventArgs>? ConnectionsUpdated;
		event EventHandler<UnhandledExceptionEventArgs>? Error;

		int Start();
		void Stop();
		ConnectionStatus[] GetCurrentConnections();
		void AddListeners(IEnumerable<ushort> ports);
		void AddListener(ushort port);
		void RemoveListener(ushort port);
	}
}