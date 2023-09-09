using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Code2.Net.TcpTarpit.Tests
{
	[TestClass]
	public class InfiniteReaderTests
	{
		[TestMethod]
		public void Read_When_BufferLengthExceedsDataLength_Expect_ContinuesData()
		{
			byte[] data = Enumerable.Range(80, 10).Select(x => (byte)x).ToArray();
			InfiniteReader reader = new InfiniteReader(data);
			byte[] buffer = new byte[24];

			reader.Read(buffer);

			Assert.AreEqual(81, buffer[11]);
		}

		[TestMethod]
		public void Read_When_UsingSingleByteBuffer_Expect_BufferSet()
		{
			byte[] data = Enumerable.Range(80, 10).Select(x => (byte)x).ToArray();
			InfiniteReader reader = new InfiniteReader(data);
			byte[] buffer = new byte[1];

			reader.Read(buffer);

			Assert.AreEqual(80, buffer[0]);
		}

		[TestMethod]
		public void Read_When_BufferSizeEqualsDataSize_Expect_BufferSet()
		{
			byte[] data = Enumerable.Range(80, 10).Select(x => (byte)x).ToArray();
			InfiniteReader reader = new InfiniteReader(data);
			byte[] buffer = new byte[10];

			reader.Read(buffer);

			Assert.AreEqual(80, buffer[0]);
			Assert.AreEqual(89, buffer[9]);
		}

		[TestMethod]
		public void Read_When_MultipleBuffers_Expect_ContinuesData()
		{
			byte[] data = Enumerable.Range(80, 10).Select(x => (byte)x).ToArray();
			InfiniteReader reader = new InfiniteReader(data);
			byte[] buffer1 = new byte[3];
			byte[] buffer2 = new byte[8];
			byte[] buffer3 = new byte[12];

			reader.Read(buffer1); //[80,81,82]
			reader.Read(buffer2); //[83,..,80]
			reader.Read(buffer3); //[81,..,82]

			Assert.AreEqual(80, buffer1[0]);
			Assert.AreEqual(80, buffer2[7]);
			Assert.AreEqual(83, buffer3[2]);
		}
	}
}