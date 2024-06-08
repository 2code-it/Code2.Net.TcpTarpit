using Code2.Net.TcpTarpit.Internals.Net;
using System;
using System.Threading.Tasks;

namespace Code2.Net.TcpTarpit
{
	public class TarpitConnection : IDisposable
	{
		internal TarpitConnection(ISocket socket, IByteReader byteReader, int timeoutInSeconds)
		{
			_socket = socket;
			_byteReader = byteReader;
			_timeoutInSeconds = timeoutInSeconds;
			_sendBuffer = new byte[socket.SendBufferSize];
			Status.Created = DateTime.Now;
		}

		private readonly ISocket _socket;
		private readonly IByteReader _byteReader;
		private readonly int _timeoutInSeconds;
		private int _readerPosition;
		private byte[] _sendBuffer;
		private bool _isSending;

		public int Id
		{
			get => Status.Id;
			set => Status.Id = value;
		}
		public ConnectionStatus Status { get; private set; } = new ConnectionStatus();

		public void Update()
		{
			Status.DurationInSeconds = (int)Math.Round((DateTime.Now - Status.Created).TotalSeconds);
			if (Status.DurationInSeconds >= _timeoutInSeconds) Close();
		}

		public void Send()
		{
			if (_isSending || !_socket.Connected) return;
			Task.Run(() =>
			{
				_isSending = true;
				_readerPosition = _byteReader.Read(_sendBuffer, _readerPosition);
				try
				{
					_socket.Send(_sendBuffer);
					Status.BytesSent += _sendBuffer.Length;
				}
				catch
				{
					Close();
				}
				_isSending = false;
			});
		}

		public void Close()
		{
			if (_socket.Connected) _socket.Close();
			_socket.Dispose();
			_isSending = false;
			Status.IsCompleted = true;
		}

		public void Dispose()
		{
			Close();
		}
	}
}
