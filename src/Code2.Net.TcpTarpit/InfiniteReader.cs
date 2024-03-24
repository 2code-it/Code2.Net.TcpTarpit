using System;

namespace Code2.Net.TcpTarpit
{
	public class InfiniteReader : IByteReader
	{
		public InfiniteReader(byte[] data)
		{
			_data = data;
		}

		private int _position = 0;
		private readonly byte[] _data;

		public int Read(byte[] buffer, int position)
		{
			if (_data.Length == 0) throw new InvalidOperationException("No data available");
			int length;
			int bytesToRead = buffer.Length;
			int bufferPosition = 0;
			while (bytesToRead > 0)
			{
				length = position + bytesToRead > _data.Length ? _data.Length - position : bytesToRead;
				Array.Copy(_data, position, buffer, bufferPosition, length);
				position = position + length == _data.Length ? 0 : position + length;
				bytesToRead -= length;
				bufferPosition += length;
			}
			return position;
		}

		public void Read(byte[] buffer)
			=> _position = Read(buffer, _position);

	}
}
