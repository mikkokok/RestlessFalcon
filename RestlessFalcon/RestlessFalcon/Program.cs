using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using RestlessFalcon.Converters;
using RestlessFalcon.Endpoints;
using RestlessFalcon.Helpers;
using RestlessFalcon.Helpers.Impl;
using RestlessFalcon.State;

// Exposes the entry point to local integration tests without using MVC hosting.
public partial class Program
{
    public static void Main(string[] args) => BuildApplication(args).Run();

    public static WebApplication BuildApplication(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new CustomDateTimeConverter()));
        builder.Services.AddTransient<IDatabaseHelper, DatabaseHelper>();
        builder.Services.AddScoped<AuthKeyHelper>();
        builder.Services.AddScoped<IStateStore, SqlStateStore>();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1",
                Title = "Restlessfalcon API",
                Description = "Collection of inhouse APIs",
                Contact = new OpenApiContact { Name = "Mikko Kokkonen", Email = "outlook.com" },
                License = new OpenApiLicense { Name = "Licensed to MK" }
            });
            var xmlPath = Path.Combine(AppContext.BaseDirectory,
                $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);
            options.OperationFilter<StateAuthOperationFilter>();
        });

        // Allow for additional configuration in tests
        configure?.Invoke(builder);

        var app = builder.Build();
        if (app.Environment.IsDevelopment())
            app.UseDeveloperExceptionPage();
        else
            app.UseHsts();
        app.UseHttpsRedirection();
        app.UseSwagger();
        app.UseSwaggerUI(options =>
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Restlessfalcon API"));
        app.MapExistingEndpoints();
        app.MapStateEndpoints();
        return app;
    }
}
