using net = System.Net.Sockets;

namespace Code2.Net.TcpTarpit.Internals.Net
{
	internal class SocketFactory : ISocketFactory
	{
		public ISocket CreateTcpStream(bool ipv4Only = false)
		{
			if (ipv4Only) return Create(net.AddressFamily.InterNetwork, net.SocketType.Stream, net.ProtocolType.Tcp);

			ISocket socket = Create(net.AddressFamily.InterNetworkV6, net.SocketType.Stream, net.ProtocolType.Tcp);
			socket.SetSocketOption(net.SocketOptionLevel.IPv6, net.SocketOptionName.IPv6Only, false);
			return socket;
		}

		public ISocket Create(net.Socket socket)
			=> new Socket(socket);

		private ISocket Create(net.AddressFamily addressFamily, net.SocketType socketType, net.ProtocolType protocolType)
			=> Create(new net.Socket(addressFamily, socketType, protocolType));
	}
}
