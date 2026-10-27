## DesktopGL

- Adapter: NVIDIA GeForce RTX 5060 Laptop GPU/PCIe/SSE2
- OS: Microsoft Windows 10.0.26200 (X64), runtime .NET 10.0.12, 16 logical CPUs
- Build: Release
- CPU: AMD Ryzen 7 260 w/ Radeon 780M Graphics
- GL_RENDERER: NVIDIA GeForce RTX 5060 Laptop GPU/PCIe/SSE2
- GL_VERSION: 4.6.0 NVIDIA 610.47
- GL_VENDOR: NVIDIA Corporation
- Images: 32: 2.9 KB, sha256 C8FB97991ED1; 128: 41.4 KB, sha256 D226456ED072; 512: 504.7 KB, sha256 5B79CC593EA9; 2048: 5955.0 KB, sha256 2B7847546EBF; 4096: 21037.0 KB, sha256 C827F225C5F9; 
- Correctness: 512x512: max per-channel diff CPU vs GPU = 0, channels off by more than 1 = 0 of 1048576 -> PASS

Summary (median ms). Both paths decode and upload (FromStream); they differ only in the premultiply step. CPU path = FromStream + CPU premultiply. GPU path = FromStream + GPU premultiply into a new RT, synced, minus the bare readback cost (sync). Submit-only = CPU-thread cost of the pooled draw when nothing waits on the GPU.

| Size | FromStream | CPU premul | GPU premul new RT | GPU new RT -sync | GPU pooled RT | GPU pooled -sync | GPU pooled submit-only | sync | CPU path | GPU path | Faster premul |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 32 | 0.12 | 0.02 | 0.86 | 0.86 | 0.11 | 0.11 | 0.009 | 0.002 | 0.13 | 0.97 | CPU |
| 128 | 0.57 | 0.05 | 0.77 | 0.76 | 0.09 | 0.09 | 0.008 | 0.004 | 0.63 | 1.34 | CPU |
| 512 | 6.93 | 0.81 | 1.12 | 0.87 | 0.50 | 0.25 | 0.011 | 0.253 | 7.74 | 7.80 | CPU |
| 2048 | 81.43 | 11.24 | 6.55 | 3.83 | 4.87 | 2.15 | 0.004 | 2.724 | 92.67 | 85.25 | GPU |
| 4096 | 385.24 | 64.72 | 60.66 | 41.03 | 32.23 | 12.60 | 0.003 | 19.637 | 449.96 | 426.26 | GPU |

<details><summary>Per-metric median / p90 (ms)</summary>

| Size | Metric | Median | p90 |
|---|---|---|---|
| 32 | 1_FromStream | 0.116 | 0.159 |
| 32 | 2_StbDecode | 0.071 | 0.108 |
| 32 | 3_CpuPremultiply | 0.017 | 0.021 |
| 32 | 4_Upload | 0.014 | 0.021 |
| 32 | 5a_GpuPremul_NewRT_Synced | 0.860 | 1.435 |
| 32 | 5b_GpuPremul_PooledRT_Synced | 0.111 | 0.312 |
| 32 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.009 | 0.010 |
| 32 | 5s_SyncOnly_FinishedRT | 0.002 | 0.003 |
| 128 | 1_FromStream | 0.575 | 0.756 |
| 128 | 2_StbDecode | 0.460 | 0.499 |
| 128 | 3_CpuPremultiply | 0.053 | 0.054 |
| 128 | 4_Upload | 0.017 | 0.044 |
| 128 | 5a_GpuPremul_NewRT_Synced | 0.769 | 1.353 |
| 128 | 5b_GpuPremul_PooledRT_Synced | 0.094 | 0.151 |
| 128 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.008 | 0.009 |
| 128 | 5s_SyncOnly_FinishedRT | 0.004 | 0.004 |
| 512 | 1_FromStream | 6.930 | 7.571 |
| 512 | 2_StbDecode | 7.785 | 8.542 |
| 512 | 3_CpuPremultiply | 0.809 | 0.819 |
| 512 | 4_Upload | 0.097 | 0.343 |
| 512 | 5a_GpuPremul_NewRT_Synced | 1.120 | 1.572 |
| 512 | 5b_GpuPremul_PooledRT_Synced | 0.504 | 0.654 |
| 512 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.011 | 0.011 |
| 512 | 5s_SyncOnly_FinishedRT | 0.253 | 0.292 |
| 2048 | 1_FromStream | 81.425 | 83.320 |
| 2048 | 2_StbDecode | 81.868 | 84.770 |
| 2048 | 3_CpuPremultiply | 11.244 | 11.394 |
| 2048 | 4_Upload | 2.225 | 2.711 |
| 2048 | 5a_GpuPremul_NewRT_Synced | 6.553 | 7.237 |
| 2048 | 5b_GpuPremul_PooledRT_Synced | 4.872 | 5.551 |
| 2048 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.004 | 0.010 |
| 2048 | 5s_SyncOnly_FinishedRT | 2.724 | 3.126 |
| 4096 | 1_FromStream | 385.237 | 420.986 |
| 4096 | 2_StbDecode | 374.886 | 395.810 |
| 4096 | 3_CpuPremultiply | 64.719 | 67.182 |
| 4096 | 4_Upload | 9.646 | 11.260 |
| 4096 | 5a_GpuPremul_NewRT_Synced | 60.664 | 65.019 |
| 4096 | 5b_GpuPremul_PooledRT_Synced | 32.232 | 35.950 |
| 4096 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.003 | 0.012 |
| 4096 | 5s_SyncOnly_FinishedRT | 19.637 | 28.340 |

</details>
