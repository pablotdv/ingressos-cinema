using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;
using VendaIngressosCinema;
using VendaIngressosCinemaHangfire;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services
    .AddEntityFrameworkMongoDB()
    .AddMongoDB<IngressosContext>("mongodb://localhost:27017/local", "local", options => { });

var migrationOptions = new MongoMigrationOptions
{
    MigrationStrategy = new MigrateMongoMigrationStrategy(),
    BackupStrategy = new CollectionMongoBackupStrategy()
};
var storageOptions = new MongoStorageOptions
{
    MigrationOptions = migrationOptions
};

builder.Services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseMongoStorage(builder.Configuration.GetConnectionString("HangfireConnection"), storageOptions));

builder.Services.AddHangfireServer();

var app = builder.Build();

GlobalConfiguration.Configuration.UseActivator(new ContainerJobActivator(app.Services));

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();


app.UseHangfireDashboard();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
