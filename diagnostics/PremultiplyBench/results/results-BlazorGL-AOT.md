## BlazorGL-AOT

- Adapter: 
- OS: Browser (Wasm), runtime .NET 10.0.12, 1 logical CPUs
- Build: Release
- Browser: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/156.0.0.0 Safari/537.36
- Images: 32: 2.9 KB, sha256 C8FB97991ED1; 128: 41.4 KB, sha256 D226456ED072; 512: 504.7 KB, sha256 5B79CC593EA9; 2048: 5955.0 KB, sha256 2B7847546EBF; 4096: 21037.0 KB, sha256 C827F225C5F9; 
- Correctness: 512x512: max per-channel diff CPU vs GPU = 0, channels off by more than 1 = 0 of 1048576 -> PASS

Summary (median ms). Both paths decode and upload (FromStream); they differ only in the premultiply step. CPU path = FromStream + CPU premultiply. GPU path = FromStream + GPU premultiply into a new RT, synced, minus the bare readback cost (sync). Submit-only = CPU-thread cost of the pooled draw when nothing waits on the GPU.

| Size | FromStream | CPU premul | GPU premul new RT | GPU new RT -sync | GPU pooled RT | GPU pooled -sync | GPU pooled submit-only | sync | CPU path | GPU path | Faster premul |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 32 | 0.20 | 0.00 | 2.20 | 1.80 | 1.00 | 0.60 | 0.100 | 0.400 | 0.20 | 2.00 | CPU |
| 128 | 1.00 | 0.10 | 1.90 | 1.50 | 0.70 | 0.30 | 0.100 | 0.400 | 1.10 | 2.50 | CPU |
| 512 | 9.20 | 0.70 | 1.30 | 1.00 | 0.90 | 0.60 | 0.100 | 0.300 | 9.90 | 10.20 | CPU |
| 2048 | 91.50 | 11.10 | 2.00 | 1.70 | 0.50 | 0.20 | 0.100 | 0.300 | 102.60 | 93.20 | GPU |
| 4096 | 369.10 | 44.90 | 5.30 | 4.90 | 1.40 | 1.00 | 0.100 | 0.400 | 414.00 | 374.00 | GPU |

<details><summary>Per-metric median / p90 (ms)</summary>

| Size | Metric | Median | p90 |
|---|---|---|---|
| 32 | 1_FromStream | 0.200 | 0.300 |
| 32 | 2_StbDecode | 0.100 | 0.200 |
| 32 | 3_CpuPremultiply | 0.000 | 0.000 |
| 32 | 4_Upload | 0.000 | 0.100 |
| 32 | 5a_GpuPremul_NewRT_Synced | 2.200 | 3.100 |
| 32 | 5b_GpuPremul_PooledRT_Synced | 1.000 | 1.400 |
| 32 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.100 | 0.200 |
| 32 | 5s_SyncOnly_FinishedRT | 0.400 | 0.600 |
| 128 | 1_FromStream | 1.000 | 1.400 |
| 128 | 2_StbDecode | 0.700 | 0.900 |
| 128 | 3_CpuPremultiply | 0.100 | 0.100 |
| 128 | 4_Upload | 0.100 | 0.200 |
| 128 | 5a_GpuPremul_NewRT_Synced | 1.900 | 2.400 |
| 128 | 5b_GpuPremul_PooledRT_Synced | 0.700 | 1.600 |
| 128 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.100 | 0.100 |
| 128 | 5s_SyncOnly_FinishedRT | 0.400 | 0.500 |
| 512 | 1_FromStream | 9.200 | 10.500 |
| 512 | 2_StbDecode | 8.500 | 10.000 |
| 512 | 3_CpuPremultiply | 0.700 | 0.900 |
| 512 | 4_Upload | 0.500 | 0.600 |
| 512 | 5a_GpuPremul_NewRT_Synced | 1.300 | 1.600 |
| 512 | 5b_GpuPremul_PooledRT_Synced | 0.900 | 1.700 |
| 512 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.100 | 0.200 |
| 512 | 5s_SyncOnly_FinishedRT | 0.300 | 0.300 |
| 2048 | 1_FromStream | 91.500 | 100.300 |
| 2048 | 2_StbDecode | 95.200 | 101.100 |
| 2048 | 3_CpuPremultiply | 11.100 | 11.900 |
| 2048 | 4_Upload | 1.400 | 8.300 |
| 2048 | 5a_GpuPremul_NewRT_Synced | 2.000 | 7.000 |
| 2048 | 5b_GpuPremul_PooledRT_Synced | 0.500 | 0.700 |
| 2048 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.100 | 0.100 |
| 2048 | 5s_SyncOnly_FinishedRT | 0.300 | 0.400 |
| 4096 | 1_FromStream | 369.100 | 385.800 |
| 4096 | 2_StbDecode | 384.600 | 402.400 |
| 4096 | 3_CpuPremultiply | 44.900 | 45.600 |
| 4096 | 4_Upload | 27.200 | 28.400 |
| 4096 | 5a_GpuPremul_NewRT_Synced | 5.300 | 5.500 |
| 4096 | 5b_GpuPremul_PooledRT_Synced | 1.400 | 1.700 |
| 4096 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.100 | 0.400 |
| 4096 | 5s_SyncOnly_FinishedRT | 0.400 | 0.500 |

</details>
