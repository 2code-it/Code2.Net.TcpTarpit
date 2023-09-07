using System.Net.Sockets;

namespace Code2.Net.TcpTarpit.Internals.Net
{
	internal interface ISocketFactory
	{
		ISocket CreateTcpStream(bool ipv4Only = false);
	}
}