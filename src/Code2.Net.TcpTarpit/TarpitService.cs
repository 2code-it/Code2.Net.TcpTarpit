using Code2.Net.TcpTarpit.Internals;
using Code2.Net.TcpTarpit.Internals.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Code2.Net.TcpTarpit
{
	public class TarpitService : ITarpitService
	{
		public TarpitService() : this(null) { }
		public TarpitService(TarpitServiceOptions? options) : this(options, new InfiniteReaderFactory()) { }
		public TarpitService(TarpitServiceOptions? options, IByteReaderFactory byteReaderFactory)
			: this(options, byteReaderFactory, new SocketFactory()) { }
		internal TarpitService(TarpitServiceOptions? options, IByteReaderFactory byteReaderFactory, ISocketFactory socketFactory)
		{
			_readerFactory = byteReaderFactory;
			_socketFactory = socketFactory;
			if (options is not null) Configure(options);
		}

		private readonly object _lock = new();
		private ISocket[]? _listeners;
		private Timer? _timerConnectionUpdate;
		private DateTime _nextConnectionsUpdate;
		private int _connectionId;
		private bool _isUpdating;
		private IByteReader _reader = default!;

		private readonly IByteReaderFactory _readerFactory;
		private readonly TarpitServiceOptions _options = GetDefaultOptions();
		private readonly ISocketFactory _socketFactory;
		private readonly List<SocketConnection> _connections = new List<SocketConnection>();

		public event EventHandler<UnhandledExceptionEventArgs>? Error;
		public event EventHandler<ConnectionCreatedEventArgs>? ConnectionCreated;
		public event EventHandler<ConnectionsUpdatedEventArgs>? ConnectionsUpdated;

		public TarpitServiceOptions Options => _options;
		public int ConnectionsCount => _connections.Count;
		public int ListenersCount => _listeners?.Length ?? 0;

		public int Start()
		{
			_reader = _options.ResponseText is null ?
				_readerFactory.Create(_options.ResponseFile) :
				_readerFactory.Create(Encoding.UTF8.GetBytes(_options.ResponseText));

			_nextConnectionsUpdate = DateTime.Now.AddSeconds(_options.UpdateIntervalInSeconds!.Value);
			ushort[] ports = GetPortsFromString(_options.Ports!);
			IPAddress listenAddress = IPAddress.Parse(_options.ListenAddress!);
			_listeners = ports.Select(x => TryGetStartedListener(listenAddress, x))
				.Where(x => x is not null).ToArray()!;
			_timerConnectionUpdate = new Timer(new TimerCallback(OnTimerConnectionUpdate), null, 0, _options.WriteIntervalInMs!.Value);
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

		public void Configure(Action<TarpitServiceOptions> optionsAction)
		{
			TarpitServiceOptions options = new();
			optionsAction(options);
			Configure(options);
		}

		public void Configure(TarpitServiceOptions options)
		{
			if (ListenersCount > 0) throw new InvalidOperationException("Can't configure running service");
			string? validationError = ValidateOptions(options);
			if (validationError is not null) throw new InvalidOperationException($"Invalid options {validationError}");

			if (options.ListenAddress is not null) _options.ListenAddress = options.ListenAddress;
			if (options.Ports is not null) _options.Ports = options.Ports;
			if (options.UseIPv4Only.HasValue) _options.UseIPv4Only = options.UseIPv4Only.Value;
			if (options.WriteIntervalInMs.HasValue) _options.WriteIntervalInMs = options.WriteIntervalInMs.Value;
			if (options.WriteSize.HasValue) _options.WriteSize = options.WriteSize.Value;
			if (options.UpdateIntervalInSeconds.HasValue) _options.UpdateIntervalInSeconds = options.UpdateIntervalInSeconds.Value;
			if (options.TimeoutInSeconds.HasValue) _options.TimeoutInSeconds = options.TimeoutInSeconds.Value;
			if (options.ResponseFile is not null) _options.ResponseFile = options.ResponseFile;
			if (options.ResponseText is not null) _options.ResponseText = options.ResponseText;
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

		private void OnError(string message, bool isTerminating = false)
			=> OnError(new InvalidOperationException(message), isTerminating);


		private void OnTimerConnectionUpdate(object? state)
		{
			if (_isUpdating) return;
			_isUpdating = true;

			SocketConnection[] connections = GetConnections();
			Parallel.ForEach(connections, x => UpdateSocketConnection(x));

			SocketConnection[] activeConnections = connections.Where(x => !x.Connection.IsCompleted).ToArray();
			Parallel.ForEach(activeConnections, x =>
			{
				x.Connection.ReaderPosition = _reader.Read(x.Connection.Buffer, x.Connection.ReaderPosition);
				x.Connection.BytesSent += TrySendData(x);
			});
			Parallel.ForEach(activeConnections, x => UpdateSocketConnection(x));

			if (_nextConnectionsUpdate <= DateTime.Now && connections.Length > 0)
			{
				OnConnectionsUpdated(connections);
				_nextConnectionsUpdate = DateTime.Now.AddSeconds(_options.UpdateIntervalInSeconds!.Value);
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

		private static int TrySendData(SocketConnection sc)
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
			socket.SendBufferSize = _options.WriteSize!.Value;
			ConnectionStatus connection = new ConnectionStatus();
			connection.Created = DateTime.Now;
			connection.Buffer = new byte[_options.WriteSize.Value];
			connection.LocalEndPoint = socket.LocalEndPoint?.ToString()!;
			connection.RemoteEndPoint = socket.RemoteEndPoint?.ToString()!;
			connection.Id = GetNextConnectionId();
			OnConnectionCreated(new SocketConnection(socket, connection));
		}

		private ISocket? TryGetStartedListener(IPAddress ipAddress, ushort port)
		{
			IPEndPoint endpoint = new IPEndPoint(ipAddress, port);
			ISocket listener = _socketFactory.CreateTcpStream(_options.UseIPv4Only!.Value);

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

		private static string? ValidatePortsString(string portsString)
		{
			string[] segments = GetPortsStringSegments(portsString);
			foreach (string segment in segments)
			{
				if (segment.Contains('-'))
				{
					ushort[] ports = segment.Split('-').Take(2).Select(x => x.Trim()).Select(GetUshortFromString).ToArray();
					if (ports[0] > ports[1] || ports[0] == 0 || ports[1] == 0) return $"Invalid port range '{segment}'";
				}
				else
				{
					ushort port = GetUshortFromString(segment);
					if (port == 0) return $"Invalid port {segment}";
				}
			}
			return null;
		}

		private static ushort[] GetPortsFromString(string portsString)
		{
			List<ushort> ports = new List<ushort>();
			string[] segments = GetPortsStringSegments(portsString);
			foreach (string segment in segments)
			{
				if (segment.Contains('-'))
				{
					ports.AddRange(GetPortsFromRangeString(segment));
				}
				else
				{
					ports.Add(GetUshortFromString(segment));
				}
			}

			return ports.Where(x => x != 0).Distinct().ToArray();
		}

		private static string[] GetPortsStringSegments(string portsString)
			=> portsString.Split(',').Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

		private static ushort[] GetPortsFromRangeString(string rangeString)
		{
			ushort[] range = rangeString.Split('-').Take(2).Select(x => x.Trim()).Select(GetUshortFromString).ToArray();
			return Enumerable.Range(range[0], 1 + range[1] - range[0]).Select(x => (ushort)x).ToArray();
		}

		private static ushort GetUshortFromString(string ushortString)
			=> ushort.TryParse(ushortString, out ushort n) ? n : default;


		public static string? ValidateOptions(TarpitServiceOptions options)
		{
			if (options.ListenAddress is not null && !IPAddress.TryParse(options.ListenAddress, out _))
				return $"{nameof(options.ListenAddress)} is not a valid ipaddress";

			if (!string.IsNullOrEmpty(options.Ports))
			{
				string? portsError = ValidatePortsString(options.Ports!);
				if (portsError is not null) return $"{nameof(options.Ports)} invalid {portsError}";
			}

			if (options.TimeoutInSeconds.HasValue && options.TimeoutInSeconds.Value <= 0)
				return $"{nameof(options.TimeoutInSeconds)} should be greater than 0";

			if (options.UpdateIntervalInSeconds.HasValue && options.UpdateIntervalInSeconds.Value <= 0)
				return $"{nameof(options.UpdateIntervalInSeconds)} should be greater than 0";

			if (options.WriteIntervalInMs.HasValue && options.WriteIntervalInMs.Value <= 0)
				return $"{nameof(options.WriteIntervalInMs)} should be greater than 0";

			if (options.WriteSize.HasValue && options.WriteSize.Value <= 0)
				return $"{nameof(options.WriteSize)} should be greater than 0";

			return null;
		}

		public static TarpitServiceOptions GetDefaultOptions() => new()
		{
			ListenAddress = "0.0.0.0",
			Ports = "8001-9000",
			UseIPv4Only = false,
			WriteIntervalInMs = 200,
			WriteSize = 2,
			TimeoutInSeconds = 600,
			UpdateIntervalInSeconds = 5,
			ResponseText = "HTTP/1.1 200 OK\r\nServer: nginx\r\nContent-Type: text/plain; charset=utf-8\r\nConnection: keep-alive\r\n\r\n00000000 00000000 00000000 00000000 00000000 00000000"
		};
	}
}
