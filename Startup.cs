using System;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniversalTennis.Algorithm.Data;
using UniversalTennis.Algorithm.Jobs;
using UniversalTennis.Algorithm.Middleware;
using UniversalTennis.Algorithm.Models;
using UniversalTennis.Algorithm.Repository;
using UniversalTennis.Algorithm.Service;

namespace UniversalTennis.Algorithm
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add functionality to inject IOptions<T>
            services.AddOptions();

            // require https
            services.Configure<MvcOptions>(options =>
            {
                options.Filters.Add(new RequireHttpsAttribute());
            });

            // Configure using a sub-section of the appsettings.json file.
            services.Configure<ConnectionStrings>(Configuration.GetSection("ConnectionStrings"));

            // Add our Config object so it can be injected
            services.Configure<Config>(Configuration);

            // Configure dbcontext
            services.AddDbContext<UniversalTennisContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

            // Add framework services.
            services.AddMvc();
            services.AddLogging();
            services.AddHangfire(o =>
            {
                o.UseSqlServerStorage(Configuration.GetConnectionString("DefaultConnection"));
            });

            // DI services
            services.AddScoped<IPlayerService, PlayerService>();
            services.AddScoped<IResultService, ResultService>();
            services.AddTransient<IJobService, JobService>();
            services.AddScoped<IPlayerRatingRepository, PlayerRatingRepository>();
            services.AddScoped<ISubRatingRepository, SubRatingRepository>();
            services.AddScoped<IRatingResultRepository, RatingResultRepository>();
            services.AddScoped<IRatingHistoryService, RatingHistoryService>();
            services.AddScoped<IRatingHistoryRepository, RatingHistoryRepository>();
            services.AddScoped<Algorithm>();
            services.AddScoped<AlgorithmDoubles>();
            services.AddScoped<IRatingJobRepository, RatingJobRepository>();
            services.AddTransient<IEventRepository, EventRepository>();


            var opt = new DbContextOptionsBuilder<UniversalTennisContext>();
            opt.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"));
            IServiceProvider provider = services.BuildServiceProvider();
            var config = provider.GetRequiredService<IOptions<Config>>();
            var loggerFactory1 = provider.GetRequiredService<ILoggerFactory>();
            services.AddSingleton(new PlayerEventListener(loggerFactory1, opt, config));
            var loggerFactory2 = provider.GetRequiredService<ILoggerFactory>();
            services.AddSingleton(new ResultEventListener(loggerFactory2, opt, config));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, IServiceScopeFactory serviceScopreFactory)
        {
            loggerFactory.AddConsole();
            loggerFactory.AddDebug();
            loggerFactory.AddAzureWebAppDiagnostics();
            app.UseConsumerTokenValidatorMiddleware();
            var options = new RewriteOptions()
                .AddRedirectToHttps();
            app.UseRewriter(options);
            app.UseMvc();

            // Configure hangfire to use the new JobActivator we defined.
            GlobalConfiguration.Configuration
                .UseActivator(new HangfireActivator(serviceScopreFactory));
            app.UseHangfireServer();
            GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute { Attempts = 0 });
            //app.UseHangfireDashboard("/jobs");

            if (env.IsProduction())
            {
                RecurringJob.AddOrUpdate<IJobService>(sc => sc.UpdateRatingsAndResults(), Cron.MinuteInterval(30));
                RecurringJob.AddOrUpdate<IRatingHistoryService>(sc => sc.SaveDailyRatings("V3_Singles"), Cron.Daily(8, 45));
                RecurringJob.AddOrUpdate<IRatingHistoryService>(sc => sc.SaveWeeklyAverage("WeeklyAverage_Singles", "V3_Singles"), Cron.Weekly(DayOfWeek.Monday, 9, 45));
            }
        }

        public class HangfireActivator : JobActivator
        {
            private readonly IServiceProvider _serviceProvider;

            public HangfireActivator(IServiceScopeFactory serviceScopeFactory)
            {
                _serviceProvider = serviceScopeFactory.CreateScope().ServiceProvider;
            }

            public override object ActivateJob(Type type)
            {
                return _serviceProvider.GetService(type);
            }
        }
    }
}
