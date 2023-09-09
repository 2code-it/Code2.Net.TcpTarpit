# Code2.Net.TcpTarpit
Tarpit services to slowly send data to incoming tcp connections. 

## Service Options
- string? **ListenAddress**, listener ip address
- string? **Ports**, comma seperated list of ports and port ranges
- bool **UseIPv4Only**, defaults to both ipv4 and ipv6, set to true for ipv4 only
- int **WriteIntervalInMs**, data send interval
- int **WriteSize**, the amount of bytes to send per interval
- int **UpdateIntervalInSeconds**, ConnectionsUpdated event interval
- int **TimeoutInSeconds**, connection timout
- string? **ResponseFile** (optional), filepath to a file containing the response data
- string? **ResponseText** (optional), response data

## Example
```
using Code2.Net.TcpTarpit;
using System.Text;

var options = new TarpitServiceOptions
{
	ListenAddress = "192.168.2.23",
	Ports = "10-99",
	UseIPv4Only = true,
	WriteIntervalInMs = 200,
	WriteSize = 6,
	UpdateIntervalInSeconds = 3,
	TimeoutInSeconds = 600,
	//ResponseFile = null
	ResponseText = "0123456789"
};

var service = new TarpitService(options);
service.Start();
service.ConnectionsUpdated += (sender, args) =>
{
	string[] lines = args.Connections.Select(x => $"{x.RemoteEndPoint}\t{x.BytesSent}\t{Encoding.UTF8.GetString(x.Buffer)}").ToArray();
	foreach (var line in lines)
	{
		Console.WriteLine(line);
	}
};
Console.WriteLine("Service started with listeners {0}", service.ListenersCount);

Console.ReadKey();
service.Stop();

Console.WriteLine("Service stopped.");

```
The above code starts TarpitService listening on 90 endpoints 
from 192.168.2.23:10 till 192.168.2.23:90. Any incoming connections within the 
range will be accepted and added to a list of active connections. Every interval
the active connections will sent the writesize in bytes from the responsetext
option. The connection will be marked completed when a send data operation fails,
or the timeout of 10 minutes exceeds. When marked completed after the next 
ConnectionsUpdated event it gets removed from the active connections list.

Program output:
```
Service started with listeners 90
[::ffff:192.168.2.23]:53728      24      890123
[::ffff:192.168.2.23]:53729      24      890123
[::ffff:192.168.2.23]:53729      54      890123
[::ffff:192.168.2.23]:53730      18      234567
[::ffff:192.168.2.23]:53730      54      890123
[::ffff:192.168.2.23]:53730      84      890123
Service stopped.
```

UseIPv4Only=false
```
Service started with listeners 90
192.168.2.23:53565       6       012345
192.168.2.23:53566       6       012345
192.168.2.23:53565       24      890123
192.168.2.23:53566       42      678901
192.168.2.23:53566       54      890123
Service stopped.
```

## Remarks
Each connection has an unique id.  
ConnectionsUpdated event timing vary when UpdateIntervalInSeconds is not a 
multiple of WriteIntervalInMs.  
When the service stops it will complete active connections and send a last
ConnectionsUpdated event.  
If the data sent total bytes exceeds the data defined, it starts from the front
(see InfiniteReader)