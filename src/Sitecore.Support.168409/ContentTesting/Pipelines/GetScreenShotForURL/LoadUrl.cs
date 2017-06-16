using System.Collections.Generic;
using System.Linq;
using Sitecore.ContentTesting.Pipelines.GetScreenShotForURL;
using Sitecore.Diagnostics;
using Sitecore.Text;
using Sitecore.Web;

namespace Sitecore.Support.ContentTesting.Pipelines.GetScreenShotForURL
{
    [UsedImplicitly]
    public class LoadUrl: GenerateScreenShotProcessor
    {
        public override void Process(GetScreenShotForURLArgs args)
        {
            Assert.IsNotNull(args, "args");
            UrlString urlString = new UrlString(args.UrlParameters)
            {
                Path = "/"
            };
            //getting hostname cookie added in SupportTestThumbnailsController
            Dictionary<string, string> cookiesDictionary = args.Cookies.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            string hostName = cookiesDictionary["hostName"];
            if (!string.IsNullOrEmpty(hostName))
            {
                args.Url = hostName + urlString.GetUrl();
            }
            else
            {
                args.Url = WebUtil.GetServerUrl() + urlString.GetUrl();
            }
        }
    }
}