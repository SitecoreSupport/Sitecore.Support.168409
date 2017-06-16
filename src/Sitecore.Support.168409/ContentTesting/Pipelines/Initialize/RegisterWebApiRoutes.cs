using System.Drawing;
using System.Web.Http;
using System.Web.Routing;
using Sitecore.ContentTesting.Configuration;
using Sitecore.ContentTesting.Diagnostics;
using Sitecore.ContentTesting.Requests.Registration;
using Sitecore.Diagnostics;
using Sitecore.Pipelines;
using Sitecore.StringExtensions;

namespace Sitecore.Support.ContentTesting.Pipelines.Initialize
{
    public class RegisterWebApiRoutes
    {
        #region Changed
        public virtual void Process(PipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (!Settings.IsAutomaticContentTestingEnabled)
            {
                return;
            }
            this.RegisterRouteForSession(RouteTable.Routes, "Sitecore.ContentTesting.CreateTestDialog", "CreateTestDialog");
            this.RegisterRouteForSessionSupportController(RouteTable.Routes, "Sitecore.ContentTesting.TestThumbnatilsRoute", "TestThumbnails");
            this.RegisterRoutes(RouteTable.Routes, args);
        }
        private RouteBase RegisterRouteForSessionSupportController(RouteCollection routes, string name, string controller)
        {
            RouteBase routeBase = routes[name];
            if (routeBase != null)
            {
                Logger.Warn("Route '{0}' has already been added. Ensure only a single route processor for Content Testing.".FormatWith(new object[]
                {
                    name
                }));
                return routeBase;
            }
            Route route = routes.MapHttpRoute(name, Settings.CommandRoutePrefix + controller + "/{action}", new
            {
                //changed mapping to support controller
                controller = "SupportTestThumbnails"
            });
            route.RouteHandler = new SessionRouteHandler();
            return route;
        }
        #endregion

        #region Unchanged
        protected virtual void RegisterRoutes(RouteCollection routes, PipelineArgs args)
        {
            if (routes["Sitecore.ContentTesting"] != null)
            {
                Logger.Warn("Route 'Sitecore.ContentTesting' has already been added. Ensure only a single route processor for Content Testing.");
                return;
            }
            routes.MapHttpRoute("Sitecore.ContentTesting", Settings.CommandRoutePrefix + "{controller}/{action}");
        }

        protected virtual RouteBase RegisterRouteForSession(RouteCollection routes, string name, string controller)
        {
            RouteBase routeBase = routes[name];
            if (routeBase != null)
            {
                Logger.Warn("Route '{0}' has already been added. Ensure only a single route processor for Content Testing.".FormatWith(new object[]
                {
                    name
                }));
                return routeBase;
            }
            Route route = routes.MapHttpRoute(name, Settings.CommandRoutePrefix + controller + "/{action}", new
            {
                controller
            });
            route.RouteHandler = new SessionRouteHandler();
            return route;
        }


        #endregion Unchanged


    }
}