using System;
using System.IO;

namespace Code2.Net.TcpTarpit.Internals
{
	internal class FileSystem : IFileSystem
	{
		public string PathGetFullPath(string path)
			=> Path.GetFullPath(path);

		public byte[] FileReadAllBytes(string path)
			=> File.ReadAllBytes(path);
	}
}
