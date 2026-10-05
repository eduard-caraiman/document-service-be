using document_service.Database;
using document_service.Documents.Repositories;
using document_service.Documents.Services;
using document_service.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

var app = builder.Build();

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

    return Results.Created($"/api/documents/{document.Id}", document);
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

    return Results.Ok(document);
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

    return Results.Ok(documents);
});

app.Run();