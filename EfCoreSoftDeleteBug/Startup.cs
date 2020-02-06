using EfCoreSoftDeleteBug.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EfCoreSoftDeleteBug
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
            services.AddControllers();

            services.AddDbContext<AppDbContext>(
                options => options
                    .UseLazyLoadingProxies()
                    .UseNpgsql(Configuration.GetConnectionString("EfCoreTest"))
            );

            services.AddHostedService<TestHostedService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }

    public class TestHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        public TestHostedService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var applicationLifetime = scope.ServiceProvider.GetRequiredService<IHostApplicationLifetime>();

            await InitDatabaseAsync(dbContext);

            var country = await dbContext.Country.FirstOrDefaultAsync();

            Console.WriteLine($"Initial people count: {country.People.Count}");

            foreach (var person in country.People)
            {
                dbContext.Person.Remove(person);
            }

            Console.WriteLine($"After .Remove() people count: {country.People.Count}");

            dbContext.SaveChanges();

            Console.WriteLine($"After .SaveChangesAsync() people count: {country.People.Count}");

            applicationLifetime.StopApplication();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task InitDatabaseAsync(AppDbContext dbContext)
        {
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();

            var country = new Country() { Name = "Estonia" };
            dbContext.Country.Add(country);

            await dbContext.SaveChangesAsync();

            dbContext.Person.Add(new Person() { Name = "Toomas", CountryId = country.Id });
            dbContext.Person.Add(new Person() { Name = "Peeter", CountryId = country.Id });
            dbContext.Person.Add(new Person() { Name = "Taavi", CountryId = country.Id });

            await dbContext.SaveChangesAsync();
        }
    }
}
