# Benchmarks summary (Apple M3)

- OS: macOS
- Arch: arm64
- Runtime: net8.0
- Source formats: Csv
- Source files: 3
- Run timestamp (UTC): 2026-01-22T09:08:03.5647622+00:00

| Scenario | Liaison (ns) | MediatR (ns) | Speedup | Liaison (B/op) | MediatR (B/op) | Alloc reduction |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Publish_DI/Publish (HandlerCount=10) | 50.46 | 243.5 | 4.83x | 0 | 1312 | 100% |
| Publish_DI/Publish (HandlerCount=2) | 40.7 | 95.13 | 2.34x | 0 | 352 | 100% |
| Publish_DI/Publish (HandlerCount=5) | 41.76 | 158.21 | 3.79x | 0 | 712 | 100% |
| Send_DI/Send | 79.37 | 95.82 | 1.21x | 240 | 312 | 23.1% |
| Send_DI_Pipeline/Send (BehaviorCount=1) | 130.1 | 157.4 | 1.21x | 368 | 448 | 17.9% |
| Send_DI_Pipeline/Send (BehaviorCount=2) | 124.1 | 156.2 | 1.26x | 368 | 560 | 34.3% |
| Send_DI_Pipeline/Send (BehaviorCount=5) | 129.8 | 215.7 | 1.66x | 368 | 896 | 58.9% |
