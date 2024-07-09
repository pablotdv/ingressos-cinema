using Confluent.Kafka;
using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;
using SendGrid.Extensions.DependencyInjection;
using VendaIngressosCinema;
using VendaIngressosCinema.Services;
using Serilog.AspNetCore;
using Serilog;
using Serilog.Sinks.Elasticsearch;

try
{

    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        //.WriteTo.MongoDB("mongodb://localhost:27017/logs", collectionName: "LogVendaIngressos")
        .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200")))
        .CreateLogger();


    var builder = WebApplication.CreateBuilder(args);

    //builder.Host.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration.ReadFrom.Configuration(hostingContext.Configuration));

    // Add services to the container.

    builder.Services.AddControllers();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddSerilog();
    //builder.Services.AddHttpLogging(o => { });

    #region para ser usado no método sincrono
    builder.Services.AddHttpClient<AntifraudeService>(options =>
    {
        options.BaseAddress = new Uri(builder.Configuration.GetValue<string>("Antifraude:baseUrl"));
    });

    builder.Services.AddHttpClient<PagamentoService>(options =>
    {
        options.BaseAddress = new Uri(builder.Configuration.GetValue<string>("Pagamento:baseUrl"));
    });

    builder.Services.AddSendGrid(options =>
    {
        options.ApiKey = builder.Configuration.GetValue<string>("SENDGRID_API_KEY");
    });
    #endregion

    builder.Services
        .AddEntityFrameworkMongoDB()
        .AddMongoDB<IngressosContext>("mongodb://localhost:27017/local", "local", options => { });

    builder.Services.AddSingleton<IProducer<Null, String>>(c =>
    {
        var config = new ProducerConfig
        {
            BootstrapServers = "localhost:9092"
        };
        return new ProducerBuilder<Null, string>(config).Build();
    });

    builder.Services.AddScoped<EmailService>();


    builder.Services.AddHangfire(configuration => configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseMongoStorage(builder.Configuration.GetConnectionString("HangfireConnection")));


    var app = builder.Build();

    //app.UseHttpLogging();
    app.UseSerilogRequestLogging((options) =>
    {
        
    });

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseMiddleware<RequestResponseLoggingMiddleware>();

    app.UseAuthorization();

    app.MapControllers();

    app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

async Task<string> ReadRequestBody(HttpRequest request)
{
    // Ensure the request's body can be read multiple times (for the next middlewares in the pipeline).
    request.EnableBuffering();

    using var streamReader = new StreamReader(request.Body, leaveOpen: true);
    var requestBody = await streamReader.ReadToEndAsync();

    // Reset the request's body stream position for next middleware in the pipeline.
    request.Body.Position = 0;
    return requestBody;
}
