namespace Code2.Net.TcpTarpit
{
	public interface IByteReader
	{
		int Read(byte[] buffer, int position);
		void Read(byte[] buffer);
	}
}