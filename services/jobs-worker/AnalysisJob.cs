using FactoryMES.Contracts;
using FactoryMES.Foundation;
using Quartz;

namespace FactoryMES.Jobs;

// 5초마다 접수된 분석을 Backend가 처리한다. LOCAL이 포함되면 GPU 작업 하나로 제한한다.
// 실제 분석·상태 변경은 Backend가 하고, 여기서는 일괄 처리 호출과 결과 기록만 한다.
[DisallowConcurrentExecution]
public sealed class AnalysisDispatchJob(
    InternalClient client,
    ILogger<AnalysisDispatchJob> logger
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var result = await client.SendAsync<ReportingDispatchResult>(
            "mes-backend",
            "/internal/v1/analyses/dispatch-batch",
            new DispatchBatchRequest(10),
            context.CancellationToken,
            requestTimeout: TimeSpan.FromSeconds(200)
        );
        JobLog.Batch(
            logger,
            "Analysis dispatch",
            result.Examined,
            result.Completed,
            result.Failed
        );
    }
}

// 매일 KST 02:00(UTC 17:00)과 60초 복구 주기: 지난 하루 창의 설비별 예약 분석을 Backend에 접수한다.
// 어떤 설비까지 접수했는지는 Backend의 작업·감사 이력이 기준이라, 재시작해도 이어서 진행된다.
[DisallowConcurrentExecution]
public sealed class ScheduledAnalysisJob(
    InternalClient client,
    ILogger<ScheduledAnalysisJob> logger
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var result = await client.SendAsync<ReportingDispatchResult>(
            "mes-backend",
            "/internal/v1/analyses/scheduled",
            new { },
            context.CancellationToken
        );
        JobLog.Batch(
            logger,
            "Scheduled analysis intake",
            result.Examined,
            result.Completed,
            result.Failed
        );
    }
}

// 일괄 처리 Job들의 공통 결과 로그.
// 처리한 것이 있으면 Information(누가: jobs-worker, 무엇을: 몇 건), 없으면 Debug.
// 실패 건이 있으면 Warning. 호출 자체의 실패는 InternalClient(Warning)와 Quartz(Error)가 남긴다.
internal static class JobLog
{
    public static void Batch(
        ILogger logger,
        string job,
        int examined,
        int completed,
        int failed
    )
    {
        var level = failed > 0
            ? LogLevel.Warning
            : examined > 0
                ? LogLevel.Information
                : LogLevel.Debug;
        logger.Log(
            level,
            "{Job}: examined {Examined}, completed {Completed}, failed {Failed}",
            job,
            examined,
            completed,
            failed
        );
    }
}






