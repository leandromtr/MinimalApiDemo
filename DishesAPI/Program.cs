using AutoMapper;
using DishesAPI.DbContexts;
using DishesAPI.Entities;
using DishesAPI.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<DishesDbContext>(o => o.UseSqlite(
    builder.Configuration["ConnectionStrings:DishesDBConnectionString"]));

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

var dishesEndPoints = app.MapGroup("/dishes");
var dishWithGuidIdEndPoints = dishesEndPoints.MapGroup("/{dishId:guid}");
var ingredientsEndPoints = dishWithGuidIdEndPoints.MapGroup("/ingredients");

//app.MapGet("/dishes", (DishesDbContext dishesDbContext) =>
//{
//    return dishesDbContext.Dishes;
//});

//async
app.MapGet("/dishes", async Task<Ok<IEnumerable<DishDto>>> (DishesDbContext dishesDbContext, IMapper mapper, [FromQuery] string? name) =>
{
    return TypedResults.Ok(mapper.Map<IEnumerable<DishDto>>(await dishesDbContext.Dishes.ToListAsync()));
});

app.MapGet("/dishes/{dishId:guid}", async Task<Results<NotFound, Ok<DishDto>>> (DishesDbContext dishesDbContext, IMapper mapper, Guid dishId) =>
{
    var dishEntity = await dishesDbContext.Dishes
    .FirstOrDefaultAsync(d => d.Id == dishId);
    if (dishEntity == null)
    {
        return TypedResults.NotFound();
    }
    return TypedResults.Ok(mapper.Map<DishDto>(dishEntity));
}).WithName("GetDish");

app.MapGet("/dishes/{dishName}", async Task<Ok<DishDto>> (DishesDbContext dishesDbContext, IMapper mapper, string dishName) =>
{
    return TypedResults.Ok( mapper.Map<DishDto>(await dishesDbContext.Dishes.FirstOrDefaultAsync(d => d.Name == dishName)));
});

app.MapGet("/dishes/{dishId}/ingredients", async Task<Results<NotFound, Ok<IEnumerable<IngredientDto>>>> (DishesDbContext dishesDbContext, IMapper mapper, Guid dishId) =>
{
    return TypedResults.Ok (mapper.Map<IEnumerable<IngredientDto>>((await dishesDbContext.Dishes
        .Include(d => d.Ingredients)
        .FirstOrDefaultAsync(d => d.Id == dishId))?.Ingredients));
});


//app.MapPost("/dishes", async Task<Created<DishDto>> (DishesDbContext dishesDbContext, IMapper mapper, DishForCreationDto dishForCreationDto, LinkGenerator linkGenerator, HttpContext httpContext) =>
//{
//    var dishEntitity = mapper.Map<Dish>(dishForCreationDto);
//    dishesDbContext.Add(dishEntitity);
//    await dishesDbContext.SaveChangesAsync();

//    var dishToReturn = mapper.Map<DishDto>(dishEntitity);

//    var linkToDish = linkGenerator.GetUriByName(
//        httpContext,
//        "GetDish", new { dishId = dishToReturn.Id });

//    var dishReturn = mapper.Map<DishDto>(dishEntitity);

//    return TypedResults.Created(linkToDish, dishToReturn);
//});

app.MapPost("/dishes", async Task<CreatedAtRoute<DishDto>> (DishesDbContext dishesDbContext, IMapper mapper, DishForCreationDto dishForCreationDto) =>
{
    var dishEntitity = mapper.Map<Dish>(dishForCreationDto);
    dishesDbContext.Add(dishEntitity);
    await dishesDbContext.SaveChangesAsync();

    var dishToReturn = mapper.Map<DishDto>(dishEntitity);

    return TypedResults.CreatedAtRoute(dishToReturn, "GetDish",  new { dishId = dishToReturn.Id });
});

app.MapPut("/dishes/{dishId:guid}", async Task<Results<NotFound, NoContent>> (DishesDbContext dishesDbContext, IMapper mapper, Guid dishId, DishForUpdateDto dishForUpdateDto) =>
{
    var dishEntitity = await dishesDbContext.Dishes.FirstOrDefaultAsync(d=> d.Id == dishId);
    if (dishEntitity == null)
    {
        return TypedResults.NotFound();
    }

    mapper.Map(dishForUpdateDto, dishEntitity);
    await dishesDbContext.SaveChangesAsync();

    var dishToReturn = mapper.Map<DishDto>(dishEntitity);

    return TypedResults.NoContent();
});

app.MapDelete("/dishes/{dishId:guid}", async Task<Results<NotFound, NoContent>> (DishesDbContext dishesDbContext, Guid dishId) =>
{
    var dishEntitity = await dishesDbContext.Dishes.FirstOrDefaultAsync(d => d.Id == dishId);
    if (dishEntitity == null)
    {
        return TypedResults.NotFound();
    }

    dishesDbContext.Remove(dishEntitity);
    await dishesDbContext.SaveChangesAsync();
    return TypedResults.NoContent();
});

using (var serviceScope = app.Services.GetService<IServiceScopeFactory>().CreateScope())
{
    var context = serviceScope.ServiceProvider.GetRequiredService<DishesDbContext>();
    context.Database.EnsureDeleted();
    context.Database.Migrate();
}

app.Run();