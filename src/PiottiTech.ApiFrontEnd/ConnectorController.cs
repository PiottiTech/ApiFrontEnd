using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http.Controllers;

namespace PiottiTech.ApiFrontEnd
{
    public abstract class ConnectorController : IHttpController
    {
        #region Protected Abstract Properties

        /// DEVNOTE: BaseUri should have a value like: "http://localhost:52899/api";

        protected abstract string BaseUri { get; }

        //DEVNOTE: Whitelist should have a value like: new string[] { "SelectAutoModels", "InsertAutoModel"};
        protected abstract string Whitelist { get; }

        #endregion Protected Abstract Properties

        #region Private Properties and Methods

        private IEnumerable<string> EnumerableWhitelist => Whitelist.ToCleanWhiteList();

        private bool IsUrlComponentSafe(string component)
        {
            return Regex.IsMatch(component, "^[A-Za-z0-9]+$");
        }

        private Uri CreateUri(string controller, IDictionary<string, object> routeValues, HttpRequestMessage request)
        {
            UriBuilder builder = new UriBuilder(BaseUri);
            builder.Query = string.Join("&", from pair in request.GetQueryNameValuePairs()
                                             select string.Format("{0}={1}", pair.Key, pair.Value));

            if (routeValues.ContainsKey("id"))
            {
                builder.Path += string.Format("/{0}/{1}", controller, routeValues["id"]);
            }
            else
            {
                builder.Path += string.Format("/{0}", controller);
            }

            return builder.Uri;
        }

        private HttpResponseMessage CreateResponse(HttpWebResponse webResponse)
        {
            var response = new HttpResponseMessage()
            {
                StatusCode = webResponse?.StatusCode ?? HttpStatusCode.InternalServerError,
                Content = new PushStreamContent((outStream, content, transportContext) =>
                {
                    if (webResponse == null)
                    {
                        return;
                    }
                    using (outStream)
                    using (var inStream = webResponse.GetResponseStream())
                    {
                        if (inStream == null)
                        {
                            return;
                        }
                        byte[] buffer = new byte[8096];
                        int bytesRead = inStream.Read(buffer, 0, buffer.Length);
                        while (bytesRead > 0)
                        {
                            outStream.Write(buffer, 0, bytesRead);
                            bytesRead = inStream.Read(buffer, 0, buffer.Length);
                        }
                    }
                }, webResponse?.ContentType?.Split(';')?.First() ?? "application/json")
            };

            return response;
        }

        private async Task<HttpResponseMessage> ConnectGet(string controller, IDictionary<string, object> routeValues, HttpRequestMessage request)
        {
            var uri = CreateUri(controller, routeValues, request);

            var webRequest = WebRequest.CreateHttp(uri);

            var webResponse = (HttpWebResponse)await webRequest.GetResponseAsync();

            return CreateResponse(webResponse);
        }

        private async Task<HttpResponseMessage> ConnectPost(string controller, IDictionary<string, object> routeValues, HttpRequestMessage request)
        {
            var uri = CreateUri(controller, routeValues, request);

            HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpRequest.Method = "POST";
            httpRequest.ContentType = "application/json";

            using (var inputStream = await request.Content.ReadAsStreamAsync())
            using (var outputStream = await httpRequest.GetRequestStreamAsync())
            {
                var buffer = new byte[8096];
                int bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length);
                while (bytesRead > 0)
                {
                    await outputStream.WriteAsync(buffer, 0, bytesRead);
                    bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length);
                }
            }

            try
            {
                return CreateResponse((HttpWebResponse)httpRequest.GetResponse());
            }
            catch (WebException ex)
            {
                return CreateResponse(ex.Response as HttpWebResponse);
            }
        }

        #endregion Private Properties and Methods

        #region Public Methods

        public async Task<HttpResponseMessage> ExecuteAsync(HttpControllerContext controllerContext, System.Threading.CancellationToken cancellationToken)
        {
            object controller;
            if (controllerContext.RouteData.Values.TryGetValue("realController", out controller) == false || EnumerableWhitelist.Contains(controller.ToString().ToLower()) == false)
            {
                //DEVNOTE: invalid requests at front end or middleware will return 404.
                return controllerContext.Request.CreateResponse(HttpStatusCode.NotFound);
            }

            if (IsUrlComponentSafe(controller.ToString()) == false || (controllerContext.RouteData.Values.ContainsKey("id") == true && IsUrlComponentSafe(controllerContext.RouteData.Values["id"].ToString()) == false))
            {
                //Logger.Log("Failed IsUrlComponentSafe checks: " + controller.ToString());
                return controllerContext.Request.CreateResponse(HttpStatusCode.Forbidden);
            }

            switch (controllerContext.Request.Method.Method.ToUpper())
            {
                case "GET":
                    return await ConnectGet(controller.ToString(), controllerContext.RouteData.Values, controllerContext.Request);

                case "POST":
                    return await ConnectPost(controller.ToString(), controllerContext.RouteData.Values, controllerContext.Request);

                case "OPTIONS":
                    return controllerContext.Request.CreateResponse(HttpStatusCode.OK);

                default:
                    //Logger.Log("controllerContext.Request.Method unknown: " + controllerContext.Request.Method.Method.ToUpper());
                    return controllerContext.Request.CreateResponse(HttpStatusCode.Forbidden);
            }
        }

        #endregion Public Methods
    }
}