
using Akka;
using Akka.IO;
using Akka.Streams;
using Akka.Streams.Dsl;
namespace AkkaStreamz.Console.App;

public class BiDirectionalFlowServerExample
{
    public static Flow<ByteString, ByteString, NotUsed> Encoder =
        Framing.SimpleFramingProtocolEncoder(256 * 1024 * 1024);

    public static Flow<ByteString, ByteString, NotUsed> Decoder =
        Framing.SimpleFramingProtocolDecoder(256 * 1024 * 1024);
    internal static async Task Run()
    {
        var system = ActorSystem.Create("BidiFlow", @"akka.loglevel=DEBUG");
        var materializer = system.Materializer();
        // create server 
        Source<Akka.Streams.Dsl.Tcp.IncomingConnection, Task<Akka.Streams.Dsl.Tcp.ServerBinding>> connections 
            = system.TcpStream().Bind("127.0.0.1", 8888);

        var (serverBind, source) = connections.PreMaterialize(materializer);
        
        // run event handler per connection 
        source.RunForeach(conn =>
        {
            var echo = Flow.Create<ByteString>().Via(Decoder)
                .Select(c => c.ToString())
                .Select(c =>
                {
                    return c.ToUpperInvariant();
                })
                .Select(ByteString.FromString)
                .Via(Encoder)
                .GroupedWithin(100, TimeSpan.FromMilliseconds(20))
                .Select(x => x.Aggregate(ByteString.Empty, (s, byteSting) => s.Concat(byteSting)));
            conn.HandleWith(echo, materializer);
        }, materializer: materializer);

        await serverBind;

        // https://github.com/Aaronontheweb/Akka.Streams.Benchmark/blob/dev/src/Akka.Streams.Benchmark/Program.cs 
        
        // start client to connect to server 
        var clientTask = system.TcpStream().OutgoingConnection(serverBind.Result.LocalAddress);
        var repeater = ByteString.FromString("A");
        var dataGen = Source.Repeat(repeater)
            .Via(Encoder)
            .Batch(100, s => s, (s, byteString) => s.Concat(byteString));
        
        // co,pute the rate at which data is sent client->server->client per second 
        var bytesPerSecondFlow = Flow.Create<ByteString>().Via(Decoder)
            .GroupedWithin(1000, TimeSpan.FromMilliseconds(1000))
            .Select(bytes => bytes.Sum(x => x.Count))
            .Aggregate((0L, DateTime.UtcNow), (l, s) =>
            {
                var (accum, time) = l;
                accum += s;
                var timeSpan = DateTime.UtcNow - time;
                if (timeSpan.TotalSeconds >= 1.0d)
                {
                    System.Console.WriteLine($"{accum} byte / {timeSpan.TotalSeconds}");
                    return (0L, DateTime.UtcNow); // reset 
                }

                return (accum, time);
            }).To(Sink.Ignore<(long, DateTime)>());
        
        // run bi-di flow 
        dataGen.Via(clientTask).To(bytesPerSecondFlow).Run(materializer);

        await system.WhenTerminated;

    }
}