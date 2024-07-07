namespace AkkaStreamz.Console.App;

public class RepeatingSender : ReceiveActor , IWithTimers
{
    public interface IProductionSwitch
    {
        
    }
    public static class RepeatingSenderMessages
    {
        public record SendNewMessage();

        public record StartProducingNow() : IProductionSwitch;

        public record StopProducing() : IProductionSwitch;
    }
    private readonly ILoggingAdapter _logger = Logging.GetLogger(Context);
    private readonly IActorRef _graphSource = null; 
    public RepeatingSender(IActorRef graphSource)
    {
        _graphSource = graphSource;
        Receive<RepeatingSenderMessages.SendNewMessage>(HandleRepeatingMessage);
        Receive<IShouldShutdown>(HandleIShouldShutdown);
        Receive<IProductionSwitch>(HandleIProductionSwitch);
    }

    private void HandleIProductionSwitch(IProductionSwitch obj)
    {
        switch (obj)
        {
            case RepeatingSenderMessages.StopProducing _ :
                CancelTimers();
                break;
            case RepeatingSenderMessages.StartProducingNow _ :
                CreateTimers();
                break;
            default: break;
                
        }
    }

    private void HandleIShouldShutdown(IShouldShutdown obj)
    {
        _logger.Info("shutting down now {0}...", Self.Path);
        Context.Stop(Self);
    }

    public static Props Props(IActorRef graphSource)
    {
        return Akka.Actor.Props.Create(() => new RepeatingSender(graphSource));
    }

    private void HandleRepeatingMessage(RepeatingSender.RepeatingSenderMessages.SendNewMessage obj)
    {
        _logger.Info("received time reminder to produce mesg: {0}", obj);
        _graphSource.Tell(new RepeatingMessage(Guid.NewGuid()));
    }

    public ITimerScheduler Timers { get; set; }

    void CreateTimers()
    {
        Timers.StartPeriodicTimer("repeater", new RepeatingSenderMessages.SendNewMessage(),
            TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1500) );
    }

    void CancelTimers()
    {
        Timers.Cancel("repeater");
    }
    protected override void PreStart()
    { 
        
        base.PreStart();
    }
}