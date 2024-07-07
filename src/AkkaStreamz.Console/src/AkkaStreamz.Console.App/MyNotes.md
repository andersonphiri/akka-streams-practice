#### Reusable patterns 

- `Combine<T>` - merges several sources together 
- `GroupedWithin<T>` - debounces events in groups of N or within `T` milliseconds
- `Batch<T>` - gorups elements together when downstream is too busy
- `Select<TIn,TOut>`- project from one type to another 
- `Broadcast<T>` - broadcast stream elements to multiple consumers 
- `Where<T>` - filter out elements based on a predicate
- `Throttle<T>` - delay the rate at which elements can be passed downstream  

#### Integration with Actors 

- `Source.ActorRef<T>` - materializes a stream into an IActorRef that you can message to run it 
- `Sink.ActorRef<T>` - forwards the message from the end of the stream to an IActorRef
- `Flow.SelectAsync<T>` - can Ask<T> an actor and await its response inline as part of the stream
- 


#### Sources for sample

- all: https://github.com/Aaronontheweb/intro-to-akka.net-streams/ 
- signalR integration:
- https://github.com/Aaronontheweb/Akka.Cluster.StreamingStatus/tree/master/src 

- scala code: 
- https://github.com/rockthejvm/akka-streams/tree/master/src/main/scala/part4_techniques 

- sink and sources from channels :
- https://alpakka.getakka.net/documentation/Channels.html 