using System.Net;

namespace Code2.Net.TcpTarpit
{
	public class TarpitServiceOptions
	{
		public string? ListenAddress { get; set; }
		public ushort PortRangeBegin { get; set; }
		public ushort PortRangeEnd { get; set; }
		public bool UseIPv4Only { get; set; }
		public int WriteIntervalInMs { get; set; }
		public int WriteSize { get; set; }
		public int UpdateIntervalInSeconds { get; set; }
		public int TimeoutInSeconds { get; set; }
		public string? ResponseFile { get; set; }
		public string? ResponseText { get; set; }
	}
}
