using Sockets = System.Net.Sockets;

namespace Code2.Net.TcpTarpit.Internals.Net
{
	internal class SocketFactory : ISocketFactory
	{
		public ISocket CreateTcpStream(bool ipv4Only = false)
		{
			if (ipv4Only) return Create(Sockets.AddressFamily.InterNetwork, Sockets.SocketType.Stream, Sockets.ProtocolType.Tcp);

			ISocket socket = Create(Sockets.AddressFamily.InterNetworkV6, Sockets.SocketType.Stream, Sockets.ProtocolType.Tcp);
			socket.SetSocketOption(Sockets.SocketOptionLevel.IPv6, Sockets.SocketOptionName.IPv6Only, false);
			return socket;
		}

		public ISocket Create(Sockets.Socket socket)
			=> new Socket(socket);

		private ISocket Create(Sockets.AddressFamily addressFamily, Sockets.SocketType socketType, Sockets.ProtocolType protocolType)
			=> Create(new Sockets.Socket(addressFamily, socketType, protocolType));
	}
}
