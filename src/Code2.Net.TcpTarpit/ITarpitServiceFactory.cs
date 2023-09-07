namespace Code2.Net.TcpTarpit
{
	public interface ITarpitServiceFactory
	{
		ITarpitService Create();
		ITarpitService Create(TarpitServiceOptions options);
		ITarpitService Create(TarpitServiceOptions options, IByteReaderFactory byteReaderFactory);
	}
}