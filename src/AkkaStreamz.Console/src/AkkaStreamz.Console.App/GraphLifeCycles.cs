using Akka;
using Akka.Streams;
using Akka.Streams.Supervision;
using Decider = Akka.Streams.Supervision.Decider;
using Directive = Akka.Streams.Supervision.Directive;

namespace AkkaStreamz.Console.App;

public class GraphLifeCycles
{
    internal static async Task Run()
    {
        var system = ActorSystem.Create("graphstages");
        var materializer = system.Materializer();
        Source<string, NotUsed> source3 = Source.Repeat("a");
        var ints = new List<int>() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var source1 = Source.From(ints);
        var merged = source1.Zip(source3).RunAsAsyncEnumerable(materializer);
        await foreach (var (i, s) in merged)
        {
            System.Console.WriteLine($"{i} -> {s}");
        }

        await system.WhenTerminated;
    }

    public static async Task SourceOfActorRef()
    {
        var system = ActorSystem.Create("graphstageswithactorsource");
        var materializer = system.Materializer();
        Source<string, IActorRef> actorSource = Source.ActorRef<string>(1000, OverflowStrategy.DropHead);
        var (preMaterializedRef, standAloneSorce) = actorSource.PreMaterialize(materializer);
        IAsyncEnumerable<string> StrResponses = standAloneSorce
            .Via(Flow.Create<string>().Select(x => x.ToLowerInvariant())).RunAsAsyncEnumerable(materializer);
        preMaterializedRef.Tell("hello");
        preMaterializedRef.Tell("my");
        preMaterializedRef.Tell("name");
        preMaterializedRef.Tell("is");
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(3000));
        await foreach (var str in StrResponses.WithCancellation(cts.Token))
        {
            System.Console.WriteLine(str);
        }
    } 
    
    public static async Task UseOfKillSwitches()
    {
        var system = ActorSystem.Create("graphstageswithkillswitch");
        var materializer = system.Materializer(); 
        var cts = new CancellationTokenSource();
        Source<string, IActorRef> actorSource = Source.ActorRef<string>(1000, OverflowStrategy.DropHead);
        var (preMaterializedRef, standAloneSource) = actorSource.PreMaterialize(materializer);
        
        // create a kill switch 
        IAsyncEnumerable<string> StrResponses = standAloneSource
            .Select(x => x.ToLowerInvariant()).Via(KillSwitches.AsFlow<string>(cts.Token, cancelGracefully: true)).RunAsAsyncEnumerable(materializer);
        preMaterializedRef.Tell("hello");
        preMaterializedRef.Tell("my");
        preMaterializedRef.Tell("name");
        preMaterializedRef.Tell("is");
        preMaterializedRef.Tell("hello");
        preMaterializedRef.Tell("my");
        preMaterializedRef.Tell("name");
        preMaterializedRef.Tell("is");
        int count = 0;
        await foreach (var str in StrResponses)
        {
            System.Console.WriteLine(str);
            if (++count == 5)
            {
                await cts.CancelAsync();
            } 
           
        } 
        System.Console.WriteLine("streaming completed gracefully!!!");
    }
    
    public static async Task UseOfPoisonPills()
    {
        var system = ActorSystem.Create("graphstageswithpoisonpill");
        var materializer = system.Materializer(); 
        var cts = new CancellationTokenSource();
        Source<string, IActorRef> actorSource = Source.ActorRef<string>(1000, OverflowStrategy.DropHead);
        var (preMaterializedRef, standAloneSource) = actorSource.PreMaterialize(materializer);
        
        // create a kill switch 
        IAsyncEnumerable<string> StrResponses = standAloneSource
            .Select(x => x.ToLowerInvariant())
            //.Via(KillSwitches.AsFlow<string>(cts.Token, cancelGracefully: true))
            .RunAsAsyncEnumerable(materializer);
        preMaterializedRef.Tell("hello");
        preMaterializedRef.Tell("my");
        preMaterializedRef.Tell("name");
        preMaterializedRef.Tell("is");
        preMaterializedRef.Tell("hello");
        preMaterializedRef.Tell("my");
        preMaterializedRef.Tell("name");
        preMaterializedRef.Tell("is");
        int count = 0;
        await foreach (var str in StrResponses)
        {
            System.Console.WriteLine(str);
            if (++count == 5)
            {
                preMaterializedRef.Tell(PoisonPill.Instance);
            } 
           
        } 
        System.Console.WriteLine("streaming completed gracefully from poison pill !!!");
    }
    
    public static async Task GracefullyShutdownStreamInCaseOfException()
    {
        var system = ActorSystem.Create("graphstageswithdecider");
        var materializer = system.Materializer(); 
        var cts = new CancellationTokenSource();
        
        Decider decider = cause => cause is DivideByZeroException
            ? Akka.Streams.Supervision.Directive.Restart
            : Directive.Stop;
        
        var ints = new List<int>() { 1, 2, 3, 4, 5,0, 6, 7, 8, 9, 10 };
        var numbers = Source.From(ints);

        var integerDivision = numbers.Via(Flow.Create<int>().Select(x => $"1/{x} is {1 / x}"))
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(decider))
            .RunAsAsyncEnumerable(materializer);
        await foreach (var div in integerDivision)
        {
            System.Console.WriteLine(div);
        }
        
        System.Console.WriteLine("streaming completed !!!");
    }

    public static async Task AskAsyncFlowToSinkActor()
    {
        var system = ActorSystem.Create("passthroughactorflow");
        var materializer = system.Materializer();
        Source<int, NotUsed> sourceInts = Source.From(Enumerable.Range(0, 10));
        var actor = system.ActorOf(Props.Create(() => new TransformerActor()), "transformeractor");
        var finalSinkActor = system.ActorOf(SimpleSinkActor.Props, "finalsinkactor");
        var flowAsync = Flow.Create<int, long>().SelectAsync(100, num =>  actor.Ask<long>((long)num, TimeSpan.FromSeconds(1)));
        var sinkConsumer = Sink.ActorRef<long>(finalSinkActor, new SimpleSinkActor.Messages.StreamCompleted());
        var runnableGraph = sourceInts.Via(flowAsync).To(sinkConsumer);
        runnableGraph.Run(materializer);
        await system.WhenTerminated;
    }
    
    public static async Task ConnectingGraphsWithActors()
    {
        var system = ActorSystem.Create("connectingactorsflow");
        var materializer = system.Materializer();
        var cts = new CancellationTokenSource();
        Source<RepeatingMessage, IActorRef> actorSource =
            Source.ActorRef<RepeatingMessage>(1000, OverflowStrategy.DropHead);
        var (prematerializedActor, preMaterializedSource) = actorSource.PreMaterialize(materializer);
        var timedProducer = system.ActorOf(RepeatingSender.Props(prematerializedActor), "timedproducer");
        int count = 0;
        IAsyncEnumerable<RepeatingMessage> asyncEnumerable = preMaterializedSource
            .Via(KillSwitches.AsFlow<RepeatingMessage>(cts.Token, true)).RunAsAsyncEnumerable(materializer);
        // start producing now
        timedProducer.Tell(new RepeatingSender.RepeatingSenderMessages.StartProducingNow());
        System.Console.WriteLine("waiting for timed producer to start now...");
        await foreach (RepeatingMessage message in asyncEnumerable)
        {
            count++;
            System.Console.WriteLine($"repeating message from flow: {message}");
            if (count > 10)
            {
                System.Console.WriteLine("shutting down all upstream producers");
                timedProducer.Tell(new ShouldShutdown());
                await cts.CancelAsync();
                
            }
        }

        await system.Terminate();
    }
    
    public static async Task BroadcastGraph()
    {
        var system = ActorSystem.Create("connectingactorsflow");
        var materializer = system.Materializer();
        var cts = new CancellationTokenSource();
        Source<RepeatingMessage, IActorRef> actorSource =
            Source.ActorRef<RepeatingMessage>(1000, OverflowStrategy.DropHead);
        var (prematerializedActor, preMaterializedSource) = actorSource.PreMaterialize(materializer);
        var timedProducer = system.ActorOf(RepeatingSender.Props(prematerializedActor), "timedproducer");
        int count = 0;
        IAsyncEnumerable<RepeatingMessage> asyncEnumerable = preMaterializedSource
            .Via(KillSwitches.AsFlow<RepeatingMessage>(cts.Token, true)).RunAsAsyncEnumerable(materializer);
        // start producing now
        timedProducer.Tell(new RepeatingSender.RepeatingSenderMessages.StartProducingNow());
        System.Console.WriteLine("waiting for timed producer to start now...");
        
        
        await foreach (RepeatingMessage message in asyncEnumerable)
        {
            count++;
            System.Console.WriteLine($"repeating message from flow: {message}");
            if (count > 10)
            {
                System.Console.WriteLine("shutting down all upstream producers");
                timedProducer.Tell(new ShouldShutdown());
                await cts.CancelAsync();
                
            }
        }

        await system.Terminate();
    }
    
}