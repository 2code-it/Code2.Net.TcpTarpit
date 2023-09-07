namespace Code2.Net.TcpTarpit
{
	public class TarpitServiceFactory : ITarpitServiceFactory
	{
		public ITarpitService Create()
			=> Create(TarpitService.GetDefaultOptions());

		public ITarpitService Create(TarpitServiceOptions options)
			=> new TarpitService(options, new InfiniteReaderFactory());

		public ITarpitService Create(TarpitServiceOptions options, IByteReaderFactory byteReaderFactory)
			=> new TarpitService(options, byteReaderFactory);
	}
}
