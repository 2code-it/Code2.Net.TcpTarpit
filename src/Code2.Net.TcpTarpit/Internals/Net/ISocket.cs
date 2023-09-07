using System.Net;

namespace Code2.Net.TcpTarpit.Internals.Net
{
	internal interface ISocket
	{
		int SendBufferSize { get; set; }
		EndPoint? LocalEndPoint { get; }
		EndPoint? RemoteEndPoint { get; }
		bool Connected { get; }
		bool IsBound { get; }

		void BeginAccept(AsyncCallback? asyncCallback, object? state);
		void Bind(EndPoint endPoint);
		void Close();
		void Dispose();
		ISocket EndAccept(IAsyncResult asyncResult);
		void Listen();
		void SetSocketOption(System.Net.Sockets.SocketOptionLevel optionLevel, System.Net.Sockets.SocketOptionName optionName, bool value);
		void Send(byte[] buffer);
	}
}