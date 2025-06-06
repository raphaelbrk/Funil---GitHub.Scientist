using FunilRollout.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure Redis
builder.Services.AddSingleton<RedisConfigProvider>();
builder.Services.AddSingleton<RolloutFunnel>();

// Registra os serviços de validação e publishers
builder.Services.AddScoped<ValidadorRollout>();
builder.Services.AddScoped<ValidadorDocumentos>();
builder.Services.AddScoped<ValidadorElegibilidadeFunil>();
builder.Services.AddScoped<CustomResultPublisher>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<GitHubScientist>();
builder.Services.AddSingleton<Teste>();
builder.Services.AddSingleton<FunilAvancado>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
