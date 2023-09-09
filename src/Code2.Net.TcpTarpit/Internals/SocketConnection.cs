using Code2.Net.TcpTarpit.Internals.Net;

namespace Code2.Net.TcpTarpit.Internals
{
	internal class SocketConnection
	{
		public SocketConnection(ISocket socket, ConnectionStatus connection)
		{
			Socket = socket;
			Connection = connection;
		}

		public int Id => Connection.Id;
		public ISocket Socket { get; private set; }
		public ConnectionStatus Connection { get; private set; }
	}
}
