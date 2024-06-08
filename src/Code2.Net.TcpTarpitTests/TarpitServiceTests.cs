using Code2.Net.TcpTarpit.Internals.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Code2.Net.TcpTarpit.Tests
{
	[TestClass]
	public class TarpitServiceTests
	{
		private ISocket _socket = default!;
		private ISocket _listener = default!;
		private ISocketFactory _socketFactory = default!;
		private IAsyncResult _asyncResult = default!;
		private List<AsyncCallback> _asyncCallbacks = new List<AsyncCallback>();
		private IByteReader _byteReader = default!;
		private IByteReaderFactory _byteReaderFactory = default!;
		private object _lock = new();


		[TestMethod]
		public void Start_When_PortRangeSpans100_Expect_100Listeners()
		{
			ResetSubstitutes();
			var options = TarpitService.GetDefaultOptions();
			options.Ports = "1-100";
			TarpitService tarpitService = new TarpitService(options, _byteReaderFactory, _socketFactory);

			int result = tarpitService.Start();

			Assert.AreEqual(100, result);
		}

		[TestMethod]
		public void Start_When_ListenerErrorWithErrorHandler_Expect_ListenersMinusErrorous()
		{
			ResetSubstitutes();
			var options = TarpitService.GetDefaultOptions();
			options.Ports = "1-100";
			ISocket errorListener = Substitute.For<ISocket>();
			errorListener.When(x => x.Listen()).Do(x => { throw new Exception(); });
			int i = 0;
			_socketFactory.CreateTcpStream().Returns(x => { i++; return i % 2 == 0 ? errorListener : _listener; });
			TarpitService tarpitService = new TarpitService(options, _byteReaderFactory, _socketFactory);
			tarpitService.Error += (s, e) => { };

			int result = tarpitService.Start();

			Assert.AreEqual(50, result);
		}

		[TestMethod]
		public void Start_When_ConnectionsCreated_Expect_UniqueConnectionIds()
		{
			ResetSubstitutes();
			var options = TarpitService.GetDefaultOptions();
			options.Ports = "1-100";
			TarpitService tarpitService = new TarpitService(options, _byteReaderFactory, _socketFactory);
			List<ConnectionStatus> connections = new List<ConnectionStatus>();
			tarpitService.ConnectionCreated += (s, e) => { lock (_lock) { connections.Add(e.Connection); } };

			tarpitService.Start();
			Parallel.ForEach(_asyncCallbacks, x => x.Invoke(_asyncResult));
			tarpitService.Stop();

			var ids = connections.Select(x => x.Id).ToArray();
			var uniques = ids.GroupBy(x => x).Select(x => x.Key).ToArray();
			Assert.AreEqual(ids.Length, uniques.Length);
		}

		[TestMethod]
		public void Start_When_ConnectionCreated_Expect_OptionValuesInProperties()
		{
			ResetSubstitutes();
			var options = TarpitService.GetDefaultOptions();
			options.Ports = "1";
			options.WriteSize = 10;
			options.SendTimeoutInMs = 3000;
			TarpitService tarpitService = new TarpitService(options, _byteReaderFactory, _socketFactory);
			ConnectionStatus connection = default!;
			tarpitService.ConnectionCreated += (s, e) => { connection = e.Connection; };

			int listenerCount = tarpitService.Start();
			Parallel.ForEach(_asyncCallbacks, x => x.Invoke(_asyncResult));
			tarpitService.Stop();

			Assert.AreEqual(1, listenerCount);
			Assert.AreEqual(options.WriteSize, _socket.SendBufferSize);
			Assert.AreEqual(options.SendTimeoutInMs, _socket.SendTimeout);
		}

		[TestMethod]
		public void Start_When_RemoteDisconnect_Expect_ConnectionsCompleted()
		{
			ResetSubstitutes();
			var options = TarpitService.GetDefaultOptions();
			options.Ports = "1";
			options.WriteIntervalInMs = 10;
			TarpitService tarpitService = new TarpitService(options, _byteReaderFactory, _socketFactory);
			ConnectionStatus[] connections = default!;
			tarpitService.ConnectionsUpdated += (s, e) => { connections = e.Connections; };
			_socket.Connected.Returns(false);

			tarpitService.Start();
			Parallel.ForEach(_asyncCallbacks, x => x.Invoke(_asyncResult));
			Thread.Sleep(100);
			tarpitService.Stop();

			Assert.AreEqual(1, connections.Length);
			Assert.IsTrue(connections[0].IsCompleted);
		}

		[TestMethod]
		public void Start_When_SocketSendError_Expect_ConnectionsCompleted()
		{
			ResetSubstitutes();
			var options = TarpitService.GetDefaultOptions();
			options.Ports = "1";
			options.WriteIntervalInMs = 10;
			TarpitService tarpitService = new TarpitService(options, _byteReaderFactory, _socketFactory);
			ConnectionStatus[] connections = default!;
			tarpitService.ConnectionsUpdated += (s, e) => { connections = e.Connections; };
			int i = 0;
			_socket.Connected.Returns(x => ++i < 2);
			_socket.When(x => x.Send(Arg.Any<byte[]>())).Do(x => { throw new Exception(); });
			_byteReader.Read(Arg.Any<byte[]>(), Arg.Any<int>()).Returns(1);

			tarpitService.Start();
			Parallel.ForEach(_asyncCallbacks, x => x.Invoke(_asyncResult));
			tarpitService.Stop();


			Assert.AreEqual(1, connections.Length);
			Assert.IsTrue(connections[0].IsCompleted);
		}

		[TestMethod]
		public void Start_When_SocketSend_Expect_ConnectionsUpdate()
		{
			ResetSubstitutes();
			int readerPosition = 10;
			var options = TarpitService.GetDefaultOptions();
			options.Ports = "1";
			options.WriteIntervalInMs = 400;
			options.WriteSize = 10;
			options.UpdateIntervalInSeconds = 1;
			TarpitService tarpitService = new TarpitService(options, _byteReaderFactory, _socketFactory);
			ConnectionStatus[] connections = default!;
			tarpitService.ConnectionsUpdated += (s, e) => { connections = e.Connections; };
			int writes = 0;
			_socket.When(x => x.Send(Arg.Any<byte[]>())).Do(x => writes++);
			_socket.Connected.Returns(true);
			_byteReader.Read(Arg.Any<byte[]>(), Arg.Any<int>()).Returns(readerPosition);

			tarpitService.Start();
			_asyncCallbacks[0].Invoke(_asyncResult);
			Thread.Sleep(1200);
			tarpitService.Stop();

			Assert.AreEqual(1, connections.Length);
			Assert.AreEqual(options.WriteSize * writes, connections[0].BytesSent);
		}



		private void ResetSubstitutes()
		{
			IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
			_byteReader = Substitute.For<IByteReader>();
			_byteReaderFactory = Substitute.For<IByteReaderFactory>();
			_byteReaderFactory.Create(Arg.Any<string?>()).Returns(_byteReader);
			_byteReaderFactory.Create(Arg.Any<byte[]>()).Returns(_byteReader);
			_listener = Substitute.For<ISocket>();
			_socket = Substitute.For<ISocket>();
			_socket.LocalEndPoint.Returns(endPoint);
			_socket.RemoteEndPoint.Returns(endPoint);
			_socketFactory = Substitute.For<ISocketFactory>();
			_asyncResult = Substitute.For<IAsyncResult>();
			_asyncResult.AsyncState.Returns(_listener);

			_listener.BeginAccept(Arg.Do<AsyncCallback?>(x => { lock (_lock) { _asyncCallbacks.Add(x!); } }), Arg.Any<object?>());
			_listener.EndAccept(Arg.Any<IAsyncResult>()).Returns(_socket);
			_socketFactory.CreateTcpStream().Returns(_listener);
		}
	}
}