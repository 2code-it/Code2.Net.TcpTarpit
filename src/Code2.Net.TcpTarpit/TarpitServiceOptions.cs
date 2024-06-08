namespace Code2.Net.TcpTarpit
{
	public class TarpitServiceOptions
	{
		public string? ListenAddress { get; set; }
		public string? Ports { get; set; }
		public bool? UseIPv4Only { get; set; }
		public int? WriteIntervalInMs { get; set; }
		public int? WriteSize { get; set; }
		public int? UpdateIntervalInSeconds { get; set; }
		public int? TimeoutInSeconds { get; set; }
		public int? SendTimeoutInMs { get; set; }
		public string? ResponseFile { get; set; }
		public string? ResponseText { get; set; }
	}
}
