## BlazorGL-Interp

- Adapter: 
- OS: Browser (Wasm), runtime .NET 10.0.12, 1 logical CPUs
- Build: Release
- Browser: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/156.0.0.0 Safari/537.36
- Images: 32: 2.9 KB, sha256 C8FB97991ED1; 128: 41.4 KB, sha256 D226456ED072; 512: 504.7 KB, sha256 5B79CC593EA9; 2048: 5955.0 KB, sha256 2B7847546EBF; 4096: 21037.0 KB, sha256 C827F225C5F9; 
- Correctness: 512x512: max per-channel diff CPU vs GPU = 0, channels off by more than 1 = 0 of 1048576 -> PASS

Summary (median ms). Both paths decode and upload (FromStream); they differ only in the premultiply step. CPU path = FromStream + CPU premultiply. GPU path = FromStream + GPU premultiply into a new RT, synced, minus the bare readback cost (sync). Submit-only = CPU-thread cost of the pooled draw when nothing waits on the GPU.

| Size | FromStream | CPU premul | GPU premul new RT | GPU new RT -sync | GPU pooled RT | GPU pooled -sync | GPU pooled submit-only | sync | CPU path | GPU path | Faster premul |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 32 | 0.55 | 0.00 | 1.20 | 0.90 | 0.70 | 0.40 | 0.200 | 0.300 | 0.55 | 1.45 | CPU |
| 128 | 5.90 | 0.20 | 2.10 | 1.15 | 1.30 | 0.35 | 0.200 | 0.950 | 6.10 | 7.05 | CPU |
| 512 | 72.40 | 3.30 | 1.50 | 1.20 | 0.70 | 0.40 | 0.200 | 0.300 | 75.70 | 73.60 | GPU |
| 2048 | 716.70 | 52.30 | 3.20 | 2.70 | 0.90 | 0.40 | 0.200 | 0.500 | 769.00 | 719.40 | GPU |
| 4096 | 3025.50 | 210.00 | 4.80 | 4.50 | 1.30 | 1.00 | 0.200 | 0.300 | 3235.50 | 3030.00 | GPU |

<details><summary>Per-metric median / p90 (ms)</summary>

| Size | Metric | Median | p90 |
|---|---|---|---|
| 32 | 1_FromStream | 0.550 | 1.400 |
| 32 | 2_StbDecode | 0.400 | 0.700 |
| 32 | 3_CpuPremultiply | 0.000 | 0.100 |
| 32 | 4_Upload | 0.000 | 0.100 |
| 32 | 5a_GpuPremul_NewRT_Synced | 1.200 | 1.500 |
| 32 | 5b_GpuPremul_PooledRT_Synced | 0.700 | 1.000 |
| 32 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.200 | 0.300 |
| 32 | 5s_SyncOnly_FinishedRT | 0.300 | 0.400 |
| 128 | 1_FromStream | 5.900 | 6.800 |
| 128 | 2_StbDecode | 5.000 | 5.600 |
| 128 | 3_CpuPremultiply | 0.200 | 0.300 |
| 128 | 4_Upload | 0.100 | 0.100 |
| 128 | 5a_GpuPremul_NewRT_Synced | 2.100 | 2.600 |
| 128 | 5b_GpuPremul_PooledRT_Synced | 1.300 | 1.500 |
| 128 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.200 | 0.400 |
| 128 | 5s_SyncOnly_FinishedRT | 0.950 | 2.000 |
| 512 | 1_FromStream | 72.400 | 79.300 |
| 512 | 2_StbDecode | 50.900 | 55.500 |
| 512 | 3_CpuPremultiply | 3.300 | 4.100 |
| 512 | 4_Upload | 0.600 | 0.700 |
| 512 | 5a_GpuPremul_NewRT_Synced | 1.500 | 1.700 |
| 512 | 5b_GpuPremul_PooledRT_Synced | 0.700 | 0.900 |
| 512 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.200 | 0.400 |
| 512 | 5s_SyncOnly_FinishedRT | 0.300 | 0.600 |
| 2048 | 1_FromStream | 716.700 | 806.300 |
| 2048 | 2_StbDecode | 614.200 | 650.400 |
| 2048 | 3_CpuPremultiply | 52.300 | 56.000 |
| 2048 | 4_Upload | 1.200 | 8.000 |
| 2048 | 5a_GpuPremul_NewRT_Synced | 3.200 | 7.000 |
| 2048 | 5b_GpuPremul_PooledRT_Synced | 0.900 | 5.100 |
| 2048 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.200 | 0.200 |
| 2048 | 5s_SyncOnly_FinishedRT | 0.500 | 0.600 |
| 4096 | 1_FromStream | 3025.500 | 3193.100 |
| 4096 | 2_StbDecode | 2392.500 | 2497.800 |
| 4096 | 3_CpuPremultiply | 210.000 | 215.300 |
| 4096 | 4_Upload | 27.900 | 29.300 |
| 4096 | 5a_GpuPremul_NewRT_Synced | 4.800 | 5.500 |
| 4096 | 5b_GpuPremul_PooledRT_Synced | 1.300 | 2.000 |
| 4096 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.200 | 0.400 |
| 4096 | 5s_SyncOnly_FinishedRT | 0.300 | 0.400 |

</details>
