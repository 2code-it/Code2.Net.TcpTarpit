
using System;

namespace Code2.Net.TcpTarpit
{
	public class ConnectionStatus
	{
		public int Id { get; set; }
		public DateTime Created { get; set; }
		public int BytesSent { get; set; }
		public string LocalEndPoint { get; set; } = string.Empty;
		public string RemoteEndPoint { get; set; } = string.Empty;
		public int DurationInSeconds { get; set; }
		public bool IsCompleted { get; set; }
	}
}
