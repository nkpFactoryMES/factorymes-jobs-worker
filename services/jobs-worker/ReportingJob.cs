using System.Globalization;
using FactoryMES.Contracts;
using FactoryMES.Foundation;
using Quartz;

namespace FactoryMES.Jobs;

// 60초마다: 어제·오늘(KST 영업일) 생산 통계를 Backend가 다시 집계하게 한다.
// 결과(작업 이력)는 Backend가 저장한다. 실패 상태면 Warning, 정상 반영은 Debug(매분 반복).
[DisallowConcurrentExecution]
public sealed class ReportingJob(
    InternalClient client,
    ILogger<ReportingJob> logger
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(9));
        foreach (var day in new[] {
            today.AddDays(-1),
            today
        })
        {
            var date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var run = await client.SendAsync<JobRunView>(
                "mes-backend",
                "/internal/v1/reporting/rebuild",
                new ReportingRebuildRequest(date),
                context.CancellationToken
            );
            var level = run.Status == "FAILED"
                ? LogLevel.Warning
                : LogLevel.Debug;
            logger.Log(
                level,
                "Reporting rebuild {Date}: run {JobRunId} {Status}, rows {Rows}{Error}",
                date,
                run.JobRunId,
                run.Status,
                run.AffectedRows,
                run.Error is null ? "" : " (" + run.Error + ")"
            );
        }
    }
}

// 5초마다: 화면에서 요청한 재집계(대기 중 작업)를 최대 10건 Backend가 처리하게 한다.
[DisallowConcurrentExecution]
public sealed class ReportingPendingJob(
    InternalClient client,
    ILogger<ReportingPendingJob> logger
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var result = await client.SendAsync<ReportingDispatchResult>(
            "mes-backend",
            "/internal/v1/reporting/pending",
            new DispatchBatchRequest(10),
            context.CancellationToken
        );
        JobLog.Batch(
            logger,
            "Reporting pending rebuilds",
            result.Examined,
            result.Completed,
            result.Failed
        );
    }
}
