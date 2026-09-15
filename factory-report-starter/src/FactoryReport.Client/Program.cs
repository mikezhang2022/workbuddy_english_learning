using FactoryReport.Client;
using FactoryReport.Client.Configuration;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiOptions = builder.Configuration
    .GetSection(ClientApiOptions.SectionName)
    .Get<ClientApiOptions>() ?? new ClientApiOptions();

builder.Services.AddClientServices(apiOptions);

await builder.Build().RunAsync();
