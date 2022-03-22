using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MinimalApiDemo.Data;
using MinimalApiDemo.Models;
using MiniValidation;
using NetDevPack.Identity;
using NetDevPack.Identity.Jwt;
using NetDevPack.Identity.Model;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<MinimalContextDb>(options =>
options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentityEntityFrameworkContextConfiguration(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
    b => b.MigrationsAssembly("MinimalApiDemo")));

builder.Services.AddIdentityConfiguration();
builder.Services.AddJwtConfiguration(builder.Configuration, "AppSettings");

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("DeleteProvider",
        policy => policy.RequireClaim("DeleteProvider"));
});


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthConfiguration();
app.UseHttpsRedirection();


app.MapPost("/register", [AllowAnonymous] async (
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    IOptions<AppJwtSettings> appJwtSettings,
    RegisterUser registerUser) =>
    {
        if (registerUser == null)
            return Results.BadRequest("User not informed");

        if (!MiniValidator.TryValidate(registerUser, out var errors))
            return Results.ValidationProblem(errors);

        var user = new IdentityUser
        {
            UserName = registerUser.Email,
            Email = registerUser.Email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, registerUser.Password);

        if (!result.Succeeded)
            return Results.BadRequest(result.Errors);

        var jwt = new JwtBuilder()
            .WithUserManager(userManager)
            .WithJwtSettings(appJwtSettings.Value)
            .WithEmail(user.Email)
            .WithJwtClaims()
            .WithUserClaims()
            .WithUserRoles()
            .BuildToken();

        return Results.Ok(jwt);
    })
    .ProducesValidationProblem()
    .Produces<Provider>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("RegisterUser")
    .WithTags("User"); 


app.MapPost("/login", [AllowAnonymous] async (
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    IOptions<AppJwtSettings> appJwtSettings,
    LoginUser loginUser) =>
{
    if (loginUser == null)
        return Results.BadRequest("User not informed");

    if (!MiniValidator.TryValidate(loginUser, out var errors))
        return Results.ValidationProblem(errors);

    var result = await signInManager.PasswordSignInAsync(loginUser.Email, loginUser.Password, true, true);

    if (result.IsLockedOut)
        return Results.BadRequest("The user is blocked");

    if (!result.Succeeded)
        return Results.BadRequest("User or Password invalid");

    var jwt = new JwtBuilder()
        .WithUserManager(userManager)
        .WithJwtSettings(appJwtSettings.Value)
        .WithEmail(loginUser.Email)
        .WithJwtClaims()
        .WithUserClaims()
        .WithUserRoles()
        .BuildToken();

    return Results.Ok(jwt);
})
    .ProducesValidationProblem()
    .Produces<Provider>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("LoginUser")
    .WithTags("User"); ;


app.MapGet("/provider", [AllowAnonymous] async (
    MinimalContextDb context) =>
    await context.Providers.ToListAsync())
    .WithName("GetProvider")
    .WithTags("Provider");

app.MapGet("/provider/{id}", [AllowAnonymous] async (
    Guid id,
    MinimalContextDb context) =>
    await context.Providers.FindAsync(id)
        is Provider provider
            ? Results.Ok(provider)
            : Results.NotFound())
    .Produces<Provider>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .WithName("GetProviderById")
    .WithTags("Provider");

app.MapPost("/provider", [Authorize] async (
    MinimalContextDb context,
    Provider provider) =>
    {
        if (!MiniValidator.TryValidate(provider, out var errors))
            return Results.ValidationProblem(errors);

        context.Providers.Add(provider); ;
        var result = await context.SaveChangesAsync();

        return result > 0
            //? Results.Created($"/provider/{provider.Id}", provider)
            ? Results.CreatedAtRoute("GetProviderById", new { id = provider.Id }, provider)
            : Results.BadRequest("There is a problem to save the provider");
    })
    .ProducesValidationProblem()
    .Produces<Provider>(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("PostProvider")
    .WithTags("Provider");


app.MapPut("/provider/{id}", [Authorize] async (
    Guid id,
    MinimalContextDb context,
    Provider provider) =>
{
    var providerDB = await context.Providers.AsNoTracking<Provider>()
                                            .FirstOrDefaultAsync(p => p.Id == id);

    if (providerDB == null) return Results.NotFound();

    if (!MiniValidator.TryValidate(provider, out var errors))
        return Results.ValidationProblem(errors);

    context.Providers.Update(provider); ;
    var result = await context.SaveChangesAsync();

    return result > 0
        //? Results.Created($"/provider/{provider.Id}", provider)
        ? Results.NoContent()
        : Results.BadRequest("There is a problem to save the provider");

}).ProducesValidationProblem()
    .Produces<Provider>(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("PutProvider")
    .WithTags("Provider");

app.MapDelete("/provider/{id}", [Authorize] async (
    Guid id,
    MinimalContextDb context) =>
{
    var provider = await context.Providers.FindAsync(id);
    if (provider == null) return Results.NotFound();

    context.Providers.Remove(provider); ;
    var result = await context.SaveChangesAsync();

    return result > 0
        //? Results.Created($"/provider/{provider.Id}", provider)
        ? Results.NoContent()
        : Results.BadRequest("There is a problem to save the provider");

}).ProducesValidationProblem()
    .Produces<Provider>(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status404NotFound)
    .RequireAuthorization("DeleteProvider")
    .WithName("DeleteProvider")
    .WithTags("Provider");

app.Run();
