using APC.Infrastructure;
using APC.Infrastructure.Services;
using APC.Kernel;
using APC.Kernel.Constants;
using APC.Kernel.Extensions;
using APC.Kernel.Registrations;
using APC.Scheduler;
using APC.Services;
using MassTransit;
using Quartz;
using StackExchange.Redis;

ModuleRegistration registration = new(ModuleType.APC, typeof(IHost));
IHost host = Host.CreateDefaultBuilder(args)
                 .ConfigureServices(services => {
                   services.AddTelemetry(registration);
                   services.AddHostedService<Worker>();
                   services.AddMassTransit(mt => {
                     mt.UsingRabbitMq((ctx, cfg) => {
                       cfg.SetupRabbitMq();
                       cfg.ConfigureEndpoints(ctx);
                     });
                   });

                   services.AddSingleton<IConnectionMultiplexer>(
                     ConnectionMultiplexer.Connect(
                       Configuration.GetApcVar(ApcVariable.APC_REDIS_HOST)));
                   services.AddScoped<IApcDatabase, MongoDatabase>();
                   services.AddSingleton<IApcCache, ApcCache>();
                   services.AddScoped<IArtifactService, ArtifactService>();

                   services.AddQuartz(q => {
                     q.AddJob<TrackingJob>(
                       j => j.WithIdentity(TrackingJob.S_KEY));
                     q.AddTrigger(t => {
                       t.WithIdentity("tracking-trigger", "apc");
                       t.ForJob(TrackingJob.S_KEY);
                       t.WithCronSchedule("0 0 0/2 ? * * *");
                     });
                   });

                   services.AddQuartzHostedService(q => {
                     q.WaitForJobsToComplete = true;
                   });
                 })
                 .Build();
await host.RunAsync();