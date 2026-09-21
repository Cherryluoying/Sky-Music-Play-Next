// 模块：SkyMusic.App 界面状态 PerformanceModeOption
using SkyMusic.Core.Execution;

namespace SkyMusic.App.ViewModels;

public sealed record PerformanceModeOption(string Label, WorkloadExecutionMode Mode);
