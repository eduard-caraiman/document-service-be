using System.Text;
using document_service.Database;
using document_service.Documents.Repositories;
using document_service.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddScoped<IDocumentStorage, LocalDocumentStorage>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddDbContext<DocumentDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    // Demonstrație temporară: salvează un text pe disc, fără metadate în baza de date.
    app.MapPost("/demo/save", async (IDocumentStorage storage, CancellationToken cancellationToken) =>
    {
        // 1. Alegem textul și generăm numele intern al fișierului.
        var text = "Salut";
        var storageKey = Guid.NewGuid().ToString("N");

        // 2. Transformăm textul în bytes și îi punem într-un flux de memorie.
        using var content = new MemoryStream(Encoding.UTF8.GetBytes(text));

        // 3. Apelăm codul din LocalDocumentStorage și așteptăm salvarea.
        await storage.SaveAsync(storageKey, content, cancellationToken);

        // 4. Confirmarea ajunge la client numai după încheierea salvării.
        return Results.Ok(new { storageKey, text, size = content.Length });
    });

    app.MapGet("/demo/read-file/{storageKey}",
        async ([FromRoute] string storageKey, IDocumentStorage storage, CancellationToken cancellationToken) =>
        {
            await using var fileStream = await storage.OpenReadAsync(storageKey, cancellationToken);
            using var reader = new StreamReader(fileStream);
            var text = await reader.ReadToEndAsync(cancellationToken);

            return Results.Ok(new { storageKey, text });
        });

    app.MapDelete("/demo/delete-file/{storageKey}",
        async ([FromRoute] string storageKey, IDocumentStorage storage, CancellationToken cancellationToken) =>
        {
            await storage.DeleteAsync(storageKey, cancellationToken);

            return Results.NoContent();
        });
}

app.UseHttpsRedirection();

app.Run();