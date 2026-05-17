// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

var orders = new[]
{
    new Order(1, "Coffee beans", 2),
    new Order(2, "Espresso machine", 1),
    new Order(3, "Milk frother", 1),
};

app.MapGet("/", () => Results.Text("orders-api up", "text/plain"));

app.MapGet("/orders", () => orders);

app.MapGet("/orders/{id:int}", (int id) =>
    orders.FirstOrDefault(o => o.Id == id) is { } order
        ? Results.Ok(order)
        : Results.NotFound());

app.Run();

internal sealed record Order(int Id, string Item, int Quantity);
