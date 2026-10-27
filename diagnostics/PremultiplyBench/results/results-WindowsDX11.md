## WindowsDX11

- Adapter: NVIDIA GeForce RTX 5060 Laptop GPU
- OS: Microsoft Windows 10.0.26200 (X64), runtime .NET 10.0.12, 16 logical CPUs
- Build: Release
- CPU: AMD Ryzen 7 260 w/ Radeon 780M Graphics
- Images: 32: 2.9 KB, sha256 C8FB97991ED1; 128: 41.4 KB, sha256 D226456ED072; 512: 504.7 KB, sha256 5B79CC593EA9; 2048: 5955.0 KB, sha256 2B7847546EBF; 4096: 21037.0 KB, sha256 C827F225C5F9; 
- Correctness: 512x512: max per-channel diff CPU vs GPU = 0, channels off by more than 1 = 0 of 1048576 -> PASS

Summary (median ms). Both paths decode and upload (FromStream); they differ only in the premultiply step. CPU path = FromStream + CPU premultiply. GPU path = FromStream + GPU premultiply into a new RT, synced, minus the bare readback cost (sync). Submit-only = CPU-thread cost of the pooled draw when nothing waits on the GPU.

| Size | FromStream | CPU premul | GPU premul new RT | GPU new RT -sync | GPU pooled RT | GPU pooled -sync | GPU pooled submit-only | sync | CPU path | GPU path | Faster premul |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 32 | 0.15 | 0.02 | 0.43 | 0.41 | 0.04 | 0.02 | 0.004 | 0.022 | 0.17 | 0.56 | CPU |
| 128 | 0.65 | 0.05 | 0.38 | 0.36 | 0.04 | 0.01 | 0.004 | 0.023 | 0.70 | 1.01 | CPU |
| 512 | 4.06 | 0.70 | 0.57 | 0.55 | 0.06 | 0.04 | 0.005 | 0.023 | 4.76 | 4.61 | GPU |
| 2048 | 50.88 | 17.04 | 1.40 | 1.37 | 0.23 | 0.19 | 0.006 | 0.033 | 67.92 | 52.25 | GPU |
| 4096 | 156.82 | 55.89 | 3.05 | 2.99 | 5.16 | 5.10 | 0.002 | 0.061 | 212.71 | 159.81 | GPU |

<details><summary>Per-metric median / p90 (ms)</summary>

| Size | Metric | Median | p90 |
|---|---|---|---|
| 32 | 1_FromStream | 0.151 | 0.259 |
| 32 | 2_StbDecode | 0.077 | 0.090 |
| 32 | 3_CpuPremultiply | 0.020 | 0.024 |
| 32 | 4_Upload | 0.012 | 0.032 |
| 32 | 5a_GpuPremul_NewRT_Synced | 0.431 | 0.728 |
| 32 | 5b_GpuPremul_PooledRT_Synced | 0.040 | 0.096 |
| 32 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.004 | 0.004 |
| 32 | 5s_SyncOnly_FinishedRT | 0.022 | 0.029 |
| 128 | 1_FromStream | 0.645 | 0.933 |
| 128 | 2_StbDecode | 0.552 | 0.705 |
| 128 | 3_CpuPremultiply | 0.053 | 0.054 |
| 128 | 4_Upload | 0.067 | 0.133 |
| 128 | 5a_GpuPremul_NewRT_Synced | 0.384 | 0.613 |
| 128 | 5b_GpuPremul_PooledRT_Synced | 0.035 | 0.077 |
| 128 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.004 | 0.004 |
| 128 | 5s_SyncOnly_FinishedRT | 0.023 | 0.039 |
| 512 | 1_FromStream | 4.061 | 5.358 |
| 512 | 2_StbDecode | 6.873 | 7.333 |
| 512 | 3_CpuPremultiply | 0.701 | 0.724 |
| 512 | 4_Upload | 0.256 | 0.291 |
| 512 | 5a_GpuPremul_NewRT_Synced | 0.573 | 0.642 |
| 512 | 5b_GpuPremul_PooledRT_Synced | 0.061 | 0.076 |
| 512 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.005 | 0.005 |
| 512 | 5s_SyncOnly_FinishedRT | 0.023 | 0.029 |
| 2048 | 1_FromStream | 50.880 | 64.051 |
| 2048 | 2_StbDecode | 116.336 | 138.688 |
| 2048 | 3_CpuPremultiply | 17.044 | 20.971 |
| 2048 | 4_Upload | 3.886 | 4.320 |
| 2048 | 5a_GpuPremul_NewRT_Synced | 1.400 | 1.724 |
| 2048 | 5b_GpuPremul_PooledRT_Synced | 0.225 | 0.565 |
| 2048 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.006 | 0.008 |
| 2048 | 5s_SyncOnly_FinishedRT | 0.033 | 0.087 |
| 4096 | 1_FromStream | 156.818 | 163.365 |
| 4096 | 2_StbDecode | 354.742 | 373.997 |
| 4096 | 3_CpuPremultiply | 55.894 | 59.126 |
| 4096 | 4_Upload | 18.174 | 20.671 |
| 4096 | 5a_GpuPremul_NewRT_Synced | 3.049 | 3.372 |
| 4096 | 5b_GpuPremul_PooledRT_Synced | 5.163 | 6.764 |
| 4096 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.002 | 0.011 |
| 4096 | 5s_SyncOnly_FinishedRT | 0.061 | 0.426 |

</details>
