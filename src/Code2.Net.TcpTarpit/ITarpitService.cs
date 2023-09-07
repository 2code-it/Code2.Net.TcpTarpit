namespace Code2.Net.TcpTarpit
{
	public interface ITarpitService
	{
		int ConnectionsCount { get; }
		int ListenersCount { get; }

		event EventHandler<ConnectionCreatedEventArgs>? ConnectionCreated;
		event EventHandler<ConnectionsUpdatedEventArgs>? ConnectionsUpdated;
		event EventHandler<UnhandledExceptionEventArgs>? Error;

		int Start();
		void Stop();
	}
}