using Quartz;

namespace FactoryMES.Jobs;

// 60초마다 Quartz가 살아 있음을 남기는 점검 Job. 업무나 외부 호출은 하지 않는다.
// 매분 반복되는 정상 신호라 Debug로 남긴다.
[DisallowConcurrentExecution]
public sealed class DiagnosticsJob(ILogger<DiagnosticsJob> logger) : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug(
            "Local Quartz skeleton heartbeat; no business or external provider job executed."
        );
        return Task.CompletedTask;
    }
}
