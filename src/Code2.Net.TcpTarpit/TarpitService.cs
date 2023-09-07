using Code2.Net.TcpTarpit.Internals;
using Code2.Net.TcpTarpit.Internals.Net;
using System.Net;
using System.Text;

namespace Code2.Net.TcpTarpit
{
	public class TarpitService : ITarpitService
	{
		public TarpitService(TarpitServiceOptions options) : this(options, new InfiniteReaderFactory()) { }
		public TarpitService(TarpitServiceOptions options, IByteReaderFactory byteReaderFactory)
			: this(options, byteReaderFactory, new SocketFactory()) { }
		internal TarpitService(TarpitServiceOptions options, IByteReaderFactory byteReaderFactory, ISocketFactory socketFactory)
		{
			_options = options;
			_reader = options.ResponseText is null ?
				byteReaderFactory.Create(options.ResponseFile) :
				byteReaderFactory.Create(Encoding.UTF8.GetBytes(options.ResponseText));
			_socketFactory = socketFactory;
		}

		private object _lock = new();
		private ISocket[]? _listeners;
		private Timer? _timerConnectionUpdate;
		private DateTime _nextConnectionsUpdate;
		private int _connectionId;
		private bool _isUpdating;

		private readonly IByteReader _reader;
		private readonly TarpitServiceOptions _options;
		private readonly ISocketFactory _socketFactory;
		private readonly IList<SocketConnection> _connections = new List<SocketConnection>();

		public event EventHandler<UnhandledExceptionEventArgs>? Error;
		public event EventHandler<ConnectionCreatedEventArgs>? ConnectionCreated;
		public event EventHandler<ConnectionsUpdatedEventArgs>? ConnectionsUpdated;


		public int ConnectionsCount => _connections.Count;
		public int ListenersCount => _listeners?.Length ?? 0;

		public int Start()
		{
			string? validationResult = TarpitService.ValidateOptions(_options);
			if (validationResult is not null)
			{
				OnError(new InvalidOperationException(validationResult), true);
				return -1;
			}

			_nextConnectionsUpdate = DateTime.Now.AddSeconds(_options.UpdateIntervalInSeconds);
			ushort[] ports = GetPorts(_options.PortRangeBegin, _options.PortRangeEnd);
			IPAddress listenAddress = IPAddress.Parse(_options.ListenAddress!);
			_listeners = ports.Select(x => TryGetStartedListener(listenAddress, x))
				.Where(x => x is not null).ToArray()!;
			_timerConnectionUpdate = new Timer(new TimerCallback(OnTimerConnectionUpdate), null, 0, _options.WriteIntervalInMs);
			return _listeners.Length;
		}

		public void Stop()
		{
			_timerConnectionUpdate?.Dispose();
			Parallel.ForEach(_listeners!, x => x.Dispose());
			_listeners = null;
			SocketConnection[] connections = _connections.ToArray();
			_connections.Clear();

			Parallel.ForEach(connections, CloseAndComplete);
			OnConnectionsUpdated(connections);
		}

		private void CloseAndComplete(SocketConnection sc)
		{
			if (!sc.Connection.IsCompleted)
			{
				sc.Socket.Close();
				sc.Connection.IsCompleted = true;
			}
		}

		private void OnConnectionCreated(SocketConnection socketConnection)
		{
			lock (_lock)
			{
				_connections.Add(socketConnection);
			}
			ConnectionCreated?.Invoke(this, new ConnectionCreatedEventArgs(socketConnection.Connection));
		}

		private void OnConnectionsUpdated(SocketConnection[] connections)
		{
			lock (_lock)
			{
				SocketConnection[] connectionsToRemove = connections.Where(x=>x.Connection.IsCompleted).ToArray();
				foreach (SocketConnection connection in connectionsToRemove)
				{
					_connections.Remove(connection);
				}
			}
			ConnectionsUpdated?.Invoke(this, new ConnectionsUpdatedEventArgs(connections.Select(x => x.Connection).ToArray()));

		}

		private void OnError(Exception exception, bool isTerminating = false)
		{
			if (Error is null) throw exception;
			Error.Invoke(this, new UnhandledExceptionEventArgs(exception, isTerminating));
		}


		private void OnTimerConnectionUpdate(object? state)
		{
			if (_isUpdating) return;
			_isUpdating = true;

			SocketConnection[] connections = _connections.ToArray();
			SocketConnection[] activeConnections = _connections.Where(x => !x.Connection.IsCompleted).ToArray();
			Parallel.ForEach(activeConnections, TrySendAndUpdate);

			if (_nextConnectionsUpdate <= DateTime.Now)
			{
				OnConnectionsUpdated(connections);
				_nextConnectionsUpdate = DateTime.Now.AddSeconds(_options.UpdateIntervalInSeconds);
			}
			_isUpdating = false;
		}

		private void TrySendAndUpdate(SocketConnection sc)
		{
			sc.Connection.ReaderPosition = _reader.Read(sc.Connection.Buffer, sc.Connection.ReaderPosition);
			sc.Connection.IsCompleted = sc.Connection.End < DateTime.Now;
			try
			{
				sc.Socket.Send(sc.Connection.Buffer);
			}
			catch
			{
				sc.Connection.IsCompleted = true;
			}
			sc.Connection.BytesSent += sc.Connection.Buffer.Length;

			if (sc.Connection.IsCompleted)
			{
				sc.Socket.Close();
			}
		}

		private void ConnectionAdd(ISocket socket)
		{
			socket.SendBufferSize = _options.WriteSize;
			ConnectionStatus connection = new ConnectionStatus();
			connection.Created = DateTime.Now;
			connection.End = connection.Created.AddSeconds(_options.TimeoutInSeconds);
			connection.Buffer = new byte[_options.WriteSize];
			connection.LocalEndPoint = socket.LocalEndPoint?.ToString()!;
			connection.RemoteEndPoint = socket.RemoteEndPoint?.ToString()!;
			connection.Id = GetNextConnectionId();
			OnConnectionCreated(new SocketConnection(socket, connection));
		}

		private ISocket? TryGetStartedListener(IPAddress ipAddress, ushort port)
		{
			IPEndPoint endpoint = new IPEndPoint(ipAddress, port);
			ISocket listener = _socketFactory.CreateTcpStream(_options.UseIPv4Only);

			try
			{
				listener.Bind(endpoint);
				listener.Listen();
			}
			catch (Exception ex)
			{
				OnError(ex);
				return null;
			}

			listener.BeginAccept(new AsyncCallback(OnBeginAcceptSocket), listener);

			return listener;
		}

		private void OnBeginAcceptSocket(IAsyncResult asyncResult)
		{
			ISocket listener = (ISocket)asyncResult.AsyncState!;
			try
			{
				ISocket client = listener.EndAccept(asyncResult);
				ConnectionAdd(client);
			}
			catch
			{
				return;
			}

			if (listener.IsBound)
			{
				listener.BeginAccept(new AsyncCallback(OnBeginAcceptSocket), listener);
			}
		}

		private int GetNextConnectionId()
		{
			lock (_lock)
			{
				unchecked
				{
					return ++_connectionId;
				}
			}
		}

		private static ushort[] GetPorts(ushort portBegin, ushort portEnd)
			=> Enumerable.Range(portBegin, 1 + portEnd - portBegin).Select(x => (ushort)x).ToArray();

		public static string? ValidateOptions(TarpitServiceOptions options)
		{
			if (options.ListenAddress is null)
				return $"{nameof(options.ListenAddress)} should not be null";
			if (!IPAddress.TryParse(options.ListenAddress, out _))
				return $"{nameof(options.ListenAddress)} is an invalid ipaddress";
			if (options.PortRangeBegin > options.PortRangeEnd)
				return $"{nameof(options.PortRangeEnd)} should be greater than or equal to {nameof(options.PortRangeBegin)}";
			if (options.TimeoutInSeconds <= 0)
				return $"{nameof(options.TimeoutInSeconds)} should be greater than 0";
			if (options.UpdateIntervalInSeconds <= 0)
				return $"{nameof(options.UpdateIntervalInSeconds)} should be greater than 0";
			if (options.WriteIntervalInMs <= 0)
				return $"{nameof(options.WriteIntervalInMs)} should be greater than 0";
			if (options.WriteSize <= 0)
				return $"{nameof(options.WriteSize)} should be greater than 0";

			return null;
		}

		public static TarpitServiceOptions GetDefaultOptions() => new()
		{
			ListenAddress = "0.0.0.0",
			PortRangeBegin = 8001,
			PortRangeEnd = 9000,
			WriteIntervalInMs = 200,
			WriteSize = 2,
			TimeoutInSeconds = 600,
			UpdateIntervalInSeconds = 5
		};
	}
}
