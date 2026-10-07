using document_service.Database;
using document_service.Documents.Messaging;
using document_service.Documents.Repositories;
using document_service.Documents.Responses;
using document_service.Documents.Services;
using document_service.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddScoped<IDocumentStorage, LocalDocumentStorage>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddDbContext<DocumentDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddSingleton<IConnection>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var rabbitMq = configuration.GetRequiredSection("RabbitMq");

    var factory = new ConnectionFactory
    {
        HostName = rabbitMq["HostName"]
                   ?? throw new InvalidOperationException("RabbitMQ HostName lipsește."),
        Port = rabbitMq.GetValue<int>("Port"),
        UserName = rabbitMq["UserName"]
                   ?? throw new InvalidOperationException("RabbitMQ UserName lipsește."),
        Password = rabbitMq["Password"]
                   ?? throw new InvalidOperationException("RabbitMQ Password lipsește."),
        AutomaticRecoveryEnabled = true
    };

    return factory.CreateConnectionAsync().GetAwaiter().GetResult();
});

builder.Services.AddHostedService<DocumentUploadConsumer>();
builder.Services.AddScoped<IDocumentCreatedPublisher, RabbitMqDocumentCreatedPublisher>();


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}


app.UseHttpsRedirection();


app.MapPost("/api/documents", async (
    [FromForm] IFormFile file,
    IDocumentService documentService,
    CancellationToken cancellationToken) =>
{
    await using var content = file.OpenReadStream();

    var document = await documentService.CreateAsync(
        file.FileName,
        file.ContentType,
        file.Length,
        content,
        cancellationToken);

    return Results.Created($"/api/documents/{document.Id}", GetDocumentResponse.From(document));
}).DisableAntiforgery();


app.MapGet("/api/documents/{id:guid}", async (
    [FromRoute] Guid id,
    IDocumentService documentService) =>
{
    var document = await documentService.GetByIdAsync(id);

    if (document is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(GetDocumentResponse.From(document));
});

app.MapGet("/api/documents/{id:guid}/download", async (
    [FromRoute] Guid id,
    IDocumentService documentService,
    CancellationToken cancellationToken) =>
{
    var result = await documentService.DownloadAsync(id, cancellationToken);

    if (result is null)
    {
        return Results.NotFound();
    }

    return Results.File(
        result.Value.Content,
        result.Value.Document.ContentType,
        result.Value.Document.FileName);
});

app.MapDelete("/api/documents/{id:guid}", async (
    [FromRoute] Guid id,
    IDocumentService documentService,
    CancellationToken cancellationToken) =>
{
    var deleted = await documentService.DeleteAsync(id, cancellationToken);

    if (!deleted)
    {
        return Results.NotFound();
    }

    return Results.NoContent();
});

app.MapGet("/api/documents", async (
    IDocumentService documentService) =>
{
    var documents = await documentService.GetAllAsync();

    return Results.Ok(documents.Select(GetDocumentResponse.From));
});

app.Run();