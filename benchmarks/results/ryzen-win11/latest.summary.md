# Benchmarks summary (Ryzen 9 7940HS)

- OS: Windows 11 Pro
- Arch: x64
- Runtime: net8.0
- Source formats: Csv
- Source files: 4
- Run timestamp (UTC): 2026-01-24T15:56:58.7208709+00:00

| Scenario | Liaison (ns) | MediatR (ns) | Speedup | Liaison (B/op) | MediatR (B/op) | Alloc reduction |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Publish_MultiHandler/Publish (HandlerCount=10) | 61.34 | 230.63 | 3.76x | 32 | 1416 | 97.7% |
| Publish_MultiHandler/Publish (HandlerCount=2) | 34.08 | 93.98 | 2.76x | 32 | 392 | 91.8% |
| Publish_MultiHandler/Publish (HandlerCount=5) | 42.36 | 152.5 | 3.6x | 32 | 776 | 95.9% |
| Send_DI/Send | 75.94 | 94.03 | 1.24x | 240 | 312 | 23.1% |
| Send_DI_Pipeline/Send (BehaviorCount=10) | 146.1 | 330.2 | 2.26x | 368 | 1456 | 74.7% |
| Send_DI_Pipeline/Send (BehaviorCount=2) | 133.1 | 155.6 | 1.17x | 368 | 560 | 34.3% |
| Send_DI_Pipeline/Send (BehaviorCount=5) | 128.5 | 194.3 | 1.51x | 368 | 896 | 58.9% |
| Send_SingleHandler/Send | 67.52 | 86.16 | 1.28x | 272 | 312 | 12.8% |
