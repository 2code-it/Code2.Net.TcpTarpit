using Code2.Net.TcpTarpit.Internals;

namespace Code2.Net.TcpTarpit
{
	public class InfiniteReaderFactory : IByteReaderFactory
	{
		public InfiniteReaderFactory() : this(new FileSystem()) { }
		internal InfiniteReaderFactory(IFileSystem fileSystem)
		{
			_fileSystem = fileSystem;
		}

		private readonly IFileSystem _fileSystem;


		public IByteReader Create(byte[] data)
			=> new InfiniteReader(data);

		public IByteReader Create()
			=> Create(GetRandomBytes());

		public IByteReader Create(string? filePath)
			=> filePath is null ? Create() : Create(_fileSystem.FileReadAllBytes(_fileSystem.PathGetFullPath(filePath)));


		private static byte[] GetRandomBytes()
			=> Enumerable.Range(0, 1024).Select(x => (byte)Random.Shared.Next(97, 122)).ToArray();
	}
}
