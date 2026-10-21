# Bad-frame FIFO qualification

`frame-drop-ci --depth 8 --pipeline 1 --seed 83` checks mixed complete good/bad
frames through both Icarus runners and independent C#/Python transaction replays.
Add `--output-fifo` or `--negative-control` for output staging or payload corruption.

The pinned MIT FIFO uses FRAME_FIFO1, DROP_OVERSIZE_FRAME1 and DROP_BAD_FRAME1.
Frames stay shorter than RAM capacity. Bit0 of the last accepted beat's tuser marks
a bad frame; earlier user bits and unmasked bit1 cannot drop a good frame. Separate
13-column traces include bad/good status pulses at the following cycle, full
sidebands, discarded/consumed conservation, backpressure and complete drain.

Normal frame/beat schemas remain unchanged. This does not qualify oversize drops,
pause controls, mid-frame reset or full AXI bursts. Construction combinations are
not executed platform/configuration certifications; checkpoint reports list actual runs.
