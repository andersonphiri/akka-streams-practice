using Akka.Streams;
using Akka.Streams.Dsl;
namespace AkkaStreamz.Console.App;
using Akka.Streams.Dsl;
using Akka;
public class CustomGraphs
{
    static async Task MergeTwoSources()
    {
        var sourceOne = Source.From(Enumerable.Range(1, 10));
        var source2 = Source.From(Enumerable.Range(20, 30));
        var mergedSources = GraphDsl.Create(builder =>
        {
            var concat = Concat.Create<int>(2);
            // concat.Shape.In(0)
            // sourceOne.Via(concat.Shape) 
            builder.From(sourceOne).To(concat.Shape.In(0));
            builder.From(source2).To(concat.Shape.In(1));
            return new SourceShape<int>(concat.Shape.Out);
        });
         
        var system = ActorSystem.Create("custommergedsource");
        var materializer = system.Materializer();
        var actor = system.ActorOf(Props.Create(() => new TransformerActor()), "transformeractor");
        var finalSinkActor = system.ActorOf(SimpleSinkActor.Props, "finalsinkactor");
        var flowAsync = Flow.Create<int, long>().SelectAsync(100, num =>  actor.Ask<long>((long)num, TimeSpan.FromSeconds(1)));
        var sinkConsumer = Sink.ActorRef<long>(finalSinkActor, new SimpleSinkActor.Messages.StreamCompleted());

        // mergedSources.RunWith(sinkConsumer);
        //var runnableGraph = mergedSources.Via(flowAsync).To(sinkConsumer);
        // runnableGraph.Run(materializer);
        await system.WhenTerminated;
    }
}