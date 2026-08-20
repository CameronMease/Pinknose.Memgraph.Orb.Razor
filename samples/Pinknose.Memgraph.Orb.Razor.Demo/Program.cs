using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Pinknose.Memgraph.Orb.Razor.Demo;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// No Router: this is a single page. Routing on GitHub Pages needs a 404.html copy of
// index.html to make deep links resolve, which is a workaround for a problem one page
// does not have.
builder.RootComponents.Add<Demo>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

await builder.Build().RunAsync();
