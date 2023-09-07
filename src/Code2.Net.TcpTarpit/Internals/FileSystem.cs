namespace Code2.Net.TcpTarpit.Internals
{
	internal class FileSystem : IFileSystem
	{
		public string PathGetFullPath(string path)
			=> Path.GetFullPath(path, AppDomain.CurrentDomain.BaseDirectory);

		public byte[] FileReadAllBytes(string path)
			=> File.ReadAllBytes(path);
	}
}
