# Pinned upstream LFSR and CRC qualification

The unchanged MIT `lfsr.v` comes from [alexforencich/verilog-lfsr](https://github.com/alexforencich/verilog-lfsr/tree/c1f86d036745935f3812ec6ddc7821c70ef4c9de).
Its copyright, license, immutable revision, and exact source hashes are retained in
`tests/Fixtures/verilog-lfsr`. No upstream scripts are executed.

`dotnet run --project tools/SharpTestBench.Tools -c Release -- lfsr-ci --fixture crc32 --style REDUCTION --seed 17` runs both the
legacy and native Icarus paths. The Python integer model and the independent
managed boolean-register model calculate serial shifts; the upstream RTL uses
parallel XOR masks. CRC32 and CRC16 are checked against Python standard-library
implementations, and PRBS7 has a fixed sequence anchor.

Eight fixture configurations cover 1/7/16/32/58-bit state, up to 64-bit data,
Galois/Fibonacci topology, bit reversal, and feedback/feed-forward modes. Both
LOOP and REDUCTION implementations run zero, all-ones, every state/data basis bit,
and deterministic random vectors. Seeds 1 and 17 run in CI. Each output has an
independent corruption control: `--fault 1` flips state and `--fault 2` flips data.
Both runners must report the named `LFSR_MISMATCH`; crashes do not qualify.

Qualification checks exact test selection, seed, exit code, complete input/output
trace, unchanged pinned sources, and the exact plan hash before/after execution.
Reports and child evidence are retained under `build/lfsr-*`. These are specific
Icarus configurations, not certification of every upstream parameter combination.
