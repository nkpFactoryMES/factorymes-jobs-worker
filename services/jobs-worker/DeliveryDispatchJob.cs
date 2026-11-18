using FactoryMES.Contracts;
using FactoryMES.Foundation;
using Quartz;

namespace FactoryMES.Jobs;

// 2초마다: (1) Outbox 이벤트를 로컬 발송 대기열로 넘기고 (2) Mock 또는 로컬 통신 발송을 처리하게 한다.
// 실제 이메일·채팅 발송은 하지 않는다(external sends=0). 처리한 건이 있으면 Information,
// 재시도 대기·실패가 있으면 Warning, 아무 일도 없으면 Debug.
[DisallowConcurrentExecution]
public sealed class DeliveryDispatchJob(
    InternalClient client,
    ILogger<DeliveryDispatchJob> logger
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var outbox = await client.SendAsync<DispatchBatchResult>(
            "mes-backend",
            "/internal/v1/jobs/outbox/dispatch-batch",
            new DispatchBatchRequest(50),
            context.CancellationToken,
            requestTimeout: TimeSpan.FromSeconds(20)
        );
        var mock = await client.SendAsync<DispatchBatchResult>(
            "mes-backend",
            "/internal/v1/jobs/deliveries/dispatch-batch",
            new DispatchBatchRequest(100),
            context.CancellationToken,
            requestTimeout: TimeSpan.FromSeconds(20)
        );

        var troubled = outbox.Failed + outbox.RetryPending + mock.Failed + mock.RetryPending;
        var level = troubled > 0
            ? LogLevel.Warning
            : outbox.Examined > 0 || mock.Examined > 0
                ? LogLevel.Information
                : LogLevel.Debug;
        logger.Log(
            level,
            "Local Outbox accepted={Outbox}; Delivery examined={Mock} delivered={Delivered} "
                + "retry={Retry} failed={Failed}; external sends=0",
            outbox.Delivered,
            mock.Examined,
            mock.Delivered,
            outbox.RetryPending + mock.RetryPending,
            outbox.Failed + mock.Failed
        );
    }
}



