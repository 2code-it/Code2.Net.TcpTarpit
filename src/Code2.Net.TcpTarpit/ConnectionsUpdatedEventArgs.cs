using System;

namespace Code2.Net.TcpTarpit
{
	public class ConnectionsUpdatedEventArgs : EventArgs
	{
		public ConnectionsUpdatedEventArgs(ConnectionStatus[] connections)
		{
			Connections = connections;
		}

		public ConnectionStatus[] Connections { get; private set; }
	}
}
