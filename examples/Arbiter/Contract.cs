// Fixture model uses a circular candidate scan, independently of RTL mask logic.
namespace SharpTestBench.Fixtures;

internal sealed record ArbiterConfig(int Ports, bool RoundRobin, int Blocking, bool LsbPriority)
{
    internal string Top => $"arb_p{Ports}_r{(RoundRobin ? 1 : 0)}_b{Blocking}_l{(LsbPriority ? 1 : 0)}";
    internal int Mask => (1 << Ports) - 1;
    internal void Validate()
    {
        if (Ports is not (2 or 3 or 4 or 8) || Blocking is < 0 or > 2)
            throw new InvalidDataException("Arbiter ports must be2/3/4/8; blocking must be0/1/2.");
    }
}
internal sealed record ArbiterRow(bool Reset, int Request, int Acknowledge);
internal readonly record struct ArbiterObservation(int Grant, bool Valid, int Encoded);

internal sealed class ArbiterModel
{
    readonly ArbiterConfig config;
    int? winner;
    int cursor;
    internal ArbiterModel(ArbiterConfig config)
    {
        config.Validate(); this.config = config; cursor = Initial;
    }
    int Initial => config.LsbPriority ? 0 : config.Ports - 1;
    internal ArbiterObservation Step(ArbiterRow row)
    {
        if (row.Request < 0 || row.Request > config.Mask || row.Acknowledge < 0 || row.Acknowledge > config.Mask)
            throw new InvalidDataException("Arbiter stimulus exceeds configured ports.");
        if (row.Reset) { winner = null; cursor = Initial; return default; }
        bool held = winner is { } previous && (config.Blocking == 1 && (row.Request & (1 << previous)) != 0 ||
            config.Blocking == 2 && (row.Acknowledge & (1 << previous)) == 0);
        if (!held)
        {
            winner = null;
            int start = config.RoundRobin ? cursor : Initial;
            for (int offset = 0; offset < config.Ports; offset++)
            {
                int candidate = (start + (config.LsbPriority ? offset : -offset) + config.Ports) % config.Ports;
                if ((row.Request & (1 << candidate)) == 0) continue;
                winner = candidate;
                if (config.RoundRobin) cursor = (candidate + (config.LsbPriority ? 1 : -1) + config.Ports) % config.Ports;
                break;
            }
        }
        return winner is { } index ? new(1 << index, true, index) : default;
    }
}
