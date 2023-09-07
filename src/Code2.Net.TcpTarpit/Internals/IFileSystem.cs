namespace Code2.Net.TcpTarpit.Internals
{
	internal interface IFileSystem
	{
		byte[] FileReadAllBytes(string path);
		string PathGetFullPath(string path);
	}
}