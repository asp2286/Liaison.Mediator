# Benchmarks summary (Ryzen 9 7940HS)

- OS: Windows 11 Pro
- Arch: x64
- Runtime: net8.0
- Source formats: Csv
- Source files: 4
- Run timestamp (UTC): 2026-01-22T05:39:17.5463788+00:00

| Scenario | Liaison (ns) | MediatR (ns) | Speedup | Liaison (B/op) | MediatR (B/op) | Alloc reduction |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Publish_MultiHandler/Publish (HandlerCount=10) | 59.87 | 236.41 | 3.95x | 32 | 1416 | 97.7% |
| Publish_MultiHandler/Publish (HandlerCount=2) | 33.56 | 92.03 | 2.74x | 32 | 392 | 91.8% |
| Publish_MultiHandler/Publish (HandlerCount=5) | 44.36 | 145.5 | 3.28x | 32 | 776 | 95.9% |
| Send_DI/Send | 160.02 | 97.91 | 0.61x | 440 | 312 | -41% |
| Send_DI_Pipeline/Send (BehaviorCount=1) | 189.7 | 131.2 | 0.69x | 576 | 448 | -28.6% |
| Send_DI_Pipeline/Send (BehaviorCount=2) | 195.6 | 163.7 | 0.84x | 688 | 560 | -22.9% |
| Send_DI_Pipeline/Send (BehaviorCount=5) | 226.4 | 200.4 | 0.89x | 1024 | 896 | -14.3% |
| Send_SingleHandler/Send | 71.71 | 99.92 | 1.39x | 272 | 312 | 12.8% |
