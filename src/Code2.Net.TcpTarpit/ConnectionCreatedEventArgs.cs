namespace Code2.Net.TcpTarpit
{
	public class ConnectionCreatedEventArgs : EventArgs
	{
		public ConnectionCreatedEventArgs(ConnectionStatus connection)
		{
			Connection = connection;
		}

		public ConnectionStatus Connection { get; private set; }
	}
}
