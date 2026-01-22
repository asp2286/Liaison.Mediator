# Benchmarks summary (Apple M3)

- OS: macOS
- Arch: arm64
- Runtime: net8.0
- Source formats: Csv
- Source files: 3
- Run timestamp (UTC): 2026-01-22T04:24:55.2463863+00:00

| Scenario | Liaison (ns) | MediatR (ns) | Speedup | Liaison (B/op) | MediatR (B/op) | Alloc reduction |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Publish_DI/Publish (HandlerCount=10) | 269.36 | 240.67 | 0.89x | 1104 | 1312 | 15.9% |
| Publish_DI/Publish (HandlerCount=2) | 127.54 | 97.4 | 0.76x | 336 | 352 | 4.5% |
| Publish_DI/Publish (HandlerCount=5) | 187.51 | 155.21 | 0.83x | 624 | 712 | 12.4% |
| Send_DI/Send | 156.84 | 94.81 | 0.6x | 440 | 312 | -41% |
| Send_DI_Pipeline/Send (BehaviorCount=1) | 183.4 | 136.2 | 0.74x | 576 | 448 | -28.6% |
| Send_DI_Pipeline/Send (BehaviorCount=2) | 200 | 156.3 | 0.78x | 688 | 560 | -22.9% |
| Send_DI_Pipeline/Send (BehaviorCount=5) | 240.4 | 220.1 | 0.92x | 1024 | 896 | -14.3% |
