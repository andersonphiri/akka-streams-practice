using System.Security.Cryptography.X509Certificates;

namespace AkkaStreamz.Console.App;

public class TransformerActor : ReceiveActor
{
    public TransformerActor()
    {
        Receive<string>(str =>
        {
            Sender.Tell(str.ToUpperInvariant());
        });
        Receive<long>(l => Sender.Tell(l *l));
    }
}

public class SimpleSinkActor : ReceiveActor
{
    public static class Messages
    {
        public record StreamCompleted();
    }
    private readonly ILoggingAdapter _logger = Logging.GetLogger(Context);
    public static Props Props {get => Props.Create(() => new SimpleSinkActor());}
    public SimpleSinkActor()
    {
        Receive<long>(l =>
        {
            _logger.Info("I have receive long value: {0}", l);
        });
        Receive<Messages.StreamCompleted>(c => { _logger.Info("stream said has completed: {0}", c); });
    }
}

public record RepeatingMessage(Guid Id );