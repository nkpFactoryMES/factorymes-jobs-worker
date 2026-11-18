using FactoryMES.Foundation;
using FactoryMES.Jobs;
using Quartz;

// Jobs Worker: Quartz 주기 작업으로 Backend의 일괄 처리 내부 API를 부르는 서비스.
// 업무 판단·저장은 모두 Backend가 하고, 여기서는 "언제 부를지"와 결과 로그만 맡는다.
// 같은 Job은 겹쳐 실행하지 않는다([DisallowConcurrentExecution]).
if (await HealthProbe.TryRunAsync(args))
    return;
var builder = WebApplication.CreateBuilder(args);
builder.AddLocalFoundation("jobs-worker");
InternalClient.Register(builder.Services, "jobs-worker");
builder.Services.AddQuartz(q =>
{
    var analysisKey = new JobKey("analysis-dispatch");
    q.AddJob<AnalysisDispatchJob>(o => o.WithIdentity(analysisKey));
    q.AddTrigger(t => t.ForJob(analysisKey)
        .WithIdentity("analysis-dispatch-trigger")
        .StartNow()
        .WithSimpleSchedule(s => s.WithIntervalInSeconds(5).RepeatForever()));
    var scheduledAnalysisKey = new JobKey("analysis-daily-kst");
    q.AddJob<ScheduledAnalysisJob>(o => o.WithIdentity(scheduledAnalysisKey));
    q.AddTrigger(t => t.ForJob(scheduledAnalysisKey)
        .WithIdentity("analysis-daily-kst-trigger")
        .WithCronSchedule("0 0 17 * * ?", c => c.InTimeZone(TimeZoneInfo.Utc)));
    // The daily trigger establishes the window; recovery drains further bounded batches.
    q.AddTrigger(t => t.ForJob(scheduledAnalysisKey)
        .WithIdentity("analysis-schedule-recovery-trigger")
        .StartNow()
        .WithSimpleSchedule(s => s.WithIntervalInSeconds(60).RepeatForever()));
    var reportingKey = new JobKey("daily-production-reporting");
    q.AddJob<ReportingJob>(o => o.WithIdentity(reportingKey));
    q.AddTrigger(t => t.ForJob(reportingKey)
        .WithIdentity("daily-production-reporting-trigger")
        .StartNow()
        .WithSimpleSchedule(s => s.WithIntervalInSeconds(60).RepeatForever()));
    var pendingKey = new JobKey("reporting-pending");
    q.AddJob<ReportingPendingJob>(o => o.WithIdentity(pendingKey));
    q.AddTrigger(t => t.ForJob(pendingKey)
        .WithIdentity("reporting-pending-trigger")
        .StartNow()
        .WithSimpleSchedule(s => s.WithIntervalInSeconds(5).RepeatForever()));
    var deliveryKey = new JobKey("local-delivery-dispatch");
    q.AddJob<DeliveryDispatchJob>(o => o.WithIdentity(deliveryKey));
    q.AddTrigger(t => t.ForJob(deliveryKey).WithIdentity("local-delivery-dispatch-trigger")
        .StartNow().WithSimpleSchedule(s => s.WithIntervalInSeconds(2).RepeatForever()));
    var watchKey = new JobKey("connections-watch");
    q.AddJob<ConnectionWatchJob>(o => o.WithIdentity(watchKey));
    q.AddTrigger(t => t.ForJob(watchKey)
        .WithIdentity("connections-watch-trigger")
        .StartNow()
        .WithSimpleSchedule(s => s.WithIntervalInSeconds(5).RepeatForever()));
    var key = new JobKey("skeleton-diagnostics");
    q.AddJob<DiagnosticsJob>(o => o.WithIdentity(key));
    q.AddTrigger(
        t => t.ForJob(key)
            .WithIdentity("skeleton-diagnostics-trigger")
            .WithSimpleSchedule(s => s.WithIntervalInSeconds(60)
                .RepeatForever())
    );
});
builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
var app = builder.Build();
app.UseMesErrors();
app.UseAuthentication();
app.UseAuthorization();
app.MapServiceBasics("jobs-worker");
app.MapGet("/health/ready", () => Results.Ok(new
{
    status = "ready",
    stage = "local-mock-delivery-and-watch"
}));
app.Run();
