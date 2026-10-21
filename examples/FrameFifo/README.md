# Committed AXI-stream frames

`frame-ci --depth 8 --pipeline 1 --seed 83` compares both Icarus runners against
C# transaction and independent Python deque references. Add `--negative-control`
for output payload corruption, or `--output-fifo` for the additional output FIFO.

The pinned MIT axis_fifo RTL is reused with FRAME_FIFO and DROP_OVERSIZE_FRAME set.
Generated frames are shorter than RAM depth; this qualification does not exercise
oversize/bad-frame dropping, pause controls, reset interruption or full AXI bursts.
The reference rejects output before the final input handshake has completed,
checks all sidebands, stable output under backpressure, complete ordered drain,
identity/seed parity and repository source hashes. Frame evidence is separate
from the existing beat-mode FIFO report. Construction is not execution: actual
qualified configurations are recorded with each local checkpoint.
