using SharpTestBench;
public static partial class Entry
{
    sealed class Detach(Action action):IDisposable
    {
        Action? pending=action;
        public void Dispose()=>Interlocked.Exchange(ref pending,null)?.Invoke();
    }
    sealed class CountedTrigger(Trigger child):Trigger
    {
        public int Registered{get;private set;}public int Active{get;private set;}public int Detached{get;private set;}
        protected override IDisposable Subscribe(SimRuntime runtime,Action<Exception?> complete)
        {
            var subscription=SubscribeTo(child,runtime,complete);Registered++;Active++;
            return new Detach(()=>{subscription.Dispose();Active--;Detached++;});
        }
    }
}
