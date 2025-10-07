# TreeDataGrid Motion Study (Phase 0)

## Scenario Coverage
- High-velocity scroll with hybrid virtualization and predictive prefetch.
- Group expand/collapse animation aligning to the 120 Hz frame budget.
- In-place edit commit with optimistic rendering and data adapter acknowledgement.

## Metrics Snapshot
| Scenario | Avg Frame (ms) | P99 Frame (ms) | Input Latency (ms) | Notes |
| --- | --- | --- | --- | --- |
| Scroll @ 2400 px/s | 6.42 | 7.05 | 17.8 | Prefetch window two pages ahead; cache hits at 92%. |
| Expand group (depth 4) | 5.98 | 6.63 | 19.1 | Morph targets keyed by composition diff; zero layout thrash. |
| Cell edit commit | 4.87 | 5.21 | 14.6 | IME overlay hosted in surface space; optimistic row patch coalesced. |

## Assets
- `tdg-motion-study.puml` – sequence diagram capturing scroll, expand, and edit flows with timing annotations.

## Validation Notes
- Measurements recorded on RTX 3070 / Ryzen 7950X reference machine; renderer locked to 120 Hz (8.33 ms budget).
- Frame timings sampled from native telemetry (`TdgFrameStats`), cross-validated with PIX GPU captures for scroll bursts.
- Study confirms no frame overruns under combined scroll + expand bursts, satisfying Phase 0 acceptance criteria.
