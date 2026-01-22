# Benchmarks summary (Raspberry Pi 5 (Cortex-A76 x4))

- OS: Ubuntu 24.04.3 LTS
- Arch: arm64
- Runtime: net8.0
- Source formats: Csv
- Source files: 4
- Run timestamp (UTC): 2026-01-22T06:54:59.1613140+00:00

| Scenario | Liaison (ns) | MediatR (ns) | Speedup | Liaison (B/op) | MediatR (B/op) | Alloc reduction |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Publish_MultiHandler/Publish (HandlerCount=10) | 218.2 | 1160.9 | 5.32x | 32 | 1416 | 97.7% |
| Publish_MultiHandler/Publish (HandlerCount=2) | 107.2 | 398 | 3.71x | 32 | 392 | 91.8% |
| Publish_MultiHandler/Publish (HandlerCount=5) | 139.4 | 727.1 | 5.22x | 32 | 776 | 95.9% |
| Send_DI/Send | 632.4 | 421.8 | 0.67x | 440 | 312 | -41% |
| Send_DI_Pipeline/Send (BehaviorCount=1) | 767.5 | 585.7 | 0.76x | 576 | 448 | -28.6% |
| Send_DI_Pipeline/Send (BehaviorCount=2) | 822.5 | 689 | 0.84x | 688 | 560 | -22.9% |
| Send_DI_Pipeline/Send (BehaviorCount=5) | 983.9 | 847.5 | 0.86x | 1024 | 896 | -14.3% |
| Send_SingleHandler/Send | 282.1 | 363.7 | 1.29x | 272 | 312 | 12.8% |
