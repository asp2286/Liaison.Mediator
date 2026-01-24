# Benchmarks summary (Raspberry Pi 5 (Cortex-A76 x4))

- OS: Ubuntu 24.04.3 LTS
- Arch: arm64
- Runtime: net8.0
- Source formats: Csv
- Source files: 4
- Run timestamp (UTC): 2026-01-24T15:34:07.3236672+00:00

| Scenario | Liaison (ns) | MediatR (ns) | Speedup | Liaison (B/op) | MediatR (B/op) | Alloc reduction |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Publish_MultiHandler/Publish (HandlerCount=10) | 218.4 | 1173.5 | 5.37x | 32 | 1416 | 97.7% |
| Publish_MultiHandler/Publish (HandlerCount=2) | 105.4 | 399.3 | 3.79x | 32 | 392 | 91.8% |
| Publish_MultiHandler/Publish (HandlerCount=5) | 137.5 | 711.1 | 5.17x | 32 | 776 | 95.9% |
| Send_DI/Send | 345.8 | 435.8 | 1.26x | 240 | 312 | 23.1% |
| Send_DI_Pipeline/Send (BehaviorCount=10) | 627.1 | 1222.5 | 1.95x | 368 | 1456 | 74.7% |
| Send_DI_Pipeline/Send (BehaviorCount=2) | 536.7 | 705.7 | 1.31x | 368 | 560 | 34.3% |
| Send_DI_Pipeline/Send (BehaviorCount=5) | 567 | 887.9 | 1.57x | 368 | 896 | 58.9% |
| Send_SingleHandler/Send | 286.6 | 402.9 | 1.41x | 272 | 312 | 12.8% |
