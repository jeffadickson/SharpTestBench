# Closure acceptance fixture

70 distinct contracts qualify a local depth-three FIFO and selected typed-access,
owned-task, structured-group, functional-coverage and discovery/failure behavior.
Use the C# helper `contracts-ci --suite closure --through 70`; the default suite
remains the prior 100 requirement contracts. Names/seeds derive from full discovery.

Icarus is qualified on Windows/Linux with both runners; Linux Verilator is a
separate backend gate. This fixture is not an upstream AXIS FIFO or arbitrary-depth
qualification. See docs/development-history/closure100-report.md for exact evidence.
