# Benchmarks summary (Apple M3)

- OS: macOS
- Arch: arm64
- Runtime: net8.0
- Source formats: Csv
- Source files: 4
- Run timestamp (UTC): 2026-01-24T15:27:23.2013700+00:00

| Scenario | Liaison (ns) | MediatR (ns) | Speedup | Liaison (B/op) | MediatR (B/op) | Alloc reduction |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Publish_MultiHandler/Publish (HandlerCount=10) | 51.83 | 258.49 | 4.99x | 32 | 1416 | 97.7% |
| Publish_MultiHandler/Publish (HandlerCount=2) | 26.63 | 87.7 | 3.29x | 32 | 392 | 91.8% |
| Publish_MultiHandler/Publish (HandlerCount=5) | 36.96 | 162.42 | 4.39x | 32 | 776 | 95.9% |
| Send_DI/Send | 74.92 | 92.1 | 1.23x | 240 | 312 | 23.1% |
| Send_DI_Pipeline/Send (BehaviorCount=10) | 136 | 301.3 | 2.22x | 368 | 1456 | 74.7% |
| Send_DI_Pipeline/Send (BehaviorCount=2) | 118.6 | 153.2 | 1.29x | 368 | 560 | 34.3% |
| Send_DI_Pipeline/Send (BehaviorCount=5) | 122.5 | 216.3 | 1.77x | 368 | 896 | 58.9% |
| Send_SingleHandler/Send | 57.13 | 76.91 | 1.35x | 272 | 312 | 12.8% |
