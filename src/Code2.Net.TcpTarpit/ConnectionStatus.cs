
namespace Code2.Net.TcpTarpit
{
	public class ConnectionStatus
	{
		public int Id { get; set; }
		public DateTime Created { get; set; }
		public DateTime End { get; set; }
		public byte[] Buffer { get; set; } = Array.Empty<byte>();
		public int BytesSent { get; set; }
		public string LocalEndPoint { get; set; } = default!;
		public string RemoteEndPoint { get; set; } = default!;
		public int ReaderPosition { get; set; }
		public bool IsCompleted { get; set; }
	}
}
