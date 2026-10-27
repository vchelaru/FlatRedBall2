## Android (fast)

- Adapter: 
- OS: Android (API level 36) (Arm64), runtime .NET 10.0.12, 8 logical CPUs
- Build: Release
- Device: motorola motorola edge 2025 (Android 16, API 36)
- SoC/hardware: Mediatek MT6878 mt6878
- GL_RENDERER: Mali-G615 MC2
- GL_VERSION: OpenGL ES 3.2 v1.r44p1-01eac0.148b774f93332d7e562320793fb6c4c9
- GL_VENDOR: ARM
- Images: 32: 2.9 KB, sha256 C8FB97991ED1; 128: 41.4 KB, sha256 D226456ED072; 512: 504.7 KB, sha256 5B79CC593EA9; 2048: 5955.0 KB, sha256 2B7847546EBF; 4096: 21037.0 KB, sha256 C827F225C5F9; 
- Correctness: 512x512: max per-channel diff CPU vs GPU = 0, channels off by more than 1 = 0 of 1048576 -> PASS

Summary (median ms). Both paths decode and upload (FromStream); they differ only in the premultiply step. CPU path = FromStream + CPU premultiply. GPU path = FromStream + GPU premultiply into a new RT, synced, minus the bare readback cost (sync). Submit-only = CPU-thread cost of the pooled draw when nothing waits on the GPU.

| Size | FromStream | CPU premul | GPU premul new RT | GPU new RT -sync | GPU pooled RT | GPU pooled -sync | GPU pooled submit-only | sync | CPU path | GPU path | Faster premul |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 32 | 1.27 | 0.01 | 0.60 | 0.59 | 0.37 | 0.37 | 0.201 | 0.003 | 1.28 | 1.87 | CPU |
| 128 | 2.50 | 0.12 | 0.50 | 0.50 | 0.35 | 0.35 | 0.083 | 0.003 | 2.62 | 3.00 | CPU |
| 512 | 17.04 | 1.95 | 0.62 | 0.62 | 0.36 | 0.36 | 0.105 | 0.003 | 18.99 | 17.66 | GPU |
| 2048 | 200.42 | 30.82 | 2.86 | 2.86 | 0.84 | 0.83 | 0.189 | 0.003 | 231.25 | 203.28 | GPU |
| 4096 | 768.24 | 122.88 | 10.68 | 10.67 | 3.75 | 3.73 | 0.441 | 0.017 | 891.12 | 778.90 | GPU |

<details><summary>Per-metric median / p90 (ms)</summary>

| Size | Metric | Median | p90 |
|---|---|---|---|
| 32 | 1_FromStream | 1.273 | 1.557 |
| 32 | 2_StbDecode | 0.104 | 0.112 |
| 32 | 3_CpuPremultiply | 0.008 | 0.008 |
| 32 | 4_Upload | 0.053 | 0.084 |
| 32 | 5a_GpuPremul_NewRT_Synced | 0.596 | 1.151 |
| 32 | 5b_GpuPremul_PooledRT_Synced | 0.373 | 0.482 |
| 32 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.201 | 0.272 |
| 32 | 5s_SyncOnly_FinishedRT | 0.003 | 0.004 |
| 128 | 1_FromStream | 2.500 | 2.649 |
| 128 | 2_StbDecode | 1.461 | 1.526 |
| 128 | 3_CpuPremultiply | 0.123 | 0.123 |
| 128 | 4_Upload | 0.051 | 0.064 |
| 128 | 5a_GpuPremul_NewRT_Synced | 0.500 | 0.601 |
| 128 | 5b_GpuPremul_PooledRT_Synced | 0.349 | 0.438 |
| 128 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.083 | 0.094 |
| 128 | 5s_SyncOnly_FinishedRT | 0.003 | 0.004 |
| 512 | 1_FromStream | 17.038 | 27.941 |
| 512 | 2_StbDecode | 18.655 | 19.212 |
| 512 | 3_CpuPremultiply | 1.952 | 1.964 |
| 512 | 4_Upload | 0.267 | 0.298 |
| 512 | 5a_GpuPremul_NewRT_Synced | 0.625 | 0.724 |
| 512 | 5b_GpuPremul_PooledRT_Synced | 0.360 | 0.450 |
| 512 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.105 | 0.449 |
| 512 | 5s_SyncOnly_FinishedRT | 0.003 | 0.003 |
| 2048 | 1_FromStream | 200.421 | 206.448 |
| 2048 | 2_StbDecode | 242.019 | 247.714 |
| 2048 | 3_CpuPremultiply | 30.825 | 32.230 |
| 2048 | 4_Upload | 4.268 | 11.635 |
| 2048 | 5a_GpuPremul_NewRT_Synced | 2.861 | 3.255 |
| 2048 | 5b_GpuPremul_PooledRT_Synced | 0.836 | 1.070 |
| 2048 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.189 | 0.454 |
| 2048 | 5s_SyncOnly_FinishedRT | 0.003 | 0.003 |
| 4096 | 1_FromStream | 768.237 | 777.734 |
| 4096 | 2_StbDecode | 912.382 | 938.230 |
| 4096 | 3_CpuPremultiply | 122.882 | 123.170 |
| 4096 | 4_Upload | 23.610 | 30.216 |
| 4096 | 5a_GpuPremul_NewRT_Synced | 10.682 | 10.745 |
| 4096 | 5b_GpuPremul_PooledRT_Synced | 3.750 | 4.161 |
| 4096 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.441 | 38.598 |
| 4096 | 5s_SyncOnly_FinishedRT | 0.017 | 0.042 |

</details>
