﻿using Code2.Net.TcpTarpit.Internals.Net;
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
		private readonly Dictionary<ushort, ISocket> _listeners = new Dictionary<ushort, ISocket>();
		private Timer? _timerConnectionUpdate;
		private DateTime _nextConnectionsUpdate;
		private int _connectionId;
		private bool _isUpdating;
		private IByteReader _reader = default!;

		private readonly IByteReaderFactory _readerFactory;
		private readonly TarpitServiceOptions _options = GetDefaultOptions();
		private readonly ISocketFactory _socketFactory;
		private readonly List<TarpitConnection> _connections = new List<TarpitConnection>();

		public event EventHandler<UnhandledExceptionEventArgs>? Error;
		public event EventHandler<ConnectionCreatedEventArgs>? ConnectionCreated;
		public event EventHandler<ConnectionsUpdatedEventArgs>? ConnectionsUpdated;

		public TarpitServiceOptions Options => _options;
		public int ConnectionsCount => _connections.Count;
		public int ListenersCount => _listeners?.Count ?? 0;


		public int Start()
		{
			_reader = _options.ResponseText is null ?
				_readerFactory.Create(_options.ResponseFile) :
				_readerFactory.Create(Encoding.UTF8.GetBytes(_options.ResponseText));

			_nextConnectionsUpdate = DateTime.Now.AddSeconds(_options.UpdateIntervalInSeconds!.Value);
			ushort[] ports = GetPortsFromString(_options.Ports!);
			AddListeners(ports);
			_timerConnectionUpdate = new Timer(new TimerCallback(OnTimerConnectionUpdate), null, 0, _options.WriteIntervalInMs!.Value);
			return _listeners.Count;
		}

		public void Stop()
		{
			_timerConnectionUpdate?.Dispose();
			Parallel.ForEach(_listeners.Values, x => x.Dispose());
			_listeners.Clear();
			TarpitConnection[] connections = _connections.ToArray();
			_connections.Clear();

			Parallel.ForEach(connections, x => x.Close());
			OnConnectionsUpdated(connections);
		}

		public ConnectionStatus[] GetCurrentConnections()
			=> GetConnections().Select(x => x.Status).ToArray();

		public void CloseConnection(int connectionId)
		{
			_connections.FirstOrDefault(x => x.Id == connectionId)?.Close();
		}

		public void RemoveListener(ushort port)
		{
			if (!_listeners.ContainsKey(port)) return;
			_listeners[port].Dispose();
			_listeners.Remove(port);
		}

		public void AddListener(ushort port)
			=> AddListeners(new[] { port });

		public void AddListeners(IEnumerable<ushort> ports)
		{
			IPAddress listenAddress = IPAddress.Parse(_options.ListenAddress!);
			foreach (var port in ports)
			{
				var listener = TryGetStartedListener(listenAddress, port);
				if (listener is null) continue;
				_listeners.Add(port, listener);
			}
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
			if (options.SendTimeoutInMs.HasValue) _options.SendTimeoutInMs = options.SendTimeoutInMs.Value;
			if (options.ResponseFile is not null)
			{
				_options.ResponseFile = options.ResponseFile;
				_options.ResponseText = null;
			}
			if (options.ResponseText is not null)
			{
				_options.ResponseText = options.ResponseText;
				_options.ResponseFile = null;
			}
		}

		private void OnConnectionCreated(TarpitConnection connection)
		{
			lock (_lock)
			{
				_connections.Add(connection);
			}
			ConnectionCreated?.Invoke(this, new ConnectionCreatedEventArgs(connection.Status));
		}

		private void OnConnectionsUpdated(TarpitConnection[] connections)
		{
			lock (_lock)
			{
				TarpitConnection[] completedConnections = connections.Where(x => x.Status.IsCompleted).ToArray();
				foreach (TarpitConnection connection in completedConnections)
				{
					_connections.Remove(connection);
				}
			}
			ConnectionsUpdated?.Invoke(this, new ConnectionsUpdatedEventArgs(connections.Select(x => x.Status).ToArray()));
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

			TarpitConnection[] connections = GetConnections();
			Parallel.ForEach(connections, x => x.Update());
			TarpitConnection[] activeConnections = connections.Where(x => !x.Status.IsCompleted).ToArray();
			Parallel.ForEach(connections, x => x.Send());

			if (_nextConnectionsUpdate <= DateTime.Now && connections.Length > 0)
			{
				OnConnectionsUpdated(connections);
				_nextConnectionsUpdate = DateTime.Now.AddSeconds(_options.UpdateIntervalInSeconds!.Value);
			}
			_isUpdating = false;
		}

		private TarpitConnection[] GetConnections()
		{
			lock (_lock)
			{
				return _connections.ToArray();
			}
		}

		private void CreateSocketConnection(ISocket socket)
		{
			socket.SendBufferSize = _options.WriteSize!.Value;
			socket.SendTimeout = _options.SendTimeoutInMs!.Value;
			TarpitConnection connection = new TarpitConnection(socket, _reader, _options.TimeoutInSeconds!.Value);
			connection.Status.LocalEndPoint = socket.LocalEndPoint?.ToString()!;
			connection.Status.RemoteEndPoint = socket.RemoteEndPoint?.ToString()!;
			connection.Id = GetNextConnectionId();
			OnConnectionCreated(connection);
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
			SendTimeoutInMs = 2000,
			UpdateIntervalInSeconds = 5,
			ResponseText = "HTTP/1.1 200 OK\r\nServer: nginx\r\nContent-Type: text/plain; charset=utf-8\r\nConnection: keep-alive\r\n\r\n00000000 00000000 00000000 00000000 00000000 00000000"
		};
	}
}
