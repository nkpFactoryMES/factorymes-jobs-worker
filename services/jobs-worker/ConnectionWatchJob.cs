using FactoryMES.Contracts;
using FactoryMES.Foundation;
using Quartz;

namespace FactoryMES.Jobs;

// 5초마다: 수집 연결이 끊긴(COMM_LOST) 대상을 Backend가 확인해 경보를 열거나 복구하게 한다.
// 경보가 새로 열리거나 복구되면 Information, 아무 변화가 없으면 Debug.
[DisallowConcurrentExecution]
public sealed class ConnectionWatchJob(
    InternalClient client,
    ILogger<ConnectionWatchJob> logger
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var result = await client.SendAsync<ConnectionWatchResult>(
            "mes-backend",
            "/internal/v1/jobs/connections/watch",
            new DispatchBatchRequest(100),
            context.CancellationToken
        );
        var level = result.Opened > 0 || result.Recovered > 0
            ? LogLevel.Information
            : LogLevel.Debug;
        logger.Log(
            level,
            "Connection watch: examined={Examined}, opened={Opened}, recovered={Recovered}",
            result.Examined,
            result.Opened,
            result.Recovered
        );
    }
}
