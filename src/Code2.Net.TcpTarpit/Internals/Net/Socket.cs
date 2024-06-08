using System;
using System.Net;

namespace Code2.Net.TcpTarpit.Internals.Net
{
	internal class Socket : ISocket
	{
		public Socket(System.Net.Sockets.Socket socket)
		{
			_socket = socket;
		}

		private readonly System.Net.Sockets.Socket _socket;
		private const int _backlogSize = 100;

		public int SendBufferSize
		{
			get => _socket.SendBufferSize;
			set => _socket.SendBufferSize = value;
		}

		public int SendTimeout
		{
			get => _socket.SendTimeout;
			set => _socket.SendTimeout = value;
		}
		public EndPoint? LocalEndPoint => _socket.LocalEndPoint;
		public EndPoint? RemoteEndPoint => _socket.RemoteEndPoint;
		public bool Connected => _socket.Connected;
		public bool IsBound => _socket.IsBound;

		public void Close() => _socket.Close();
		public void Send(byte[] buffer) => _socket.Send(buffer);
		public void SetSocketOption(System.Net.Sockets.SocketOptionLevel optionLevel, System.Net.Sockets.SocketOptionName optionName, bool value)
			=> _socket.SetSocketOption(optionLevel, optionName, value);

		public void Bind(EndPoint endPoint) => _socket.Bind(endPoint);
		public void Listen() => _socket.Listen(_backlogSize);
		public void BeginAccept(AsyncCallback? asyncCallback, object? state) => _socket.BeginAccept(asyncCallback, state);
		public ISocket EndAccept(IAsyncResult asyncResult) => new Socket(_socket.EndAccept(asyncResult));
		public void Dispose() => _socket.Dispose();
	}
}
