using Code2.Net.TcpTarpit.Internals;
using Code2.Net.TcpTarpit.Internals.Net;
using System.Net;
using System.Text;

namespace Code2.Net.TcpTarpit
{
	public class TarpitService : ITarpitService
	{
		public TarpitService() : this(GetDefaultOptions()) { }
		public TarpitService(TarpitServiceOptions options) : this(options, new InfiniteReaderFactory()) { }
		public TarpitService(TarpitServiceOptions options, IByteReaderFactory byteReaderFactory)
			: this(options, byteReaderFactory, new SocketFactory()) { }
		internal TarpitService(TarpitServiceOptions options, IByteReaderFactory byteReaderFactory, ISocketFactory socketFactory)
		{
			_options = options;
			_readerFactory = byteReaderFactory;
			_socketFactory = socketFactory;
		}

		private object _lock = new();
		private ISocket[]? _listeners;
		private Timer? _timerConnectionUpdate;
		private DateTime _nextConnectionsUpdate;
		private int _connectionId;
		private bool _isUpdating;
		private IByteReader _reader = default!;

		private readonly IByteReaderFactory _readerFactory;
		private readonly TarpitServiceOptions _options;
		private readonly ISocketFactory _socketFactory;
		private readonly IList<SocketConnection> _connections = new List<SocketConnection>();

		public event EventHandler<UnhandledExceptionEventArgs>? Error;
		public event EventHandler<ConnectionCreatedEventArgs>? ConnectionCreated;
		public event EventHandler<ConnectionsUpdatedEventArgs>? ConnectionsUpdated;

		public TarpitServiceOptions Options => _options;
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

			_reader = _options.ResponseText is null ?
				_readerFactory.Create(_options.ResponseFile) :
				_readerFactory.Create(Encoding.UTF8.GetBytes(_options.ResponseText));

			_nextConnectionsUpdate = DateTime.Now.AddSeconds(_options.UpdateIntervalInSeconds);
			ushort[] ports = GetPortsFromString(_options.Ports!);
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

			Parallel.ForEach(connections, x => UpdateSocketConnection(x, true));
			OnConnectionsUpdated(connections);
		}

		public static ITarpitService Configure(Action<TarpitServiceOptions> optionsAction)
		{
			TarpitServiceOptions options = GetDefaultOptions();
			optionsAction(options);
			return new TarpitService(options);
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
				SocketConnection[] completedConnections = connections.Where(x => x.Connection.IsCompleted).ToArray();
				foreach (SocketConnection connection in completedConnections)
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

			SocketConnection[] connections = GetConnections();
			Parallel.ForEach(connections, x => UpdateSocketConnection(x));

			SocketConnection[] activeConnections = connections.Where(x => !x.Connection.IsCompleted).ToArray();
			Parallel.ForEach(activeConnections, x => {
				x.Connection.ReaderPosition = _reader.Read(x.Connection.Buffer, x.Connection.ReaderPosition);
				x.Connection.BytesSent += TrySendData(x); 
			});
			Parallel.ForEach(activeConnections, x => UpdateSocketConnection(x));

			if (_nextConnectionsUpdate <= DateTime.Now && connections.Length > 0)
			{
				OnConnectionsUpdated(connections);
				_nextConnectionsUpdate = DateTime.Now.AddSeconds(_options.UpdateIntervalInSeconds);
			}
			_isUpdating = false;
		}

		private SocketConnection[] GetConnections()
		{
			lock (_lock)
			{
				return _connections.ToArray();
			}
		}

		private void UpdateSocketConnection(SocketConnection sc, bool closeConnection = false)
		{
			if ((sc.Connection.DurationInSeconds >= Options.TimeoutInSeconds || closeConnection) && sc.Socket.Connected) 
				sc.Socket.Close();

			sc.Connection.DurationInSeconds = (int)Math.Round((DateTime.Now - sc.Connection.Created).TotalSeconds);
			sc.Connection.IsCompleted = !sc.Socket.Connected;
		}

		private int TrySendData(SocketConnection sc)
		{
			try
			{
				sc.Socket.Send(sc.Connection.Buffer);
				return sc.Connection.Buffer.Length;
			}
			catch
			{
				sc.Socket.Close();
			}
			return 0;
		}

		private void CreateSocketConnection(ISocket socket)
		{
			socket.SendBufferSize = _options.WriteSize;
			ConnectionStatus connection = new ConnectionStatus();
			connection.Created = DateTime.Now;
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
				CreateSocketConnection(client);
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

		private ushort[] GetPortsFromString(string portsString)
			=> portsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.SelectMany(GetPortsFromSegmentString).Where(x => x != 0).Distinct().ToArray();


		private ushort[] GetPortsFromSegmentString(string segmentString)
		{
			if (segmentString.IndexOf('-') != -1)
			{
				ushort[] ports = segmentString.Split('-', 2, StringSplitOptions.TrimEntries).Select(GetUshortFromString).ToArray();
				if (ports[0] > ports[1] || ports[0] == 0 || ports[1] == 0)
				{
					OnError(new InvalidOperationException($"Invalid port range {segmentString}"));
					return Array.Empty<ushort>();
				}
				return Enumerable.Range(ports[0], 1 + ports[1] - ports[0]).Select(x => (ushort)x).ToArray();
			}
			else
			{
				ushort port = GetUshortFromString(segmentString);
				if (port == 0)
				{
					OnError(new InvalidOperationException($"Invalid port {segmentString}"));
					return Array.Empty<ushort>();
				}
				else
				{
					return new[] { port };
				}
			}
		}

		private ushort GetUshortFromString(string ushortString)
		{
			ushort n;
			return ushort.TryParse(ushortString, out n) ? n : default;
		}

		public static string? ValidateOptions(TarpitServiceOptions options)
		{
			if (string.IsNullOrEmpty(options.ListenAddress))
				return $"{nameof(options.ListenAddress)} should not be null or empty";
			if (!IPAddress.TryParse(options.ListenAddress, out _))
				return $"{nameof(options.ListenAddress)} is not an invalid ipaddress";
			if (string.IsNullOrEmpty(options.Ports))
				return $"{nameof(options.Ports)} should not be null or empty";
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
			Ports = "8001-9000",
			WriteIntervalInMs = 200,
			WriteSize = 2,
			TimeoutInSeconds = 600,
			UpdateIntervalInSeconds = 5
		};
	}
}
