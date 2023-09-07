namespace Code2.Net.TcpTarpit
{
	public interface IByteReaderFactory
	{
		IByteReader Create();
		IByteReader Create(byte[] data);
		IByteReader Create(string? filePath);
	}
}
