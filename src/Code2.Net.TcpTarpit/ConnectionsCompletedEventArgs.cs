namespace Code2.Net.TcpTarpit
{
	public class ConnectionsCompletedEventArgs : EventArgs
	{
		public ConnectionsCompletedEventArgs(ConnectionStatus[] connections)
		{
			Connections = connections;
		}

		public ConnectionStatus[] Connections { get; private set; }
	}
}
