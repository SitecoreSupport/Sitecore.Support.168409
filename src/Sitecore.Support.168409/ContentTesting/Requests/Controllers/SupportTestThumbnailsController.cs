using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Http.Results;
using Newtonsoft.Json;
using Sitecore.Analytics;
using Sitecore.Common;
using Sitecore.ContentTesting;
using Sitecore.ContentTesting.Configuration;
using Sitecore.ContentTesting.Managers;
using Sitecore.ContentTesting.Model;
using Sitecore.ContentTesting.Model.Extensions;
using Sitecore.ContentTesting.Pipelines.GetScreenShotForURL;
using Sitecore.ContentTesting.Requests.Controllers;
using Sitecore.ContentTesting.Screenshot;
using Sitecore.ContentTesting.ViewModel;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Jobs;
using Sitecore.Security.Accounts;
using Sitecore.Web.Http.Filters;

namespace Sitecore.Support.ContentTesting.Requests.Controllers
{
    [Authorize, ValidateHttpAntiForgeryToken]
    public class SupportTestThumbnailsController: ContentTestingControllerBase
    {
        public SupportTestThumbnailsController()
        {
            base.Database = Tracker.DefinitionDatabase;
        }

        #region Unchanged           
        [HttpGet, Obsolete("Use the StartGetThumbnails and TryFinishGetThumbnails actions instead")]
        public JsonResult<ThumbnailPath> GetThumbnail(string id, string version, string language, string mvvariants, string combination, string rules, string itemName, string compareVersion, string revision, string deviceId)
        {
            if (((ScreenshotGenerationState)Switcher<ScreenshotGenerationState, ScreenshotGenerationState>.CurrentValue) == ScreenshotGenerationState.Disabled)
            {
                return null;
            }
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            Item itemFromID = this.GetItemFromID(id);
            if (itemFromID == null)
            {
                return null;
            }
            GetScreenShotForURLArgs args = new GetScreenShotForURLArgs(itemFromID.ID)
            {
                Revision = string.IsNullOrEmpty(revision) ? itemFromID.Statistics.Revision : revision,
                Language = string.IsNullOrEmpty(language) ? itemFromID.Language.Name : language
            };
            args.DeviceId = this.ParseId(deviceId);
            int result = -1;
            int.TryParse(version, out result);
            if (result > 0)
            {
                args.Version = new int?(result);
            }
            int num2 = -1;
            int.TryParse(compareVersion, out num2);
            if (num2 > 0)
            {
                args.CompareVersion = new int?(num2);
            }
            TestExperience experience = null;
            ITestConfiguration configuration = base.ContentTestStore.LoadTestForItem(itemFromID, true);
            if (((configuration != null) && (configuration.TestDefinitionItem != null)) && (combination != null))
            {
                byte[] parsedCombination = combination.ParseFromMultiplexedString("-").ToArray<byte>();
                experience = (from exp in configuration.TestSet.GetExperiences()
                              where exp.Combination.IgnoreLengthCompareEquality(parsedCombination)
                              select exp).FirstOrDefault<TestExperience>();
            }
            if (!string.IsNullOrEmpty(rules))
            {
                args.Rules = ThumbnailRequest.ParseRules(rules);
            }
            if (!string.IsNullOrEmpty(mvvariants))
            {
                char[] separator = new char[] { '|' };
                IEnumerable<ShortID> enumerable = from v in mvvariants.Split(separator)
                                                  where !string.IsNullOrWhiteSpace(v)
                                                  select ShortID.Parse(v);
                args.MvVariants = enumerable;
            }
            if (experience != null)
            {
                args.Variants = from v in experience.Variants select ID.Parse(v);
            }
            args.Combination = combination;
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            HttpContextWrapper wrapper = base.Request.Properties["MS_HttpContext"] as HttpContextWrapper;
            if (wrapper == null)
            {
                Log.Error("Context is null.", this);
                return null;
            }
            HttpCookieCollection cookies = wrapper.Request.Cookies;
            foreach (string str2 in cookies.AllKeys)
            {
                HttpCookie cookie = cookies[str2];
                list.Add(new KeyValuePair<string, string>(cookie.Name, cookie.Value));
            }
            args.Cookies = list;
            string str = Sitecore.ContentTesting.Helpers.PathHelper.ConvertPathSeparatorsForUrl(new ThumbnailsManager().GenerateThumbnail(args));
            ThumbnailPath content = new ThumbnailPath
            {
                pathImage = str,
                itemName = itemName
            };
            return base.Json<ThumbnailPath>(content);
        }

        [HttpPost]
        public IHttpActionResult StartGetThumbnails([FromBody] string items)
        {
            if (((ScreenshotGenerationState)Switcher<ScreenshotGenerationState, ScreenshotGenerationState>.CurrentValue) == ScreenshotGenerationState.Disabled)
            {
                return null;
            }
            ThumbnailRequest[] requestArray = JsonConvert.DeserializeObject<ThumbnailRequest[]>(items);
            ScreenshotGenerationBatchBeginModel content = new ScreenshotGenerationBatchBeginModel();
            List<ScreenshotUrlModel> list = new List<ScreenshotUrlModel>();
            foreach (ThumbnailRequest request in requestArray)
            {
                ScreenShotFileNameGenerator generator = new ScreenShotFileNameGenerator(request);
                Item item = this.GetItem(request.Id, request.Language, request.Version);
                if (item != null)
                {
                    generator.Revision = item.Statistics.Revision;
                }
                string path = generator.GenerateFilePath();
                if (System.IO.File.Exists(Sitecore.ContentTesting.Helpers.PathHelper.MapPath(path)))
                {
                    string str2 = Sitecore.ContentTesting.Helpers.PathHelper.ConvertPathSeparatorsForUrl(path);
                    ScreenshotUrlModel model1 = new ScreenshotUrlModel
                    {
                        UId = request.UId,
                        Url = str2
                    };
                    list.Add(model1);
                }
            }
            content.Urls = list;
            if (list.Count != requestArray.Length)
            {
                object[] parameters = new object[] { items, Context.User };
                Job job = new Job(new JobOptions("Generate screenshots", "Content Testing", (Context.Site != null) ? Context.Site.Name : "", this, "GetThumbnails", parameters));
                JobManager.Start(job);
                content.Handle = job.Handle.ToString();
            }
            return base.Json<ScreenshotGenerationBatchBeginModel>(content);
        }

        [HttpGet]
        public IHttpActionResult TryFinishGetThumbnails(string handle)
        {
            Handle handle2 = Handle.Parse(handle);
            if (handle2 == null)
            {
                return null;
            }
            Job job = JobManager.GetJob(handle2);
            if (job == null)
            {
                return null;
            }
            ScreenshotGenerationBatchResultModel content = new ScreenshotGenerationBatchResultModel
            {
                IsDone = job.IsDone
            };
            ScreenshotUrlCollector customData = job.Options.CustomData as ScreenshotUrlCollector;
            if (customData != null)
            {
                content.Urls = customData.Urls;
            }
            return base.Json<ScreenshotGenerationBatchResultModel>(content);
        }
        #endregion
        #region Changed
        [HttpPost]
        public IHttpActionResult GetThumbnails([FromBody] string items, User user = null)
        {
            if (((ScreenshotGenerationState)Switcher<ScreenshotGenerationState, ScreenshotGenerationState>.CurrentValue) == ScreenshotGenerationState.Disabled)
            {
                return null;
            }
            ThumbnailRequest[] requestArray = JsonConvert.DeserializeObject<ThumbnailRequest[]>(items);
            ScreenshotUrlCollector collector = new ScreenshotUrlCollector(requestArray.Length, Context.Job);
            if (Context.Job != null)
            {
                Context.Job.Options.CustomData = collector;
            }
            HttpContextWrapper wrapper = base.Request.Properties["MS_HttpContext"] as HttpContextWrapper;
            if (wrapper == null)
            {
                Log.Error("Context is null.", this);
                return null;
            }
            HttpCookieCollection cookies = wrapper.Request.Cookies;
            //adding current hostname to cookies 
            if (wrapper.Request.Url != null)
            {
                string url = $"{wrapper.Request.Url.Scheme}://{wrapper.Request.Url.Host}";
                cookies.Add(new HttpCookie("hostName", url));
            }
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            foreach (string str in cookies.AllKeys)
            {
                HttpCookie cookie = cookies[str];
                list.Add(new KeyValuePair<string, string>(cookie.Name, cookie.Value));
            }
            IScreenshotGenerator screenshotGenerator = ContentTestingFactory.Instance.ScreenshotGenerator;
            foreach (ThumbnailRequest request in requestArray)
            {
                ID result = null;
                ID.TryParse(request.DeviceId, out result);
                ScreenshotTask task = new ScreenshotTask(request.ToVariation(), base.Database, ScreenshotTaskPriority.High)
                {
                    Cookies = list,
                    User = user,
                    ScaleBounds = request.ParseSize()
                };
                if (result != (ID)null)
                {
                    task.DeviceId = result;
                }
                screenshotGenerator.QueueTask(task, new Action<ScreenshotTask, ScreenshotUrlModel>(collector.TaskComplete));
            }
            collector.WaitHandle.WaitOne();
            return base.Json<ConcurrentBag<ScreenshotUrlModel>>(collector.Urls);
        }


        #endregion
    }
}