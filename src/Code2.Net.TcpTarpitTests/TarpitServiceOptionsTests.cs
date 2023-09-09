using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Code2.Net.TcpTarpit.Tests
{
	[TestClass]
	public class TarpitServiceOptionsTests
	{
		[TestMethod]
		public void GetDefault_When_DefaultOptions_Expect_ValidResult()
		{
			TarpitServiceOptions options = TarpitService.GetDefaultOptions();

			string? result = TarpitService.ValidateOptions(options);

			Assert.IsNull(result);
		}

		[TestMethod]
		public void Validate_When_ListenAddressIsInvalid_Expect_InvalidResult()
		{
			TarpitServiceOptions options = TarpitService.GetDefaultOptions();
			options.ListenAddress = "0.1.a";

			string? result = TarpitService.ValidateOptions(options);

			Assert.IsNotNull(result);
			Assert.IsTrue(result.Contains(nameof(TarpitServiceOptions.ListenAddress)));
		}

		[TestMethod]
		public void Validate_When_ListenAddressIsNull_Expect_InvalidResult()
		{
			TarpitServiceOptions options = TarpitService.GetDefaultOptions();
			options.ListenAddress = null;

			string? result = TarpitService.ValidateOptions(options);

			Assert.IsNotNull(result);
			Assert.IsTrue(result.Contains(nameof(TarpitServiceOptions.ListenAddress)));
		}

		[TestMethod]
		public void Validate_When_PortsIsNotDefined_Expect_InvalidResult()
		{
			TarpitServiceOptions options = TarpitService.GetDefaultOptions();
			options.Ports = null;

			string? result = TarpitService.ValidateOptions(options);

			Assert.IsNotNull(result);
			Assert.IsTrue(result.Contains(nameof(TarpitServiceOptions.Ports)));
		}

		[TestMethod]
		public void Validate_When_TimeoutInSecondsIs0_Expect_InvalidResult()
		{
			TarpitServiceOptions options = TarpitService.GetDefaultOptions();
			options.TimeoutInSeconds = 0;

			string? result = TarpitService.ValidateOptions(options);

			Assert.IsNotNull(result);
			Assert.IsTrue(result.Contains(nameof(TarpitServiceOptions.TimeoutInSeconds)));
		}

		[TestMethod]
		public void Validate_When_WriteIntervalInMsIs0_Expect_InvalidResult()
		{
			TarpitServiceOptions options = TarpitService.GetDefaultOptions();
			options.WriteIntervalInMs = 0;

			string? result = TarpitService.ValidateOptions(options);

			Assert.IsNotNull(result);
			Assert.IsTrue(result.Contains(nameof(TarpitServiceOptions.WriteIntervalInMs)));
		}

		[TestMethod]
		public void Validate_When_WriteSizeIs0_Expect_InvalidResult()
		{
			TarpitServiceOptions options = TarpitService.GetDefaultOptions();
			options.WriteSize = 0;

			string? result = TarpitService.ValidateOptions(options);

			Assert.IsNotNull(result);
			Assert.IsTrue(result.Contains(nameof(TarpitServiceOptions.WriteSize)));
		}

	}
}