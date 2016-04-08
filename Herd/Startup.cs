using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(Herd.Startup))]
namespace Herd
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
