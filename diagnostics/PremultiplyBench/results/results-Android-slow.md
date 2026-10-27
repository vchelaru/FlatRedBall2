## Android (slow)

- Adapter: 
- OS: Android (API level 32) (Arm64), runtime .NET 10.0.12, 4 logical CPUs
- Build: Release
- Device: unknown Phh-Treble with GApps (Android 12, API 32)
- SoC/hardware: unknown unknown mt8766
- GL_RENDERER: PowerVR Rogue GE8300
- GL_VERSION: OpenGL ES 3.2 build 1.10@5130912
- GL_VENDOR: Imagination Technologies
- Images: 32: 2.9 KB, sha256 C8FB97991ED1; 128: 41.4 KB, sha256 D226456ED072; 512: 504.7 KB, sha256 5B79CC593EA9; 2048: 5955.0 KB, sha256 2B7847546EBF; 4096: 21037.0 KB, sha256 C827F225C5F9; 
- Correctness: 512x512: max per-channel diff CPU vs GPU = 1, channels off by more than 1 = 0 of 1048576 -> PASS

Summary (median ms). Both paths decode and upload (FromStream); they differ only in the premultiply step. CPU path = FromStream + CPU premultiply. GPU path = FromStream + GPU premultiply into a new RT, synced, minus the bare readback cost (sync). Submit-only = CPU-thread cost of the pooled draw when nothing waits on the GPU.

| Size | FromStream | CPU premul | GPU premul new RT | GPU new RT -sync | GPU pooled RT | GPU pooled -sync | GPU pooled submit-only | sync | CPU path | GPU path | Faster premul |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 32 | 1.92 | 0.05 | 2.67 | 1.88 | 2.22 | 1.43 | 0.680 | 0.790 | 1.98 | 3.80 | CPU |
| 128 | 4.67 | 0.85 | 2.57 | 1.75 | 2.27 | 1.45 | 0.622 | 0.828 | 5.52 | 6.42 | CPU |
| 512 | 39.76 | 14.04 | 2.71 | 1.89 | 2.29 | 1.46 | 0.624 | 0.824 | 53.80 | 41.65 | GPU |
| 2048 | 532.04 | 218.97 | 31.62 | 30.30 | 8.73 | 7.40 | 1.130 | 1.324 | 751.00 | 562.34 | GPU |
| 4096 | 1873.75 | 877.02 | 120.80 | 119.53 | 29.45 | 28.18 | 1.272 | 1.268 | 2750.77 | 1993.28 | GPU |

<details><summary>Per-metric median / p90 (ms)</summary>

| Size | Metric | Median | p90 |
|---|---|---|---|
| 32 | 1_FromStream | 1.923 | 2.137 |
| 32 | 2_StbDecode | 0.525 | 0.605 |
| 32 | 3_CpuPremultiply | 0.054 | 0.055 |
| 32 | 4_Upload | 0.111 | 0.171 |
| 32 | 5a_GpuPremul_NewRT_Synced | 2.672 | 3.420 |
| 32 | 5b_GpuPremul_PooledRT_Synced | 2.224 | 2.387 |
| 32 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.680 | 0.859 |
| 32 | 5s_SyncOnly_FinishedRT | 0.790 | 0.930 |
| 128 | 1_FromStream | 4.670 | 5.137 |
| 128 | 2_StbDecode | 7.090 | 7.449 |
| 128 | 3_CpuPremultiply | 0.854 | 0.875 |
| 128 | 4_Upload | 0.157 | 0.291 |
| 128 | 5a_GpuPremul_NewRT_Synced | 2.575 | 2.684 |
| 128 | 5b_GpuPremul_PooledRT_Synced | 2.273 | 2.502 |
| 128 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.622 | 0.800 |
| 128 | 5s_SyncOnly_FinishedRT | 0.828 | 0.953 |
| 512 | 1_FromStream | 39.760 | 76.724 |
| 512 | 2_StbDecode | 94.023 | 99.044 |
| 512 | 3_CpuPremultiply | 14.036 | 14.163 |
| 512 | 4_Upload | 1.127 | 1.407 |
| 512 | 5a_GpuPremul_NewRT_Synced | 2.713 | 2.814 |
| 512 | 5b_GpuPremul_PooledRT_Synced | 2.286 | 2.480 |
| 512 | 5c_GpuPremul_PooledRT_SubmitOnly | 0.624 | 0.995 |
| 512 | 5s_SyncOnly_FinishedRT | 0.824 | 0.945 |
| 2048 | 1_FromStream | 532.037 | 677.266 |
| 2048 | 2_StbDecode | 1314.312 | 1315.756 |
| 2048 | 3_CpuPremultiply | 218.965 | 221.176 |
| 2048 | 4_Upload | 17.386 | 36.219 |
| 2048 | 5a_GpuPremul_NewRT_Synced | 31.624 | 33.119 |
| 2048 | 5b_GpuPremul_PooledRT_Synced | 8.726 | 8.979 |
| 2048 | 5c_GpuPremul_PooledRT_SubmitOnly | 1.130 | 1.433 |
| 2048 | 5s_SyncOnly_FinishedRT | 1.324 | 1.756 |
| 4096 | 1_FromStream | 1873.752 | 1902.929 |
| 4096 | 2_StbDecode | 5008.902 | 5016.590 |
| 4096 | 3_CpuPremultiply | 877.022 | 878.237 |
| 4096 | 4_Upload | 142.876 | 145.711 |
| 4096 | 5a_GpuPremul_NewRT_Synced | 120.798 | 123.076 |
| 4096 | 5b_GpuPremul_PooledRT_Synced | 29.451 | 31.410 |
| 4096 | 5c_GpuPremul_PooledRT_SubmitOnly | 1.272 | 2.050 |
| 4096 | 5s_SyncOnly_FinishedRT | 1.268 | 1.312 |

</details>
